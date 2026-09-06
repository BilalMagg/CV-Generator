#!/usr/bin/env python3
"""
extract_contacts_pdf.py
=======================

Extract the HR-contacts directory from the PDF
"docs/datasets/Email des RH pour ceux qui seront a la recherche des stages de fin d'etude .pdf"
into a CSV.

The PDF is an Excel (2013) print export:
  - only the first 17 pages (0-16) contain data (~2,300 records); the rest are blank grids
  - the table is positioned text: 11 columns (SOCIETE, GROUPE, VILLE, ACTIVITE, PERSONNE,
    FONCTION/SERVICE, TEL, FAX, GSM, EMAIL, ADRESSE), cells placed by x-band + y-row
  - accented characters are NOT in the ToUnicode map (pymupdf returns U+FFFD), but the
    outgoing glyph CIDs are WinAnsi byte codes (0xE9 = é, 0xE8 = è, ...), recovered here
    via a cp1252 lookup on the per-character CID from get_texttrace().

The parser is deterministic (no OCR / no LLM):
  1. read every char via page.get_texttrace(): (cid, origin, bbox)
  2. decode cid -> unicode via cp1252
  3. group chars into words (x-proximity), words into rows (baseline y)
  4. detect the 11 header columns from the repeated header row (SOCIETE ... ADRESSE)
  5. classify each word into a column by x-band
  6. anchor records on rows that start in the SOCIETE band; continuation rows
     (wrapped cells) append to the current record
  7. join per-column words, write CSV (UTF-8 with BOM so Excel shows accents correctly)

Usage:
  python scripts/extract_contacts_pdf.py [input.pdf] [output.csv]
  python scripts/extract_contacts_pdf.py --dump <input.pdf> <page_index>   # diagnostic

Only requires: pymupdf (pip install pymupdf) + stdlib.
"""

from __future__ import annotations

import argparse
import collections
import os
import re
import sys


HEADER_KEYWORDS = {"SOCIETE", "GROUPE", "VILLE", "ACTIVITE", "PERSONNE",
                   "FONCTION", "TEL", "FAX", "GSM", "EMAIL", "ADRESSE"}

COLUMNS = ["SOCIETE", "GROUPE", "VILLE", "ACTIVITE", "PERSONNE",
           "FONCTION", "TEL", "FAX", "GSM", "EMAIL", "ADRESSE"]


def canon_label(tok: str) -> str:
    """Map a header label (as extracted, possibly merged/split) to a canonical
    column name, or None if it should be ignored."""
    t = tok.replace(" ", "").upper().replace("-", "").replace("_", "")
    if "SOCIETE" in t:
        return "SOCIETE"
    if "GROUPE" in t:
        return "GROUPE"
    if t == "VILLE" or t == "VILLE":
        return "VILLE"
    if t == "ACTIVITE":
        return "ACTIVITE"
    if t.startswith("PERSONNE"):
        return "PERSONNE"
    if "FONCTION" in t or t in ("SERVICE", "/SERVICE"):
        return "FONCTION"
    if t == "TEL" or t.startswith("TEL"):
        return "TEL"
    if t == "FAX":
        return "FAX"
    if t == "GSM":
        return "GSM"
    if "MAIL" in t:
        return "EMAIL"
    if "ADRESSE" in t:
        return "ADRESSE"
    return None


def cp1252(cid: int) -> str:
    """Map a WinAnsi (cp1252) byte/cid to its unicode char."""
    if cid < 128:
        return chr(cid)
    if cid == 0x20:  # space
        return " "
    if cid > 255:
        # Identity-H glyph ids are not bytes; keep as-is (rare in this doc)
        return chr(cid)
    try:
        return bytes([cid]).decode("cp1252")
    except UnicodeDecodeError:
        return "\ufffd"


def is_phone(tok: str) -> bool:
    t = tok.strip("()/+- ")
    return bool(re.fullmatch(r"\d[\d\s/]*", tok.strip())) and any(ch.isdigit() for ch in tok)


