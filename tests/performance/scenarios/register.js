import http from 'k6/http';
import { check } from 'k6';
import { Counter, Rate } from 'k6/metrics';
import { config } from '../config/env.js';
import { buildUser, jsonHeaders } from '../lib/helpers.js';

// ── Test plan ────────────────────────────────────────────────────────────────
// Goal: exercise POST /api/auth/register with 10 concurrent virtual users.
// Each VU performs exactly one registration → 10 distinct accounts created.
// We assert the response is 200 and that the body says { success: true }.
//
// Run with:
//   k6 run tests/performance/scenarios/register.js
//   k6 run -e BASE_URL=http://localhost:8080 tests/performance/scenarios/register.js
//
// What gets reported:
//   - http_req_duration       (p95 / p99 latency of the register call)
//   - register_success_rate   (custom: % of 2xx + success:true responses)
//   - register_conflicts      (custom: # of 409 already-exists responses)
// ─────────────────────────────────────────────────────────────────────────────

const successRate = new Rate('register_success_rate');
const conflictCount = new Counter('register_conflicts');
const serverErrorCount = new Counter('register_server_errors');

export const options = {
  // 10 virtual users, each runs the default function once → 10 total requests
  vus: 10,
  iterations: 10,

  // Thresholds turn the run red if performance regresses.
  thresholds: {
    'http_req_failed': ['rate<0.10'],            // < 10% of HTTP calls fail
    'http_req_duration': ['p(95)<10000'],        // local Keycloak user-creation ~7s; 10s gives headroom
    'register_success_rate': ['rate>0.90'],      // ≥ 90% logical success
  },
};

// setup() runs ONCE before any VU starts. We capture a single timestamp here
// and share it with every VU so all generated emails are unique to this run
// but predictable enough to debug from the logs.
export function setup() {
  const runId = Date.now().toString(36);
  console.log(`[register-load] run id = ${runId} → target = ${config.endpoints.register}`);
  return { runId };
}

export default function (data) {
  const user = buildUser(data.runId, __VU, __ITER);

  const res = http.post(
    config.endpoints.register,
    JSON.stringify(user),
    {
      headers: jsonHeaders,
      tags: { name: 'register' },
    },
  );

  let body = {};
  try {
    body = res.json();
  } catch (_) {
    // non-JSON body (e.g., 502 from gateway) — keep body empty for checks
  }

  const ok = check(res, {
    'status is 200': (r) => r.status === 200,
    'response has success=true': () => body && body.success === true,
    'message confirms account created': () => body && typeof body.message === 'string' && body.message.length > 0,
  });

  successRate.add(ok);
  if (res.status === 409) conflictCount.add(1);
  if (res.status >= 500) serverErrorCount.add(1);

  if (!ok) {
    console.warn(`[register-load] VU=${__VU} iter=${__ITER} status=${res.status} body=${res.body}`);
  }
}

export function teardown(data) {
  console.log(`[register-load] finished run id = ${data.runId}`);
}
