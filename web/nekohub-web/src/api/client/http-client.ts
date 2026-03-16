import axios, { type AxiosRequestConfig, type InternalAxiosRequestConfig } from 'axios';
import type { Pinia } from 'pinia';
import { useAppConfigStore } from '../../stores/app-config';
import { useAuthStore } from '../../stores/auth.store';

export const httpClient = axios.create({
  timeout: 15000,
});

export const authHttpClient = axios.create({
  timeout: 15000,
});

interface RetriableRequestConfig extends InternalAxiosRequestConfig {
  _retry?: boolean;
}

interface HttpClientInterceptorOptions {
  pinia: Pinia;
  onUnauthorized?: () => void;
}

let interceptorsInitialized = false;
// 所有并发 401 共用同一个 refresh promise，避免短时间内打出多次 refresh 请求。
let refreshTokenPromise: Promise<string | null> | null = null;

function applyBaseUrl(config: AxiosRequestConfig, pinia: Pinia): void {
  const appConfigStore = useAppConfigStore(pinia);
  config.baseURL = appConfigStore.apiBaseUrl;
}

function isAuthRequest(url: string | undefined): boolean {
  if (!url) {
    return false;
  }

  return url.includes('/api/v1/auth/login')
    || url.includes('/api/v1/auth/refresh')
    || url.includes('/api/v1/auth/logout');
}

export function setupHttpClientInterceptors(options: HttpClientInterceptorOptions): void {
  if (interceptorsInitialized) {
    return;
  }

  interceptorsInitialized = true;

  const { pinia, onUnauthorized } = options;

  authHttpClient.interceptors.request.use((config) => {
    applyBaseUrl(config, pinia);
    return config;
  });

  httpClient.interceptors.request.use((config) => {
    const authStore = useAuthStore(pinia);
    applyBaseUrl(config, pinia);
    if (authStore.accessToken) {
      config.headers = config.headers ?? {};
      (config.headers as Record<string, string>).Authorization = `Bearer ${authStore.accessToken}`;
    }
    return config;
  });

  httpClient.interceptors.response.use(
    (response) => response,
    async (error) => {
      if (!axios.isAxiosError(error)) {
        throw error;
      }

      const statusCode = error.response?.status;
      const requestConfig = error.config as RetriableRequestConfig | undefined;
      // 已经重试过、认证接口本身、或非 401 的请求都不进入自动 refresh 分支，避免死循环。
      if (!requestConfig || statusCode !== 401 || requestConfig._retry || isAuthRequest(requestConfig.url)) {
        throw error;
      }

      const authStore = useAuthStore(pinia);
      if (!authStore.refreshToken) {
        authStore.clearSession();
        onUnauthorized?.();
        throw error;
      }

      requestConfig._retry = true;

      if (!refreshTokenPromise) {
        // 第一条 401 负责发起 refresh，后续请求只等待同一个结果。
        refreshTokenPromise = authStore
          .refreshSession()
          .finally(() => {
            refreshTokenPromise = null;
          });
      }

      const refreshedAccessToken = await refreshTokenPromise;
      if (!refreshedAccessToken) {
        if (!authStore.refreshToken || !authStore.accessToken) {
          authStore.clearSession();
          onUnauthorized?.();
        }

        throw error;
      }

      requestConfig.headers = requestConfig.headers ?? {};
      (requestConfig.headers as Record<string, string>).Authorization = `Bearer ${refreshedAccessToken}`;
      return httpClient.request(requestConfig);
    },
  );
}
