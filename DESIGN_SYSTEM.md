# Driftly — Design System

> **App Name:** Driftly  
> **Stack:** Angular 21 · SCSS · OKLCH color space  
> **Last updated:** 2026-05-22

---

## 1. Brand & Identity

| Token | Value |
|---|---|
| App name | **Driftly** |
| Tagline | *Tailor a CV in eleven seconds.* |
| Logo mark | Star/sparkle SVG icon on dark background |
| Plan badge | `Pro` — small pill next to brand name in sidebar |

---

## 2. Color Tokens

All colors use the **OKLCH** color space (perceptually uniform, wide-gamut-ready). Defined in `:root` inside `frontend/src/styles.scss`.

### Surfaces & Backgrounds

| Token | Value | Usage |
|---|---|---|
| `--bg` | `oklch(0.985 0.003 80)` | App background (off-white warm) |
| `--bg-2` | `oklch(0.97 0.004 80)` | Slightly darker bg for nested areas |
| `--surface` | `oklch(1 0 0)` | Card / panel surfaces (pure white) |
| `--surface-2` | `oklch(0.985 0.003 80)` | Secondary surface |
| `--border` | `oklch(0.9 0.005 80)` | Default border color |
| `--border-2` | `oklch(0.94 0.004 80)` | Lighter border variant |
| `--hairline` | `oklch(0.93 0.005 80)` | Divider lines, subtle rules |

### Text

| Token | Value | Usage |
|---|---|---|
| `--text` | `oklch(0.22 0.01 80)` | Primary text (near-black) |
| `--text-2` | `oklch(0.42 0.01 80)` | Secondary text (dark gray) |
| `--text-3` | `oklch(0.58 0.008 80)` | Tertiary / placeholder |
| `--text-4` | `oklch(0.72 0.006 80)` | Disabled / lightest |

### Accent (Indigo Blue)

| Token | Value | Usage |
|---|---|---|
| `--accent` | `oklch(0.6 0.16 250)` | Links, active nav, primary interactive |
| `--accent-2` | `oklch(0.55 0.17 250)` | Darker accent for hover states |
| `--accent-soft` | `oklch(0.94 0.04 250)` | Tinted bg for accent areas |
| `--accent-text` | `oklch(0.42 0.16 250)` | Text on accent-soft backgrounds |

### Dark CTA

| Token | Value | Usage |
|---|---|---|
| `--dark` | `oklch(0.18 0.01 80)` | Primary CTA buttons, dark banner bg |

### Application Status Colors

| Status | Dot color | Background | Text | Hue |
|---|---|---|---|---|
| Applied | `oklch(0.68 0.015 250)` | `oklch(0.96 0.008 250)` | `oklch(0.42 0.02 250)` | Blue-gray |
| Interview | `oklch(0.6 0.16 250)` | `oklch(0.95 0.04 250)` | `oklch(0.42 0.16 250)` | Indigo |
| Offer | `oklch(0.62 0.15 155)` | `oklch(0.95 0.04 155)` | `oklch(0.42 0.12 155)` | Green |
| Rejected | `oklch(0.62 0.18 25)` | `oklch(0.95 0.04 25)` | `oklch(0.45 0.16 25)` | Red |
| Saved | `oklch(0.7 0.12 75)` | `oklch(0.96 0.04 75)` | `oklch(0.45 0.12 75)` | Yellow |

### Dashboard Stat Card Colors (from screenshots)

| Stat | Highlight color |
|---|---|
| Total applications | Black / `--dark` |
| Interviewing | Blue / `--accent` |
| Offers | Green / `--st-offer` |
| Response rate | Red / `--st-rejected` |

---

## 3. Typography

### Font Families

| Token | Stack | Usage |
|---|---|---|
| `--font-body` | `'Geist', 'Plus Jakarta Sans', ui-sans-serif` | Body text, UI labels |
| `--font-display` | `'Plus Jakarta Sans', 'Geist', ui-sans-serif` | Headings, display text |
| `--font-mono` | `'Geist Mono', ui-monospace` | Code, monospaced values |

Both Geist and Plus Jakarta Sans are loaded via Google Fonts.

### Type Scale

