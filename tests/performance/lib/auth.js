import http from 'k6/http';
import { fail } from 'k6';
import { config } from '../config/env.js';
import { jsonHeaders } from './helpers.js';

// The gateway's POST /api/auth/login does two things:
//  1. Returns { success: true, data: { userId, email, tokens: { accessToken }, ... } }
//  2. Sets a session cookie via SignInAsync(CookieAuthentication...)
//
// All downstream gateway routes (/api/applications, /api/user-content/...) are
// cookie-authenticated -- YARP transforms read the access_token claim off the
// cookie principal and inject "Authorization: Bearer ..." into upstream
// requests internally (api_gateway/Program.cs:528).
//
// k6 maintains a per-VU cookie jar automatically, so as long as the same VU
// calls loginAs() once, all subsequent http calls in that VU re-send the
// session cookie. There is no token to pass around manually.
export function loginAs(email, password) {
  const res = http.post(
    config.endpoints.login,
    JSON.stringify({ email, password }),
    {
      headers: jsonHeaders,
      tags: { name: 'login' },
    },
  );

  if (res.status !== 200) {
    fail(`loginAs(${email}) failed: status=${res.status} body=${res.body}`);
  }

  let body = {};
  try { body = res.json(); } catch (_) { /* ignore */ }
  if (!body || body.success !== true) {
    fail(`loginAs(${email}) returned non-success body: ${res.body}`);
  }
  return body.data || {};
}