def read_chars(page):
    """Yield (baseline_y, x0, x1, char_str) for every glyph with usable geometry."""
    out = []
    for tr in page.get_texttrace():
        for c in tr.get("chars", ()) or ():
            cid = c[0]
            origin = c[2]
            if cid is None:
                continue
            ch = cp1252(cid)
            if ch.isspace() and ch != " ":
                continue
            out.append((origin[1], origin[0], ch))
    return out


def words_and_rows(chars, x_tol=2.6, y_tol=1.4):
    """Group sorted chars into rows (by baseline y), then words within each row
    (split on space glyphs or inter-char gaps > x_tol). Returns list of rows,
    each a list of (word_text, x0, x1, baseline_y)."""
    chars.sort(key=lambda t: (t[0], t[1]))

    # cluster chars into rows by baseline y
    rows = []
    curr = []
    cury = None
    for y, x, ch in chars:
        if cury is None or abs(y - cury) <= y_tol:
            cury = y if cury is None else (cury + y) / 2
            curr.append((y, x, ch))
        else:
            rows.append(curr)
            curr = [(y, x, ch)]
            cury = y
    if curr:
        rows.append(curr)

    out_rows = []
    for r in rows:
        r.sort(key=lambda t: t[1])  # within a row, sort by x
        words = []  # (text, x0)
        if not r:
            continue
        buf = [r[0][2]]
        x0 = r[0][1]
        last_x = r[0][1]
        for _, x, ch in r[1:]:
            if ch == " ":
                if buf:
                    words.append((("".join(buf)).strip(), x0))
                    buf = []
                last_x = x
                continue
            if x - last_x > x_tol:
                if buf:
                    words.append((("".join(buf)).strip(), x0))
                buf = []
            if not buf:
                x0 = x
            buf.append(ch)
            last_x = x
        if buf:
            words.append((("".join(buf)).strip(), x0))
        yavg = sum(w[0] for w in r) / len(r)
        out_rows.append([(t, x0_, x0_ + len(t) * 0.6, yavg) for t, x0_ in words])
    return out_rows


def detect_header_row(rows):
    """Return the first text row whose canonical labels cover >=5 distinct columns."""
    for r in sorted(rows, key=lambda rr: rr[0][3] if rr else 1e9):
        labels = []
        for w in r:
            c = canon_label(w[0])
            if c and not any(c == existing[0] for existing in labels):
                labels.append((c, w))
        if len(labels) >= 5:
            labels.sort(key=lambda cw: cw[1][1])
            return labels
    return None


def gridline_boundaries(page):
    """Detect column boundaries from the table's vertical separator lines.

    The PDF draws the table grid: horizontal row separators (thin full-width bars)
    and vertical column separators (thin, tall bars) with x = 15.6, 129.7, 174.5,
    206.2, 281.5, 340.4, 436.8, 504.0, 546.3, 588.0, 661.8, 805.4 for the 11 columns.
    Returns {column_name: left_x} for the 11 columns, or None if the gridline set
    is not exactly one more position than the column count."""
    xs = set()
    for d in page.get_drawings():
        r = d["rect"]
        if r.width < 0.6 and r.height > 2:
            xs.add(round(r.x0, 0))
    xs = sorted(xs)
    if len(xs) == len(COLUMNS) + 1:
        return {name: xs[i] for i, name in enumerate(COLUMNS)}
    return None