| Role | Size | Weight | Font | Letter-spacing |
|---|---|---|---|---|
| Display / hero | `36–48px` | 800 | display | `-0.03em` |
| Page title | `28–32px` | 700–800 | display | `-0.025em` |
| Section heading | `18–20px` | 700 | display | `-0.02em` |
| Card title | `14–15px` | 600 | body | `-0.01em` |
| Body / label | `14px` | 400–500 | body | `0` |
| Caption / meta | `12px` | 400 | body | `0` |
| Monospace value | `13–14px` | 500 | mono | `0` |

### Heading defaults

```scss
h1, h2, h3, h4, .h-display, .display {
  font-family: var(--font-display);
  letter-spacing: -0.025em;
}
```

---

## 4. Spacing & Sizing

| Token | Value | Usage |
|---|---|---|
| `--sb-w` | `224px` | Sidebar width |
| `--header-h` | `52px` | App header height |
| `--radius` | `10px` | Default card/button radius |
| `--radius-sm` | `7px` | Small elements (badges, inputs) |
| `--radius-lg` | `14px` | Large cards, modals |

### Shadows

| Token | Value |
|---|---|
| `--shadow-card` | `0 1px 0 oklch(0.95 0.004 80), 0 1px 2px oklch(0.9 0.005 80 / .3)` |
| `--shadow-pop` | `0 1px 0 oklch(0.95 0.004 80), 0 8px 24px oklch(0.6 0.01 80 / .12)` |

---

## 5. Layout System

### Authenticated App Shell

```
┌─────────────────────────────────────────────────────────────┐
│  HEADER  (52px tall, spans full width above main)           │
├──────────┬──────────────────────────────────────────────────┤
│          │                                                  │
│ SIDEBAR  │              MAIN CONTENT                        │
│ (224px)  │         (flex: 1, overflow-y: auto)             │
│          │                                                  │
└──────────┴──────────────────────────────────────────────────┘
```

**CSS Grid approach:**
```scss
.app {
  display: grid;
  grid-template-columns: var(--sb-w) 1fr;
  height: 100vh;
  overflow: hidden;
}

.main {
  display: flex;
  flex-direction: column;
  min-width: 0;
  background: var(--bg);
}

.stage {
  flex: 1;
  overflow: auto;
  min-height: 0;
}
```

### Page Content Padding

Main content pages use `padding: 40px 48px` (or similar) for inner content breathing room.

---

## 6. Navigation

### Sidebar Structure (top → bottom)

1. **Brand row** — Logo icon + "Driftly" name + "Pro" badge + chevron dropdown
2. **Search bar** — Full-width button, magnifier icon, placeholder "Search…", `⌘K` keycap badge
3. **Main nav** — Vertical list of navigation items
4. **LIBRARY eyebrow** — Section label in small caps
5. **Library nav** — My CV link
6. **Spacer** — Pushes bottom section down
7. **Promo / credits card** — "24 credits left this month" with upgrade CTA
8. **User row** — Avatar initials, name, email, settings icon

### Navigation Items

| Label | Icon | Route | Special |
|---|---|---|---|
| Dashboard | Grid/squares | `/applications/dashboard` | — |
| Generate CV | Sparkle | `/applications/generate` | AI badge (indigo), accent style |
| Applications | Kanban bars | `/applications/kanban` | — |
| Analytics | Bar chart | `/applications/analytics` | — |
| Calendar | Calendar grid | `/applications/calendar` | — |
| My CV | Person/user | `/my-cv` | Under LIBRARY section |

### Nav Item States

- **Default:** `--text-2` color, transparent background
- **Hover:** light gray background (`--bg-2`)
- **Active:** `--text` color, darker background, left accent or text color change
- **Accent (Generate CV):** indigo `--accent` color text, `--accent-soft` background when active

### Header Structure

Left: Breadcrumb — `Driftly > Page Name` (simple text path)  
Right: `⌘ Help` · Bell icon (notification dot) · `● Signed in` status pill

---

## 7. Component Library

### Buttons

| Class | Appearance | Usage |
|---|---|---|
| `.btn` | Transparent, hover: light bg | Ghost action buttons |
| `.btn-primary` | Dark (`--dark`) fill, rounded, inset shadow | Primary CTA (e.g. "Generate CV") |
| `.btn-secondary` | White with border | Secondary actions |
| `.btn-accent` | Dark fill | Alternate CTA |
| `.btn-blue` | Indigo fill | Accent actions |
| `.btn-ghost` | Text only | Minimal actions |
| `.btn-sm` / `.btn-lg` | Size modifier | — |

