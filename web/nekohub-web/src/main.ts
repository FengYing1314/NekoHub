import { createApp } from 'vue';
import { createPinia } from 'pinia';
import App from './App.vue';
import './style.css';
import { router } from './app/router';
import { i18n } from './locales';
import { useAppConfigStore } from './stores/app-config';
import { setupHttpClientInterceptors } from './api/client/http-client';

const app = createApp(App);
const pinia = createPinia();

app.use(pinia);

const appConfigStore = useAppConfigStore(pinia);
appConfigStore.hydrate();

setupHttpClientInterceptors(pinia);

app.use(i18n);
app.use(router);

app.mount('#app');
