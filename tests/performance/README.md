# Performance tests (k6)

Load and performance tests targeting the **API gateway** (`http://localhost:8080`).
These tests are deliberately separated from the Angular E2E suite (`frontend/cypress/`)
because they exercise the **backend contract**, not the UI.

## Layout

```
tests/performance/
├── config/
│   └── env.js              # base URL + endpoint map (overridable via -e BASE_URL=...)
├── lib/
│   └── helpers.js          # unique-email builder, shared headers
├── scenarios/
│   └── register.js         # POST /api/auth/register, 10 VUs × 1 iteration
└── README.md
```

## Prerequisites

1. **k6** installed (`winget install GrafanaLabs.k6` or `choco install k6`).
   Verify with `k6 version`.
2. The stack must be running locally (`.\dev.ps1` from the repo root), so the
   gateway is reachable on `http://localhost:8080`.

## Running

```powershell
# From repo root, with default BASE_URL = http://localhost:8080
k6 run tests/performance/scenarios/register.js

# Against a different environment
k6 run -e BASE_URL=https://staging.cv-generator.example tests/performance/scenarios/register.js
```

## What each scenario does

### `scenarios/register.js`

- 10 virtual users (`vus: 10`), each runs `default` once (`iterations: 10`) →
  **10 distinct register requests in parallel**.
- Generates a unique email per VU using a per-run timestamp + `__VU` + `__ITER`,
  so the test is fully **repeatable** (different emails every run, no 409
  collisions with previous runs).
- Asserts: HTTP 200, `success: true` in the body, non-empty message.
- Thresholds (red build on regression):
  - `http_req_failed` < 10 %
  - `http_req_duration p(95)` < 2 s
  - `register_success_rate` > 90 %
- Custom counters: `register_conflicts` (409s), `register_server_errors` (5xx).

## Reading the output

After the run k6 prints a summary like:

```
checks.........................: 100.00% ✓ 30  ✗ 0
http_req_duration..............: avg=312ms p(95)=781ms
register_success_rate..........: 100.00% ✓ 10  ✗ 0
register_conflicts.............: 0
```

Failed thresholds appear with a ✗ and cause `k6 run` to exit with code 99.