**Pill variants:** Same as above but `border-radius: 999px` (fully rounded):  
`.pill-btn`, `.pill-primary`, `.pill-secondary`, `.pill-blue`, `.pill-ghost`, `.pill-sm`, `.pill-lg`

### Cards

| Class | Appearance |
|---|---|
| `.card` | White surface (`--surface`), `1px --border` border, `--radius`, `--shadow-card` |
| `.soft-card` | Similar but higher radius (`--radius-lg`), softer shadow |
| `.cream-section` | Warm beige (`--bg-2`) background section |

### Status Badges / Pills

```scss
.pill { border-radius: var(--radius-sm); padding: 2px 8px; font-size: 12px; font-weight: 500; }
.st-applied   { background: var(--st-applied-bg);   color: var(--st-applied-text); }
.st-interview { background: var(--st-interview-bg); color: var(--st-interview-text); }
.st-offer     { background: var(--st-offer-bg);     color: var(--st-offer-text); }
.st-rejected  { background: var(--st-rejected-bg);  color: var(--st-rejected-text); }
.st-saved     { background: var(--st-saved-bg);     color: var(--st-saved-text); }
```

`.pill-dot` — adds a colored dot before the label.

### Form Controls

| Class | Element | Notes |
|---|---|---|
| `.input` | `<input>` | Bordered, focus ring in `--accent` |
| `.textarea` | `<textarea>` | Same as input |
| `.select` | `<select>` | With custom chevron |

### Stat Cards (Dashboard)

Structure:
```
┌──────────────────────────────────┐
│ ● Label                  +12%   │
│                                  │
│  24  vs last month               │
│                                  │
│  [mini bar chart]                │
└──────────────────────────────────┘
```

- Dot color matches the stat category (black/blue/green/red)
- Change badge: green background for positive, red for negative
- Large number in `--font-display`, bold
- Mini sparkline bar chart at bottom

### Tone Selector (Generate CV)

Horizontal group of pill-shaped buttons. Selected state = dark fill (`--dark`), others = outlined.

```
[ Confident ] [ Warm ] [ Technical ] [ Concise ]
```

### Kanban Cards (Applications)

```
┌─────────────────────────────────┐
│ [Logo] CompanyName  Location    │
│        Job Title                │
│                                 │
│  $XXk salary         3d ago     │
└─────────────────────────────────┘
```

- Company initials avatar with color-coded background
- Role title in medium weight
- Salary as `$XXk` with money icon
- Timestamp in `--text-4`
- Hover: slight shadow lift

### View Toggle (Board / List / Calendar)

Pill-button group:
```
[ Board ] [ List ] [ Calendar ] | [ Filter ] [ Add ]
```
Active tab has dark/solid background. Right side has action buttons.

---

## 8. Pages

### Dashboard (`/applications/dashboard`)

**Header section:**
- Date pill: e.g. `Sunday · May 19` — small outlined pill
- Greeting: `Good afternoon, [FirstName]` — display font, large, name in `--accent` blue
- Subtext: "You have N follow-ups this week and N active offers."
- Top-right actions: `+ Add application` button + `Generate CV` primary CTA

**Stats row (4 cards):**
| Card | Color | Metric |
|---|---|---|
| Total applications | Black dot | Count + `vs last month` |
| Interviewing | Blue dot | Count + `N active` |
| Offers | Green dot | Count + `N pending` |
| Response rate | Red dot | Percentage + `industry avg X%` |

**Dark CTA banner:**
- Near-black background (`--dark`)
- Credits indicator: `⚡ 24 credits left this month`
- Heading: `Got a new role you're chasing?`
- Subtext: `Paste a job description. Get a tailored CV in 11 seconds.`
- CTA: outlined `Generate a CV →` button

**Lower section (2-column):**
- Left: `Your pipeline` — Applications by status chart (legend: Applied/Interview/Offer/Rejected dots), last 6 months subtitle
- Right: `Follow-ups` — What's next this week, with `See calendar →` link, and list of upcoming events with company avatar + title + time ago

---

### Generate CV (`/applications/generate`)

**Layout:** Breadcrumb header + full-width content with left/right panels

**Left panel (wider):**
- Section heading: `Paste the job description`
- Subtext: `From any company. We'll handle the rest.`
- Helper buttons: `Use sample` | `Paste URL`
- Large textarea: `Paste a job description here…`

