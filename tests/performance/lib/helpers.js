// Build a unique email per VU + iteration so the register endpoint
// never sees the same email twice across a single run.
//
//   runId   - a single timestamp captured in setup(), shared by all VUs of this run
//   vu      - the virtual user id (1..N)
//   iter    - the iteration index within that VU (0..M-1)
export function buildUser(runId, vu, iter) {
  const tag = `${runId}_v${vu}_i${iter}`;
  return {
    firstName: `Perf${vu}`,
    lastName: `User${iter}`,
    email: `perf_${tag}@cvgen-loadtest.local`,
    password: 'PerfTest@2026!',
  };
}

// Common JSON headers — k6 expects a plain object, not a Headers instance.
export const jsonHeaders = {
  'Content-Type': 'application/json',
  Accept: 'application/json',
};
