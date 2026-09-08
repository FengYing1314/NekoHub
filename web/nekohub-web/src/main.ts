import { createApp, watch } from 'vue';
import { createPinia } from 'pinia';
import App from './App.vue';
import './style.css';
import { router } from './app/router';
import { i18n } from './locales';
import { useAppConfigStore } from './stores/app-config';
import { useAuthStore } from './stores/auth.store';
import { setupHttpClientInterceptors } from './api/client/http-client';

async function bootstrap(): Promise<void> {
  const app = createApp(App);
  const pinia = createPinia();

  app.use(pinia);

  const appConfigStore = useAppConfigStore(pinia);
  await appConfigStore.hydrate();

  const authStore = useAuthStore(pinia);
  authStore.hydrate();

  setupHttpClientInterceptors({
    pinia,
    onUnauthorized: () => {
      const redirect = router.currentRoute.value.fullPath;
      if (router.currentRoute.value.path === '/login') {
        return;
      }

      void router.push({
        path: '/login',
        query: {
          redirect,
        },
      });
    },
  });

  watch(() => authStore.isAuthenticated, (authenticated, previouslyAuthenticated) => {
    if (!authenticated && previouslyAuthenticated && router.currentRoute.value.meta.requiresAuth) {
      void router.replace({ path: '/login', query: { redirect: router.currentRoute.value.fullPath } });
    }
  });

  await authStore.bootstrapSession();

  app.use(i18n);
  app.use(router);

  app.mount('#app');
}

void bootstrap();
