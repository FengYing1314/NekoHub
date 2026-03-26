import { defineStore } from 'pinia';
import { readFromLocalStorage, writeToLocalStorage } from '../utils/local-storage';
import { runtimeConfig } from '../config/runtime';

const APP_CONFIG_STORAGE_KEY = 'nekohub.app-config';
export const DEFAULT_API_BASE_URL = runtimeConfig.apiBaseUrl;

export interface AppConfigPayload {
  apiBaseUrl: string;
  apiKey: string;
}

export interface AppConfigValidationResult {
  apiBaseUrlMissing: boolean;
  apiKeyMissing: boolean;
}

interface AppConfigPersistedState {
  apiBaseUrl: string;
  apiKey: string;
}

interface AppConfigState {
  apiBaseUrl: string;
  apiKey: string;
}

function hasConfiguredValue(value: string): boolean {
  return value.trim().length > 0;
}

function normalizeBaseUrl(apiBaseUrl: string): string {
  const trimmed = apiBaseUrl.trim();

  if (!trimmed) {
    return DEFAULT_API_BASE_URL;
  }

  return trimmed.replace(/\/+$/, '');
}

function readInitialState(): AppConfigState {
  const persisted = readFromLocalStorage<AppConfigPersistedState>(APP_CONFIG_STORAGE_KEY);

  if (!persisted) {
    return {
      apiBaseUrl: DEFAULT_API_BASE_URL,
      apiKey: '',
    };
  }

  return {
    apiBaseUrl: normalizeBaseUrl(persisted.apiBaseUrl),
    apiKey: persisted.apiKey?.trim() ?? '',
  };
}

export function validateAppConfigPayload(payload: AppConfigPayload): AppConfigValidationResult {
  return {
    apiBaseUrlMissing: !hasConfiguredValue(payload.apiBaseUrl),
    apiKeyMissing: !hasConfiguredValue(payload.apiKey),
  };
}

export const useAppConfigStore = defineStore('appConfig', {
  state: (): AppConfigState => readInitialState(),
  getters: {
    isApiBaseUrlConfigured: (state) => hasConfiguredValue(state.apiBaseUrl),
    isApiKeyConfigured: (state) => state.apiKey.length > 0,
    isConfigured(): boolean {
      return this.isApiBaseUrlConfigured && this.isApiKeyConfigured;
    },
  },
  actions: {
    hydrate() {
      const nextState = readInitialState();
      this.apiBaseUrl = nextState.apiBaseUrl;
      this.apiKey = nextState.apiKey;
    },
    setConfig(payload: AppConfigPayload) {
      this.apiBaseUrl = normalizeBaseUrl(payload.apiBaseUrl);
      this.apiKey = payload.apiKey.trim();
      this.persist();
    },
    persist() {
      writeToLocalStorage(APP_CONFIG_STORAGE_KEY, {
        apiBaseUrl: this.apiBaseUrl,
        apiKey: this.apiKey,
      } satisfies AppConfigPersistedState);
    },
  },
});
