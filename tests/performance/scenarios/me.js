import http from 'k6/http';
import { check } from 'k6';
import { Rate } from 'k6/metrics';
import { config, SEEDED_USER } from '../config/env.js';
import { loginAs } from '../lib/auth.js';

// ── Test plan ───────────────────────────────────────────────────────────────
// Goal: measure GET /api/auth/me throughput. This is the cookie-validation
// hot path -- every authenticated request the frontend makes goes through
// the same gateway middleware that AuthenticateAsync uses here.
//
// Each VU logs in ONCE on its first iteration (k6 maintains a per-VU cookie
// jar, so the session cookie persists across iterations within the same VU).
// Then 20 sequential /me hits per VU x 10 VUs = 200 validations.
//
// Run:
//   k6 run tests/performance/scenarios/me.js
//
// Threshold: p95 < 500ms. /me is local cookie+claim work, no Keycloak call.
// ────────────────────────────────────────────────────────────────────────────

const meSuccess = new Rate('me_success_rate');

export const options = {
  vus: 10,
  iterations: 200,                               // 10 VUs * 20 each

  thresholds: {
    'http_req_failed':    ['rate<0.05'],
    'http_req_duration{name:me}': ['p(95)<500'],
    'me_success_rate':    ['rate>0.95'],
  },
};

export default function () {
  // Re-login on every iteration: k6's per-VU cookie jar does not reliably
  // persist session cookies across iterations with the shared-iterations
  // executor. The login tag keeps these requests out of the {name:me} threshold.
  loginAs(SEEDED_USER.email, SEEDED_USER.password);

  const res = http.get(config.endpoints.me, {
    tags: { name: 'me' },
  });

  let body = {};
  try { body = res.json(); } catch (_) { /* ignore */ }

  const ok = check(res, {
    'status is 200':        (r) => r.status === 200,
    'success flag is true': () => body && body.success === true,
    'returned email':       () => body && body.data && body.data.email === SEEDED_USER.email,
  });

  meSuccess.add(ok);

  if (!ok) {
    console.warn(`[me-load] VU=${__VU} iter=${__ITER} status=${res.status} body=${res.body}`);
  }
}
