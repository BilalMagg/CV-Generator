# Test stack scripts

These scripts give you a **clean, isolated database environment for E2E and
performance tests** that never touches your dev databases or your `cv-realm`
Keycloak realm.

## How isolation works

Same containers as dev (Postgres x7, Keycloak, Kafka, MinIO), but:

| Resource | Dev | Test |
|---|---|---|
| Postgres DBs | `user_db`, `content_db`, ... | `user_test_db`, `content_test_db`, ... |
| Keycloak realm | `cv-realm` | `cv-realm-test` |
| Gateway port | 8080 | 8080 (**same** — only one stack runs at a time) |
| Service ports | 5001-5007 | 5001-5007 (same) |

Because the host ports are shared, **dev and test cannot run simultaneously**.
Switching modes means stopping the other launcher first.

## Files in this folder

- `reset-test-data.ps1` — drops + recreates the 7 `*_test_db` Postgres databases
  inside the existing containers, then deletes + re-imports the `cv-realm-test`
  realm via the Keycloak admin API. Idempotent.

## Common workflows

### Switch from dev to test

```powershell
.\dev.ps1 -Stop          # release ports 8080, 5001-5007
.\dev.test.ps1           # starts infra (or reuses), wipes test data, boots services
```

When the launcher finishes you'll see a yellow `CV-Generator TEST stack is
running` banner and a `.test-mode` sentinel file at the repo root.

### Run the tests

```powershell
# k6 perf — register endpoint, 10 VUs
k6 run tests/performance/scenarios/register.js

# Cypress E2E
cd frontend
npx cypress run         # refuses to start if .test-mode is missing
```

### Switch back to dev

```powershell
.\dev.test.ps1 -Stop     # kills test services, removes sentinel
.\dev.ps1                # back to normal dev DBs + cv-realm
```

### Wipe test data without restarting services

If your services are already running but you want a clean slate (for example
between two manual Cypress runs):

```powershell
.\tests\scripts\reset-test-data.ps1
```

You may need to restart the .NET services afterwards so EF Core re-runs
migrations against the freshly empty DBs — easiest path is to just relaunch
`.\dev.test.ps1`.

### Reset only Postgres or only Keycloak

```powershell
.\tests\scripts\reset-test-data.ps1 -SkipKeycloak
.\tests\scripts\reset-test-data.ps1 -SkipPostgres
```

## Known limitations

- **Kafka topics are shared** between dev and test (the topics live in the
  same broker volume). Events you publish in test mode sit in the same
  `user.registered` topic that dev consumers read. Since the two modes never
  run at the same time, the only consequence is that leftover test events
  may be consumed by dev consumers on next dev startup. If this becomes a
  problem, prefix topic names with a `KAFKA_TOPIC_PREFIX` env var and add it
  to the producer/consumer code.
- **The `.test-mode` sentinel is advisory.** Cypress checks it (and refuses
  to run if missing). k6 does not — be careful not to run k6 against dev.
- **The seeded `testuser` from the realm JSON exists in `cv-realm-test`.**
  Login fixtures in `frontend/cypress/fixtures/users.json` should match either
  this seeded user or whatever you register during your tests.
