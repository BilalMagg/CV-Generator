# Performance tests (k6)

Load and performance tests targeting the **API gateway** (`http://localhost:8080`).
These tests are deliberately separated from the Angular E2E suite (`frontend/cypress/`)
because they exercise the **backend contract**, not the UI.

## Layout

```
tests/performance/
├── config/
│   └── env.js                       # base URL + endpoint map + SEEDED_USER
├── lib/
│   ├── helpers.js                   # unique-email builder, shared headers
│   └── auth.js                      # loginAs(email, password) for auth'd scenarios
├── scenarios/
│   ├── register.js                  # POST /api/auth/register
│   ├── login.js                     # POST /api/auth/login (seeded user)
│   ├── me.js                        # GET  /api/auth/me (cookie-authed)
│   ├── applications-list.js         # GET  /api/applications with random filters
│   ├── cvprofiles-create.js         # POST /api/user-content/cvprofiles
│   └── journey.js                   # full register → login → me → cvprofile → list flow
└── README.md
```

## Prerequisites

1. **k6** installed (`winget install GrafanaLabs.k6` or `choco install k6`).
   Verify with `k6 version`.
2. **Start the TEST stack, not the dev stack** — k6 will happily write to whatever
   gateway is on `http://localhost:8080`, so make sure the test gateway is the
   one running:
   ```powershell
   .\dev.ps1 -Stop          # if dev was running
   .\dev.test.ps1           # spins up infra + wipes test DBs + starts services
                            #   pointed at *_test_db and cv-realm-test realm
   ```
   See `tests/scripts/README.md` for the full test-stack workflow.

## Running

```powershell
# From repo root, with default BASE_URL = http://localhost:8080
k6 run tests/performance/scenarios/register.js
k6 run tests/performance/scenarios/login.js
k6 run tests/performance/scenarios/me.js
k6 run tests/performance/scenarios/applications-list.js
k6 run tests/performance/scenarios/cvprofiles-create.js
k6 run tests/performance/scenarios/journey.js

# Against a different environment
k6 run -e BASE_URL=https://staging.cv-generator.example tests/performance/scenarios/register.js
```

## Scenarios at a glance

All run against the test stack (`.\dev.test.ps1`) at `http://localhost:8080`.
The seeded `testuser` / `test1234` from `tests/scripts/cv-realm-test-realm.json`
is shared by every auth-protected scenario.

| Scenario | VUs × iter | What it hits | Key threshold | What it's looking for |
|---|---|---|---|---|
| `register.js`         | 10 × 10  | `POST /api/auth/register` (unique email per iter)        | `p(95) < 2000ms` | Keycloak admin create + user-service Kafka publish |
| `login.js`            | 10 × 50 (50 total) | `POST /api/auth/login` (seeded user)            | `p(95) < 3000ms` | Keycloak token endpoint throughput |
| `me.js`               | 10 × 20 (200 total)| `GET /api/auth/me` (after one login per VU)     | `p(95) < 500ms`  | Cookie + claim validation hot path |
| `applications-list.js`| 10 × 10  | `GET /api/applications?...` with random filter combos    | `p(95) < 1500ms` | Most complex .NET SQL filter — surfaces missing indexes |
| `cvprofiles-create.js`| 10 × 5   | `POST /api/user-content/cvprofiles`                      | `p(95) < 2000ms` | Postgres write + Kafka publish (compare vs. register p95) |
| `journey.js`          | 10 × 1   | register → login → me → cvprofile → list (per VU)        | overall `>90%`   | Integration cliffs single-endpoint tests miss |

## How auth works in scenarios

The gateway's `POST /api/auth/login` returns the `accessToken` in the body
**and** sets a session cookie. All gateway routes are cookie-authenticated
(YARP transforms the cookie into a `Bearer` header internally before
forwarding to the upstream service).

k6 maintains a **per-VU cookie jar** automatically. So in scenarios:

1. The VU calls `loginAs(...)` from `lib/auth.js` exactly once.
2. Every subsequent `http.get/post(...)` in that VU re-sends the cookie.
3. No manual `Authorization` header handling.

Module-level state (like `let loggedIn = false`) is **per-VU** because each
VU runs in its own JS VM. That's how the auth-protected scenarios cheaply
log in just on the first iteration.

## Reading the output

After the run k6 prints a summary like:

```
checks.........................: 100.00% ✓ 30  ✗ 0
http_req_duration..............: avg=312ms p(95)=781ms
register_success_rate..........: 100.00% ✓ 10  ✗ 0
register_conflicts.............: 0
```

Failed thresholds appear with a ✗ and cause `k6 run` to exit with code 99.
