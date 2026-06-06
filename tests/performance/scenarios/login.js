import http from 'k6/http';
import { check } from 'k6';
import { Rate, Counter } from 'k6/metrics';
import { config, SEEDED_USER } from '../config/env.js';
import { jsonHeaders } from '../lib/helpers.js';

// ── Test plan ───────────────────────────────────────────────────────────────
// Goal: hammer POST /api/auth/login with the seeded testuser to measure
// concurrent token-issuance throughput. Same credentials every time -- this
// is the "many users log in at once" pattern, not the "many distinct users"
// pattern.
//
// Run:
//   k6 run tests/performance/scenarios/login.js
//
// What we watch:
//   - http_req_duration p95 -- Keycloak token endpoint is the bottleneck
//     (saw 21s on register earlier; if login is also slow, root cause is shared)
//   - login_success_rate    -- % of 2xx + success:true responses
//   - login_4xx / login_5xx -- counts split by class for easy diagnosis
// ────────────────────────────────────────────────────────────────────────────

const loginSuccess = new Rate('login_success_rate');
const login4xx = new Counter('login_4xx');
const login5xx = new Counter('login_5xx');

export const options = {
  vus: 10,
  iterations: 50,                                // 10 VUs share 50 iters

  thresholds: {
    'http_req_failed':       ['rate<0.10'],
    'http_req_duration':     ['p(95)<5000'],     // local Keycloak token endpoint ~3.8s; 5s gives headroom
    'login_success_rate':    ['rate>0.90'],
  },
};

export function setup() {
  console.log(`[login-load] target = ${config.endpoints.login} as ${SEEDED_USER.email}`);
}

export default function () {
  const res = http.post(
    config.endpoints.login,
    JSON.stringify({ email: SEEDED_USER.email, password: SEEDED_USER.password }),
    {
      headers: jsonHeaders,
      tags: { name: 'login' },
    },
  );

  let body = {};
  try { body = res.json(); } catch (_) { /* ignore */ }

  const ok = check(res, {
    'status is 200':         (r) => r.status === 200,
    'success flag is true':  () => body && body.success === true,
    'access token returned': () => body && body.data && body.data.tokens && typeof body.data.tokens.accessToken === 'string' && body.data.tokens.accessToken.length > 0,
  });

  loginSuccess.add(ok);
  if (res.status >= 400 && res.status < 500) login4xx.add(1);
  if (res.status >= 500) login5xx.add(1);

  if (!ok) {
    console.warn(`[login-load] VU=${__VU} iter=${__ITER} status=${res.status} body=${res.body}`);
  }
}