def compute_boundaries(rows):
    """Data-driven column-left boundaries.

    - find the header row -> ordered canonical labels -> reference centers
    - cluster every non-header word by its x0 into column groups; name each group by
      the header label whose center is nearest
    - boundary between neighbouring columns = midpoint of the gap between the two
      groups' data extents (max x of the left group, min x of the right group)
    Returns {column_name: left_x (0 for the first column)} or None if no header."""
    header = detect_header_row(rows)
    if header is None:
        return None
    centers = [(c, (w[1] + w[2]) / 2) for c, w in header]
    header_y = header[0][1][3]

    data_words = [w for r in rows for w in r if abs(w[3] - header_y) > 1.0]
    xs = sorted({w[1] for w in data_words})
    if not xs:
        return None

    # cluster x0s by big gaps (inter-column gaps are ~30-45pt, intra-column < ~10)
    gap = 18
    clusters = []
    cur = [xs[0]]
    for a, b in zip(xs, xs[1:]):
        if b - a <= gap:
            cur.append(b)
        else:
            clusters.append(cur)
            cur = [b]
    clusters.append(cur)
    clusters = [c for c in clusters if c]

    # name each cluster by the nearest header-label center
    named = []  # (name, left_edge, right_edge)
    for c in clusters:
        ctr = sum(c) / len(c)
        name = min(centers, key=lambda nc: abs(nc[1] - ctr))[0]
        named.append((name, min(c), max(c)))

    # boundaries between consecutive named clusters in x order
    bounds = {named[0][0]: 0}
    for i in range(1, len(named)):
        _, _, prev_right = named[i - 1]
        name, left, _ = named[i]
        if name != named[i - 1][0]:
            bounds[name] = (prev_right + left) / 2

    # fill any column that has no cluster (e.g. GROUPE when empty) by interpolation
    names_ordered = [c[0] for c in centers]
    for order_i, name in enumerate(names_ordered):
        if name in bounds:
            continue
        left_b = None
        right_b = None
        for j in range(order_i - 1, -1, -1):
            if names_ordered[j] in bounds:
                left_b = bounds[names_ordered[j]]
                break
        for j in range(order_i + 1, len(names_ordered)):
            if names_ordered[j] in bounds:
                right_b = bounds[names_ordered[j]]
                break
        if left_b is not None and right_b is not None:
            bounds[name] = (left_b + right_b) / 2
        elif left_b is not None:
            bounds[name] = left_b + 1
        elif right_b is not None:
            bounds[name] = right_b - 1
        else:
            bounds[name] = 0
    return bounds


def classify(word_x0, bounds):
    """Return column name whose [left,next_left) band contains word_x0."""
    col = None
    for name in COLUMNS:
        if word_x0 >= bounds[name]:
            col = name
    # if a word lands in the SOCIETE band but is far left, fine.
    return col


def parse_page(page):
    chars = read_chars(page)
    rows = words_and_rows(chars)
    return rows


def gridline_separators(page):
    """Return the y positions of the table's horizontal row separators."""
    ys = set()
    for d in page.get_drawings():
        r = d["rect"]
        if r.width > 300 and r.height <= 0.9:
            ys.add(round(r.y0, 1))
    return sorted(ys)


def band_index(y, seps):
    """Which row band a baseline y falls into (band 0 = below the first separator)."""
    for i, s in enumerate(seps):
        if y < s:
            return i
    return len(seps)