**Right panel (fixed width ~360px):**
- Card 1: **Generation settings**
  - `CV profile` label + dropdown (e.g. "Senior Product Designer · Product · Web")
  - Caption: `The base we'll tailor to this role.`
  - `Tone` label + 4-way selector: Confident / Warm / Technical / Concise
  - Checkbox: `Email me the finished CV & cover letter`
  - Caption: `Sent to [email] as PDF + DOCX.`
- Card 2: **You'll get**
  - Checklist: Tailored CV (PDF + DOCX), Cover letter, ATS score, etc.

**Page header:**
- AI badge pill: `✦ AI CV Generator`
- H1: `Tailor a CV in eleven seconds.`
- Subtext: "Paste any job description. We'll match it against your portfolio and write a CV that scores 95%+ on ATS parsers."
- Top-right: `History` button

---

### Applications — Kanban (`/applications/kanban`)

**Page header:**
- H1: `Move applications across stages`
- Subtext: `21 active applications · Drag a card between columns to update status.`
- View toggle: Board | List | Calendar | Filter | Add

**Kanban board:**
- 4 columns: Applied, Interview, Offer, Rejected
- Each column header: status dot + label + count
- Cards are draggable between columns (Angular CDK DragDrop)
- Column `+` button to add new card

**Card design:**
- Company color avatar (2-letter initials, background color based on company name hash)
- Company name + location (small text)
- Job title (medium weight)
- Salary tag + time ago timestamp

---

### Analytics (`/applications/analytics`)

**Page header:**
- Breadcrumb: `Driftly > Analytics`
- Badge pill: `Analytics`
- H1: `Your search, by the numbers`
- Subtext: `See what's working, where you're getting traction, and where to push harder.`
- Top-right: `Last 6 months` dropdown + `↓ Report` button

**Stats row (4 cards):**
| Metric | Color | Change |
|---|---|---|
| Response rate | Blue | `+8%` green badge |
| Interview rate | Red | `+5%` green badge |
| Offer rate | Green | `+2%` green badge |
| Avg. response time | Purple | `-1.2d` red badge |

**Charts section (2-column):**
- Left: `Status distribution` — Donut chart with center count + legend (Applied/Interview/Offer/Rejected with %)
- Right: `Applications per month` — Stacked bar chart by status

---

### Calendar (`/applications/calendar`)

**Page header:**
- Badge pill: `Schedule`
- H1: `May 2026` (current month/year)
- Subtext: `Interviews, follow-ups, and offer deadlines — never miss a beat.`
- View toggle: Board | List | **Calendar** (active) | Today | `< >`

**Main area (2-column):**
- Left: Monthly calendar grid (Mon–Sun, 5 rows)
  - Date cells show events as colored pills (color = company/status)
  - Today highlighted with dark circle
- Right: `Upcoming` panel — vertical list of events
  - Each event: Date chip (`MAY 19`) + event type (Offer decision/Interview/Follow-up) + company · role + chevron

---

### My CV (`/my-cv`)

**Left sub-sidebar (within main content):**
- User avatar (initials) + name + role
- Sections: Projects · Experience · Education · Skills · Certifications · Languages · Social Links · Personal Info · CV profiles · Saved resumes

**Main content:**
- H2: `My CV`
- Subtitle: `Your canonical portfolio. Edit once, every tailored CV picks it up.`
- **Projects** section:
  - Cards: Project name + year + description + tags + time since
  - `+ Add project` card (dashed border)

---

## 9. Animation & Motion

| Class | Effect |
|---|---|
| `.reveal` | Fade + slide up on scroll (IntersectionObserver) |
| `.reveal-delay-1` to `.reveal-delay-4` | Staggered 75ms delay increments |
| `.anim-up` | Simple `translateY(-6px)` fade-up |

Transitions: `150ms ease` for hover states, `200ms ease` for panel transitions.

---

## 10. Design Principles

1. **Warm neutral palette** — Off-white backgrounds, not pure white, to reduce eye strain.
2. **OKLCH colors** — All colors use perceptually uniform OKLCH for consistent visual weight.
3. **Dense but breathable** — 14px base font, compact nav, but generous page-level padding.
4. **Status-coded throughout** — Job pipeline statuses have a consistent color language (blue/indigo/green/red/yellow).
5. **Typography-led hierarchy** — Large display headings with Plus Jakarta Sans, backed by Geist for body.
6. **Dark CTAs** — Primary conversion actions always use the `--dark` token (near-black), never blue.
7. **AI actions distinctly marked** — Generate CV uses sparkle icon and indigo accent to signal AI capability.
