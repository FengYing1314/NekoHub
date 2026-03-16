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

function parseAllowedHosts(rawValue: string | undefined): true | string[] | undefined {
  if (!rawValue) {
    return undefined;
  }

  const trimmed = rawValue.trim();
  if (!trimmed) {
    return undefined;
  }

  if (trimmed === '*' || /^true$/i.test(trimmed) || /^all$/i.test(trimmed)) {
    return true;
  }

  const hosts = trimmed
    .split(/[,\s]+/)
    .map((value) => value.trim())
    .filter((value) => value.length > 0);

  return hosts.length > 0 ? hosts : undefined;
}

function resolvePreviewAllowedHosts(): true | string[] {
  return parseAllowedHosts(process.env.VITE_ALLOWED_HOSTS) ?? true;
}

// https://vite.dev/config/
export default defineConfig({
  base: resolveViteBasePath(),
  plugins: [vue()],
  build: {
    assetsDir: 'static',
    chunkSizeWarningLimit: 550,
    rollupOptions: {
      output: {
        manualChunks(id) {
          const normalizedId = id.replace(/\\/g, '/');

          if (!normalizedId.includes('/node_modules/')) {
            return undefined;
          }

          if (normalizedId.includes('/node_modules/vue-router/')) {
            return 'vendor-router';
          }

          if (normalizedId.includes('/node_modules/pinia/')) {
            return 'vendor-pinia';
          }

          if (normalizedId.includes('/node_modules/vue-i18n/')) {
            return 'vendor-i18n';
          }

          if (normalizedId.includes('/node_modules/naive-ui/')) {
            return 'vendor-naive-ui';
          }

          if (
            normalizedId.includes('/node_modules/vueuc/')
            || normalizedId.includes('/node_modules/vooks/')
            || normalizedId.includes('/node_modules/css-render/')
            || normalizedId.includes('/node_modules/@css-render/')
            || normalizedId.includes('/node_modules/seemly/')
            || normalizedId.includes('/node_modules/evtd/')
            || normalizedId.includes('/node_modules/date-fns/')
          ) {
            return 'vendor-naive-ui-support';
          }

          if (normalizedId.includes('/node_modules/axios/')) {
            return 'vendor-axios';
          }

          if (
            normalizedId.includes('/node_modules/vue/')
            || normalizedId.includes('/node_modules/@vue/')
          ) {
            return 'vendor-vue';
          }

          return 'vendor-misc';
        },
      },
    },
  },
  server: {
    host: '0.0.0.0',
    port: 5173,
  },
  preview: {
    host: '0.0.0.0',
    port: 4173,
    allowedHosts: resolvePreviewAllowedHosts(),
  },
});
