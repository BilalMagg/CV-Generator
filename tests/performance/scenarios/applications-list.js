import http from 'k6/http';
import { check } from 'k6';
import { Rate } from 'k6/metrics';
import { config, SEEDED_USER } from '../config/env.js';
import { loginAs } from '../lib/auth.js';

// ── Test plan ───────────────────────────────────────────────────────────────
// Goal: stress GET /api/applications with random filter combos. This is the
// most complex SQL filter the .NET services own (see
// application-service/Controllers/ApplicationsController.cs:60-84):
//   page, pageSize, statuses (CSV), search, appliedFrom/To, updatedFrom/To.
//
// Each iteration picks a random subset so we cover many query shapes rather
// than caching the same plan in Postgres. This is where missing indexes
// surface first.
//
// Note: against a freshly-wiped test DB the result set will be empty, so this
// measures filter-planner and query-compile cost, not result-streaming cost.
// To stress result hydration, seed applications first (out of scope for v1).
//
// Run:
//   k6 run tests/performance/scenarios/applications-list.js
// ────────────────────────────────────────────────────────────────────────────

const listSuccess = new Rate('applications_list_success_rate');

export const options = {
  vus: 10,
  iterations: 100,                                // 10 VUs * 10 each

  thresholds: {
    'http_req_failed':                          ['rate<0.05'],
    'http_req_duration{name:applications-list}': ['p(95)<1500'],
    'applications_list_success_rate':           ['rate>0.95'],
  },
};

const STATUSES = ['applied', 'interviewing', 'offered', 'rejected', 'accepted'];
const SEARCH_TERMS = ['developer', 'engineer', 'manager', 'designer', ''];

// k6 uses the goja JS engine which does not support URLSearchParams.
// Build query strings manually with encodeURIComponent instead.
function buildQuery() {
  const parts = [];
  parts.push('page=' + (1 + Math.floor(Math.random() * 3)));
  parts.push('pageSize=' + (10 + Math.floor(Math.random() * 40)));

  if (Math.random() < 0.7) {
    const picks = STATUSES.filter(() => Math.random() < 0.5);
    if (picks.length > 0) parts.push('statuses=' + encodeURIComponent(picks.join(',')));
  }

  if (Math.random() < 0.5) {
    const term = SEARCH_TERMS[Math.floor(Math.random() * SEARCH_TERMS.length)];
    if (term) parts.push('search=' + encodeURIComponent(term));
  }

  if (Math.random() < 0.4) {
    parts.push('appliedFrom=2024-01-01');
    parts.push('appliedTo=2026-12-31');
  }
  if (Math.random() < 0.3) {
    parts.push('updatedFrom=2025-01-01');
  }

  return parts.join('&');
}

export default function () {
  // Re-login on every iteration: k6's per-VU cookie jar does not reliably
  // persist session cookies across iterations with the shared-iterations
  // executor. The login tag keeps these requests out of the applications-list
  // duration threshold.
  loginAs(SEEDED_USER.email, SEEDED_USER.password);

  const url = `${config.endpoints.applications}?${buildQuery()}`;
  const res = http.get(url, { tags: { name: 'applications-list' } });

  let body = {};
  try { body = res.json(); } catch (_) { /* ignore */ }

  const ok = check(res, {
    'status is 200':              (r) => r.status === 200,
    'response has data envelope': () => body && (body.success === true || body.data !== undefined),
  });

  listSuccess.add(ok);

  if (!ok) {
    console.warn(`[apps-list] VU=${__VU} iter=${__ITER} url=${url} status=${res.status} body=${res.body}`);
  }
}