def build_records(rows, bounds, separators):
    """Segment words into records using the table's horizontal gridlines.

    Every Excel row (a rectangle between two horizontal separators) becomes exactly
    one record, even when its SOCIETE cell is empty (e.g. a sub-listing row that only
    carries a phone number) or when its cells wrap onto several baselines. The header
    band is dropped. Falls back to SOCIETE-anchoring if no separators were found."""
    records = []
    if separators:
        bands = {}  # band idx -> OrderedDict
        for r in rows:
            for w in r:
                b = band_index(w[3], separators)
                if b not in bands:
                    bands[b] = collections.OrderedDict((c, []) for c in COLUMNS)
                col = classify(w[1], bounds)
                if col is not None:
                    bands[b][col].append(w[0])
        for b in sorted(bands):
            rec = bands[b]
            if sum(1 for c in COLUMNS for t in rec[c] if canon_label(t.strip()) is not None) >= 3:
                continue  # header band
            records.append(rec)
    else:
        # fallback: SOCIETE-anchored grouping
        cur = None
        prev_soc_only = False
        soc_bound = bounds.get("GROUPE") or (bounds.get("VILLE") or 999)
        for r in rows:
            if sum(1 for w in r if canon_label(w[0].strip())) >= 5:
                continue
            soc_words = [w for w in r if w[1] < soc_bound]
            other_words = [w for w in r if w[1] >= soc_bound]
            only_soc = bool(soc_words) and not other_words
            if soc_words:
                if not prev_soc_only:
                    cur = collections.OrderedDict((c, []) for c in COLUMNS)
                    records.append(cur)
                prev_soc_only = only_soc
            else:
                if cur is None:
                    continue
                prev_soc_only = False
            for w in r:
                col = classify(w[1], bounds)
                if col is not None and cur is not None:
                    cur[col].append(w[0])

    out = []
    for rec in records:
        row = {c: " ".join(toks).strip() for c, toks in rec.items()}
        # emails never contain internal spaces; the PDF wraps them with space glyphs
        row["EMAIL"] = "".join(rec["EMAIL"]).replace(" ", "")
        out.append(row)
    return out


def extract(pdf_path, out_path=None):
    import pymupdf

    doc = pymupdf.open(pdf_path)
    all_records = []
    per_page = {}

    # Determine which pages have text
    text_pages = [i for i in range(doc.page_count) if doc[i].get_text("words")]
    for i in text_pages:
        page = doc[i]
        rows = parse_page(page)
        bounds = gridline_boundaries(page) or compute_boundaries(rows) or (last_bounds if "last_bounds" in dir() else None)
        if bounds is None:
            print(f"page {i}: could not detect header columns; skipping", file=sys.stderr)
            continue
        seps = gridline_separators(page)
        recs = build_records(rows, bounds, seps)
        per_page[i] = recs

        all_records.extend(recs)
        last_bounds = bounds

    return all_records, text_pages, per_page


def write_csv(records, out_path):
    import csv

    with open(out_path, "w", newline="", encoding="utf-8-sig") as f:
        w = csv.writer(f)
        w.writerow(COLUMNS)
        for r in records:
            w.writerow([r[c] for c in COLUMNS])
    print(f"Wrote {len(records)} rows -> {out_path}")


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("pdf", nargs="?", default=os.path.join("docs", "datasets", "Email des RH pour ceux qui seront a la recherche des stages de fin d'etude .pdf"))
    ap.add_argument("out", nargs="?", default=os.path.join("docs", "datasets", "contacts_export.csv"))
    ap.add_argument("--dump", action="store_true", help="print page rows instead of writing csv")
    args = ap.parse_args()

    if args.dump:
        import pymupdf
        doc = pymupdf.open(args.pdf)
        while True:
            try:
                idx = int(input("page index (enter for next page, q to quit): ") or -1)
            except (EOFError, ValueError):
                break
            if idx < 0 or idx >= doc.page_count:
                break
            rows = parse_page(doc[idx])
            bounds = gridline_boundaries(doc[idx]) or compute_boundaries(rows)
            header = detect_header_row(rows)
            print(f"page {idx}: {len(rows)} rows; header words: {[w[0] for w in header] if header else 'NONE'}")
            if bounds:
                print("column left boundaries:", {k: round(v, 1) for k, v in bounds.items()})
            for r in rows:
                cells = sorted((classify(w[1], bounds) if bounds else None, w[0], round(w[1], 1)) for w in r)
                print("   ", " | ".join(f"{c}:{t}(x{x:.0f})" for c, t, x in cells))
        return

    records, text_pages, per_page = extract(args.pdf)
    total = 0
    for p, recs in per_page.items():
        print(f"page {p}: {len(recs)} records")
        total += len(recs)
    print(f"total records: {total}")

    write_csv(records, args.out)


if __name__ == "__main__":
    main()