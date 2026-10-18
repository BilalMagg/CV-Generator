using System.Text.RegularExpressions;
using CV_Generator.Dto;

namespace CV_Generator.Services.BulkExtract;

/// <summary>
/// Deterministic parser for the common paste format:
/// <code>
/// 1. Capgemini Morocco — Casablanca
///    Email : recrutement@capgemini.com
///    Spécialités : Data Science, Software Engineering
/// </code>
/// Falls back to LLM extraction only when this yields nothing (see the controller).
/// </summary>
public static class BulkTextParser
{
    private static readonly Regex NumberedHeader = new(@"^\s*\d+\s*[.)\-]\s*(.+)$", RegexOptions.Compiled);
    private static readonly Regex EmailLike = new(@"[\w.+-]+@[\w-]+\.[\w.-]+", RegexOptions.Compiled);
    private static readonly Regex EmailLabel = new(@"^\s*(e[\-\s]?mail|mail|email)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SpecialtyLabel = new(@"^\s*(sp[ée]?cialit|specialt|domaine|secteur|p[ôo]le|area|field)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex HeaderSplit = new(@"\s*[—–]\s*|\s+-\s+", RegexOptions.Compiled);

    private sealed class Block
    {
        public string Company = "";
        public string? Location;
        public readonly List<string> Emails = [];
        public readonly List<string> Specialties = [];
    }

    public static List<BulkExtractRowDto> Parse(string? text)
    {
        var rows = new List<BulkExtractRowDto>();
        if (string.IsNullOrWhiteSpace(text)) return rows;

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var blocks = new List<Block>();
        Block? current = null;

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            var emailsInLine = EmailLike.Matches(line).Select(m => m.Value).ToList();

            if (emailsInLine.Count > 0 && (EmailLabel.IsMatch(line) || emailsInLine.Any(e => e == line)))
            {
                current ??= NewBlock("");
                foreach (var e in emailsInLine) AddEmail(current, e);
                continue;
            }

            if (SpecialtyLabel.IsMatch(line))
            {
                current ??= NewBlock("");
                foreach (var s in SplitSpecialties(line)) AddSpecialty(current, s);
                continue;
            }

            // Otherwise this starts a new company block.
            current = ParseHeader(line);
            blocks.Add(current);
        }

        foreach (var block in blocks)
        {
            if (string.IsNullOrWhiteSpace(block.Company) && block.Emails.Count == 0) continue;

            var specialties = block.Specialties;
            var targets = block.Emails.Count > 0 ? block.Emails : [""];
            foreach (var email in targets)
            {
                rows.Add(new BulkExtractRowDto
                {
                    CompanyName = block.Company,
                    Location = block.Location,
                    Email = string.IsNullOrWhiteSpace(email) ? null : email,
                    ContactName = null,
                    Specialties = [.. specialties]
                });
            }
        }

        // Drop rows that carry neither a company nor any contactable detail.
        return rows
            .Where(r => !string.IsNullOrWhiteSpace(r.CompanyName))
            .ToList();
    }

    private static Block NewBlock(string company) => new() { Company = company };

    private static Block ParseHeader(string line)
    {
        var header = line;
        var m = NumberedHeader.Match(header);
        if (m.Success) header = m.Groups[1].Value.Trim();

        var block = NewBlock("");
        var segments = HeaderSplit.Split(header).Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        if (segments.Count == 0) return block;

        block.Company = StripTrailing(segments[0]);

        for (var i = 1; i < segments.Count; i++)
        {
            var seg = segments[i];
            var email = EmailLike.Match(seg);
            if (email.Success)
            {
                AddEmail(block, email.Value);
                continue;
            }
            if (block.Location is null && !SpecialtyLabel.IsMatch(seg))
            {
                block.Location = StripTrailing(seg);
            }
            else
            {
                foreach (var s in SplitSpecialties(seg)) AddSpecialty(block, s);
            }
        }

        // A header may itself carry an inline email even before a separator.
        if (EmailLike.IsMatch(block.Company))
        {
            foreach (Match e in EmailLike.Matches(block.Company)) AddEmail(block, e.Value);
            block.Company = StripTrailing(EmailLike.Replace(block.Company, "").Trim());
        }

        return block;
    }

    private static IEnumerable<string> SplitSpecialties(string line)
    {
        var value = SpecialtyLabel.Replace(line, "");
        value = value.TrimStart(':', '-', '—', '–', ' ').Trim();
        return value
            .Split([',', ';', '/', '|', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => StripTrailing(s.Trim()))
            .Where(s => s.Length > 0);
    }

    private static void AddEmail(Block block, string email)
    {
        var e = email.Trim().Trim(',', ';', '.');
        if (e.Length > 0 && !block.Emails.Any(x => x.Equals(e, StringComparison.OrdinalIgnoreCase)))
            block.Emails.Add(e);
    }

    private static void AddSpecialty(Block block, string specialty)
    {
        var s = specialty.Trim();
        if (s.Length > 0 && !block.Specialties.Any(x => x.Equals(s, StringComparison.OrdinalIgnoreCase)))
            block.Specialties.Add(s);
    }

    private static string StripTrailing(string value) => value.Trim().TrimEnd(',', ';', '.', ':', '-', '—', '–').Trim();
}
