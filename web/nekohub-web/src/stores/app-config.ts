import { defineStore } from 'pinia';
import { readFromLocalStorage, writeToLocalStorage } from '../utils/local-storage';
import { fetchSystemBootstrap } from '../api/system/system.api';
import { runtimeConfig } from '../config/runtime';
import type { SystemBootstrapResponse } from '../types/system';

const APP_CONFIG_STORAGE_KEY = 'nekohub.app-config';
export const DEFAULT_API_BASE_URL = runtimeConfig.apiBaseUrl;
const DEFAULT_ALLOWED_CONTENT_TYPES = ['image/jpeg', 'image/png', 'image/webp', 'image/gif'];

export interface AppConfigPayload {
  apiBaseUrl: string;
}

export interface AppConfigValidationOptions {
  allowEmptyApiBaseUrl?: boolean;
}

export interface AppConfigValidationResult {
  apiBaseUrlMissing: boolean;
}

interface AppConfigPersistedState {
  apiBaseUrl: string;
}

interface AppConfigState {
  apiBaseUrl: string;
  bootstrapAvailable: boolean;
  maxUploadSizeBytes: number;
  allowedContentTypes: string[];
  isConfigModalOpen: boolean;
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
async function inspectSystemBootstrap(apiBaseUrl: string): Promise<SystemBootstrapResponse | null> {
  try {
    return await fetchSystemBootstrap(normalizeBaseUrl(apiBaseUrl));
  } catch {
    return null;
  }
}

function readInitialState(): AppConfigState {
  const persisted = readFromLocalStorage<AppConfigPersistedState>(APP_CONFIG_STORAGE_KEY);

  if (!persisted) {
    return {
      apiBaseUrl: DEFAULT_API_BASE_URL,
      bootstrapAvailable: false,
      maxUploadSizeBytes: runtimeConfig.maxUploadSizeBytes,
      allowedContentTypes: DEFAULT_ALLOWED_CONTENT_TYPES,
      isConfigModalOpen: false,
    };
  }

  return {
    apiBaseUrl: normalizeBaseUrl(persisted.apiBaseUrl),
    bootstrapAvailable: false,
    maxUploadSizeBytes: runtimeConfig.maxUploadSizeBytes,
    allowedContentTypes: DEFAULT_ALLOWED_CONTENT_TYPES,
    isConfigModalOpen: false,
  };
}

export function validateAppConfigPayload(
  payload: AppConfigPayload,
  options: AppConfigValidationOptions = {},
): AppConfigValidationResult {
  const allowEmptyApiBaseUrl = options.allowEmptyApiBaseUrl ?? false;

  return {
    apiBaseUrlMissing: !allowEmptyApiBaseUrl && !hasConfiguredValue(payload.apiBaseUrl),
  };
}

export const useAppConfigStore = defineStore('appConfig', {
  state: (): AppConfigState => readInitialState(),
  getters: {
    isApiBaseUrlConfigured: (state) => state.bootstrapAvailable,
    isConfigured(): boolean {
      return this.isApiBaseUrlConfigured;
    },
  },
  actions: {
    async hydrate() {
      const nextState = readInitialState();
      this.apiBaseUrl = nextState.apiBaseUrl;
      this.bootstrapAvailable = nextState.bootstrapAvailable;
      this.maxUploadSizeBytes = nextState.maxUploadSizeBytes;
      this.allowedContentTypes = nextState.allowedContentTypes;
      this.isConfigModalOpen = false;
      await this.refreshBootstrap();
    },
    async setConfig(payload: AppConfigPayload) {
      const apiBaseUrl = normalizeBaseUrl(payload.apiBaseUrl);
      const bootstrap = await fetchSystemBootstrap(apiBaseUrl);

      this.apiBaseUrl = apiBaseUrl;
      this.bootstrapAvailable = true;
      this.maxUploadSizeBytes = bootstrap.maxUploadSizeBytes;
      this.allowedContentTypes = [...bootstrap.allowedContentTypes];
      this.persist();
      this.closeConfigModal();
    },
    async refreshBootstrap() {
      const bootstrap = await inspectSystemBootstrap(this.apiBaseUrl);
      if (bootstrap) {
        this.bootstrapAvailable = true;
        this.maxUploadSizeBytes = bootstrap.maxUploadSizeBytes;
        this.allowedContentTypes = [...bootstrap.allowedContentTypes];
      } else {
        this.bootstrapAvailable = false;
        this.maxUploadSizeBytes = runtimeConfig.maxUploadSizeBytes;
        this.allowedContentTypes = [...DEFAULT_ALLOWED_CONTENT_TYPES];
      }
    },
    async inspectBootstrap(apiBaseUrl: string): Promise<SystemBootstrapResponse | null> {
      return inspectSystemBootstrap(apiBaseUrl);
    },
    openConfigModal() {
      this.isConfigModalOpen = true;
    },
    closeConfigModal() {
      this.isConfigModalOpen = false;
    },
    persist() {
      writeToLocalStorage(APP_CONFIG_STORAGE_KEY, {
        apiBaseUrl: this.apiBaseUrl,
      } satisfies AppConfigPersistedState);
    },
  },
});
