import axios, { type InternalAxiosRequestConfig } from 'axios';
import type { Pinia } from 'pinia';
import { watch } from 'vue';
import { useAppConfigStore } from '../../stores/app-config';
import { useAuthStore } from '../../stores/auth.store';
import { getApiBackendIdentity } from '../../config/api-backend';

export const httpClient = axios.create({ timeout: 15000 });
export const authHttpClient = axios.create({ timeout: 15000 });

interface SessionRequestConfig extends InternalAxiosRequestConfig {
  _retry?: boolean;
  _sessionVersion?: number;
  _backendUrl?: string;
}

interface HttpClientInterceptorOptions {
  pinia: Pinia;
  onUnauthorized?: () => void;
}

let interceptorsInitialized = false;

export function setupHttpClientInterceptors({ pinia, onUnauthorized }: HttpClientInterceptorOptions): void {
  if (interceptorsInitialized) {
    return;
  }
  interceptorsInitialized = true;
  const authStore = useAuthStore(pinia);
  const configStore = useAppConfigStore(pinia);
  let sessionController = new AbortController();
  let refreshPromise: Promise<string | null> | null = null;

  watch(
    [() => authStore.sessionVersion, () => getApiBackendIdentity(configStore.apiBaseUrl)],
    () => {
      sessionController.abort();
      sessionController = new AbortController();
      refreshPromise = null;
    },
    { flush: 'sync' },
  );

  function assertCurrent(config: SessionRequestConfig): void {
    if ((config._sessionVersion !== undefined && config._sessionVersion !== authStore.sessionVersion)
      || (config._backendUrl !== undefined && config._backendUrl !== getApiBackendIdentity(configStore.apiBaseUrl))) {
      throw new axios.CanceledError('请求所属会话或后端已改变', undefined, config);
    }
  }

  function prepareRequest(config: SessionRequestConfig, authenticated: boolean): SessionRequestConfig {
    assertCurrent(config);
    const backend = getApiBackendIdentity(configStore.apiBaseUrl);
    config.baseURL = backend;
    config._backendUrl = backend;
    config._sessionVersion = authStore.sessionVersion;
    config.signal = config.signal
      ? AbortSignal.any([config.signal as AbortSignal, sessionController.signal])
      : sessionController.signal;
    if (authenticated && authStore.isAuthenticated) {
      config.headers.set('Authorization', `Bearer ${authStore.accessToken}`);
    } else {
      config.headers.delete('Authorization');
    }
    return config;
  }

  // 同步固定请求所属会话，后续重试不能被重定向到新配置的服务器。
  authHttpClient.interceptors.request.use((config) => prepareRequest(config, false), (error) => { throw error; }, { synchronous: true });
  httpClient.interceptors.request.use((config) => prepareRequest(config, true), (error) => { throw error; }, { synchronous: true });
  authHttpClient.interceptors.response.use((response) => {
    assertCurrent(response.config);
    return response;
  });

  httpClient.interceptors.response.use(
    (response) => {
      assertCurrent(response.config);
      return response;
    },
    async (error) => {
      if (!axios.isAxiosError(error)) {
        throw error;
      }
      const config = error.config as SessionRequestConfig | undefined;
      if (!config) {
        throw error;
      }
      assertCurrent(config);
      if (error.response?.status !== 401 || config._retry) {
        throw error;
      }
      if (!authStore.isAuthenticated) {
        authStore.clearSession();
        onUnauthorized?.();
        throw error;
      }
      config._retry = true;
      // 旧会话的 promise 结束时不能清掉新会话的刷新任务。
      if (!refreshPromise) {
        const pending = authStore.refreshSession().finally(() => {
          if (refreshPromise === pending) {
            refreshPromise = null;
          }
        });
        refreshPromise = pending;
      }
      const version = authStore.sessionVersion;
      const refreshedAccessToken = await refreshPromise;
      if (version !== authStore.sessionVersion) {
        throw new axios.CanceledError('刷新所属会话已结束', undefined, config);
      }
      assertCurrent(config);
      if (!refreshedAccessToken) {
        if (!authStore.isAuthenticated) {
          onUnauthorized?.();
        }
        throw error;
      }
      config.headers = config.headers ?? {};
      (config.headers as Record<string, string>).Authorization = `Bearer ${refreshedAccessToken}`;
      return httpClient.request(config);
    },
  );
}
