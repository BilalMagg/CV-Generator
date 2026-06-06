import http from 'k6/http';
import { check } from 'k6';
import { Rate } from 'k6/metrics';
import { config } from '../config/env.js';
import { buildUser, jsonHeaders } from '../lib/helpers.js';

// ── Test plan ───────────────────────────────────────────────────────────────
// Goal: catch integration cliffs that single-endpoint scenarios miss. Per VU,
// one full new-user journey:
//
//   1. POST /api/auth/register    (creates fresh Keycloak + user-service user)
//   2. POST /api/auth/login       (gets the session cookie)
//   3. GET  /api/auth/me          (validates the cookie path works)
//   4. POST /api/user-content/cvprofiles    (write + Kafka publish)
//   5. GET  /api/applications     (auth'd query)
//
// 10 VUs x 1 iteration -> 10 complete journeys in parallel. Each step is
// tagged so http_req_duration{step:...} breaks down per step in the summary.
// A failure in any step skips the remaining steps for that VU (the journey
// is sequential by design -- you can't list applications if login failed).
//
// What this catches that single-endpoint tests don't:
//   - The user-service consumer can lag behind the Kafka "user.registered"
//     event. If login fires before the consumer caught up, /me will succeed
//     but downstream calls that need a synced user_id will fail.
//   - Cookie-domain / SameSite issues between the auth and proxy paths.
//
// Run:
//   k6 run tests/performance/scenarios/journey.js
// ────────────────────────────────────────────────────────────────────────────

const journeyOk    = new Rate('journey_success_rate');
const stepRegister = new Rate('journey_step_register_rate');
const stepLogin    = new Rate('journey_step_login_rate');
const stepMe       = new Rate('journey_step_me_rate');
const stepProfile  = new Rate('journey_step_cvprofile_rate');
const stepList     = new Rate('journey_step_apps_rate');

export const options = {
  vus: 10,
  iterations: 10,                                // 1 journey per VU

  thresholds: {
    'http_req_failed':              ['rate<0.10'],
    'journey_success_rate':         ['rate>0.90'],
    'journey_step_register_rate':   ['rate>0.90'],
    'journey_step_login_rate':      ['rate>0.90'],
    'journey_step_me_rate':         ['rate>0.90'],
    'journey_step_cvprofile_rate':  ['rate>0.90'],
    'journey_step_apps_rate':       ['rate>0.90'],
  },
};

export function setup() {
  const runId = Date.now().toString(36);
  console.log(`[journey] run id = ${runId}`);
  return { runId };
}

function step(name, fn) {
  const res = fn();
  let body = {};
  try { body = res.json(); } catch (_) { /* ignore */ }
  return { res, body };
}

export default function (data) {
  const user = buildUser(data.runId, __VU, __ITER);
  let allStepsOk = true;

  // Step 1 -- register
  {
    const { res, body } = step('register', () =>
      http.post(config.endpoints.register, JSON.stringify(user), {
        headers: jsonHeaders,
        tags: { name: 'journey', step: 'register' },
      }),
    );
    const ok = check(res, {
      'register 200':    (r) => r.status === 200,
      'register success': () => body && body.success === true,
    });
    stepRegister.add(ok);
    if (!ok) {
      console.warn(`[journey] VU=${__VU} step=register status=${res.status} body=${res.body}`);
      journeyOk.add(false);
      return;
    }
  }

  // Step 2 -- login (sets the cookie in the per-VU jar)
  {
    const { res, body } = step('login', () =>
      http.post(
        config.endpoints.login,
        JSON.stringify({ email: user.email, password: user.password }),
        { headers: jsonHeaders, tags: { name: 'journey', step: 'login' } },
      ),
    );
    const ok = check(res, {
      'login 200':     (r) => r.status === 200,
      'login success': () => body && body.success === true,
    });
    stepLogin.add(ok);
    if (!ok) {
      console.warn(`[journey] VU=${__VU} step=login status=${res.status} body=${res.body}`);
      journeyOk.add(false);
      return;
    }
  }

  // Step 3 -- me
  {
    const { res, body } = step('me', () =>
      http.get(config.endpoints.me, { tags: { name: 'journey', step: 'me' } }),
    );
    const ok = check(res, {
      'me 200':            (r) => r.status === 200,
      'me echoes email':   () => body && body.data && body.data.email === user.email,
    });
    stepMe.add(ok);
    if (!ok) { allStepsOk = false; console.warn(`[journey] VU=${__VU} step=me status=${res.status}`); }
  }

  // Step 4 -- create CV profile
  {
    const dto = {
      Title:   `Journey Profile ${data.runId} v${__VU}`,
      Summary: 'Created during journey load test.',
    };
    const { res, body } = step('cvprofile', () =>
      http.post(config.endpoints.cvprofiles, JSON.stringify(dto), {
        headers: jsonHeaders,
        tags: { name: 'journey', step: 'cvprofile' },
      }),
    );
    const ok = check(res, {
      'cvprofile 201':   (r) => r.status === 201,
      'cvprofile id':    () => body && body.data && typeof body.data.id === 'string',
    });
    stepProfile.add(ok);
    if (!ok) { allStepsOk = false; console.warn(`[journey] VU=${__VU} step=cvprofile status=${res.status} body=${res.body}`); }
  }

  // Step 5 -- list applications
  {
    const { res } = step('apps', () =>
      http.get(`${config.endpoints.applications}?page=1&pageSize=20`, {
        tags: { name: 'journey', step: 'apps' },
      }),
    );
    const ok = check(res, {
      'apps 200': (r) => r.status === 200,
    });
    stepList.add(ok);
    if (!ok) { allStepsOk = false; console.warn(`[journey] VU=${__VU} step=apps status=${res.status}`); }
  }

  journeyOk.add(allStepsOk);
}
