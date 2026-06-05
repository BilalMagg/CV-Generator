// Central place to resolve the target environment.
// Override per run with: k6 run -e BASE_URL=https://staging.example.com tests/performance/...
const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080';

export const config = {
  baseUrl: BASE_URL,
  endpoints: {
    register: `${BASE_URL}/api/auth/register`,
    login: `${BASE_URL}/api/auth/login`,
    me: `${BASE_URL}/api/auth/me`,
  },
};
