import { defineConfig } from 'cypress';
import { existsSync } from 'fs';
import { resolve } from 'path';

// Tests must only run against the test stack (dev.test.ps1), never the dev stack.
// dev.test.ps1 writes a .test-mode sentinel at the repo root on startup and
// removes it on -Stop. If the sentinel is missing we abort before opening the
// runner so we don't accidentally pollute the dev databases.
const sentinelPath = resolve(__dirname, '..', '.test-mode');

export default defineConfig({
  e2e: {
    baseUrl: 'http://localhost:4200',
    setupNodeEvents(_on, _config) {
      if (process.env.CYPRESS_SKIP_TEST_MODE_CHECK !== '1' && !existsSync(sentinelPath)) {
        throw new Error(
          [
            '',
            'Test-mode sentinel missing: ' + sentinelPath,
            '',
            'Cypress refuses to run against the dev stack to avoid polluting',
            'the main databases. Start the test stack first:',
            '',
            '    .\\dev.ps1 -Stop          # if dev is running',
            '    .\\dev.test.ps1',
            '',
            'Or set CYPRESS_SKIP_TEST_MODE_CHECK=1 to bypass (not recommended).',
            '',
          ].join('\n'),
        );
      }
      return _config;
    },
  },

  component: {
    devServer: {
      framework: 'angular',
      bundler: 'webpack',
    },
    specPattern: '**/*.cy.ts',
  },
});
