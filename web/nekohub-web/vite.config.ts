import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';

const devProxyTarget = process.env.NEKOHUB_DEV_PROXY_TARGET ?? 'http://localhost:5121';

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue()],
  server: {
    host: '0.0.0.0',
    port: 5173,
    proxy: {
      '/api': {
        target: devProxyTarget,
        changeOrigin: true,
      },
      '/mcp': {
        target: devProxyTarget,
        changeOrigin: true,
      },
      '/content': {
        target: devProxyTarget,
        changeOrigin: true,
      },
      '/openapi': {
        target: devProxyTarget,
        changeOrigin: true,
      },
    },
  },
});
