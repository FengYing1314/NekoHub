import { defineStore } from 'pinia';
import { readFromLocalStorage, writeToLocalStorage } from '../utils/local-storage';
import { runtimeConfig } from '../config/runtime';

const APP_CONFIG_STORAGE_KEY = 'nekohub.app-config';
export const DEFAULT_API_BASE_URL = runtimeConfig.apiBaseUrl;

interface AppConfigPersistedState {
  apiBaseUrl: string;
  apiKey: string;
}

interface AppConfigState {
  apiBaseUrl: string;
  apiKey: string;
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

export const useAppConfigStore = defineStore('appConfig', {
  state: (): AppConfigState => readInitialState(),
  getters: {
    isApiKeyConfigured: (state) => state.apiKey.length > 0,
  },
  actions: {
    hydrate() {
      const nextState = readInitialState();
      this.apiBaseUrl = nextState.apiBaseUrl;
      this.apiKey = nextState.apiKey;
    },
    setConfig(payload: { apiBaseUrl: string; apiKey: string }) {
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
