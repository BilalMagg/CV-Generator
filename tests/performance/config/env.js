// Central place to resolve the target environment.
// Override per run with: k6 run -e BASE_URL=https://staging.example.com tests/performance/...
const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080';

export const config = {
  baseUrl: BASE_URL,
  endpoints: {
    register:     `${BASE_URL}/api/auth/register`,
    login:        `${BASE_URL}/api/auth/login`,
    me:           `${BASE_URL}/api/auth/me`,
    applications: `${BASE_URL}/api/applications`,
    cvprofiles:   `${BASE_URL}/api/user-content/cvprofiles`,
  },
};

// Seeded login in cv-realm-test (see tests/scripts/cv-realm-test-realm.json).
// Shared by every auth-protected scenario so test data stays predictable.
export const SEEDED_USER = {
  email: 'test@example.com',
  password: 'test1234',
};
