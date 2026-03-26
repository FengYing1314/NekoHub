import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';

function normalizeBasePath(rawBasePath: string | undefined): string | undefined {
  if (!rawBasePath) {
    return undefined;
  }

  const trimmed = rawBasePath.trim();
  if (!trimmed) {
    return undefined;
  }

  if (trimmed === '/') {
    return '/';
  }

  const withoutEdgeSlashes = trimmed.replace(/^\/+|\/+$/g, '');
  if (!withoutEdgeSlashes) {
    return '/';
  }

  return `/${withoutEdgeSlashes}/`;
}

function resolveViteBasePath(): string {
  const explicitBasePath = normalizeBasePath(process.env.VITE_BASE_PATH);
  if (explicitBasePath) {
    return explicitBasePath;
  }

  if (process.env.GITHUB_ACTIONS === 'true') {
    const repoName = process.env.GITHUB_REPOSITORY?.split('/')[1]?.trim();
    if (repoName && !/\.github\.io$/i.test(repoName)) {
      return `/${repoName}/`;
    }
  }

  return '/';
}

// https://vite.dev/config/
export default defineConfig({
  base: resolveViteBasePath(),
  plugins: [vue()],
  server: {
    host: '0.0.0.0',
    port: 5173,
  },
  preview: {
    host: '0.0.0.0',
    port: 4173,
  },
});
