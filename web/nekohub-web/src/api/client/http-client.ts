import axios from 'axios';
import type { Pinia } from 'pinia';
import { useAppConfigStore } from '../../stores/app-config';

export const httpClient = axios.create({
  timeout: 15000,
});

let interceptorsInitialized = false;

export function setupHttpClientInterceptors(pinia: Pinia): void {
  if (interceptorsInitialized) {
    return;
  }

  interceptorsInitialized = true;

  httpClient.interceptors.request.use((config) => {
    const appConfigStore = useAppConfigStore(pinia);

    config.baseURL = appConfigStore.apiBaseUrl || undefined;

    if (appConfigStore.apiKey) {
      config.headers = config.headers ?? {};
      (config.headers as Record<string, string>).Authorization = `Bearer ${appConfigStore.apiKey}`;
    }

    return config;
  });
}
