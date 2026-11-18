import { defineConfig } from 'vitest/config';
import path from 'path';

export default defineConfig({
  test: {
    environment: 'jsdom',
    globals: true,
  },
  resolve: {
    alias: {
      '@app': path.resolve(__dirname, './src/app'),
      '@env': path.resolve(__dirname, './src/environments'),
    },
  },
});
