import { createPinia, setActivePinia } from 'pinia';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useAuthStore } from './auth.store';
import { useAppConfigStore, validateAppConfigPayload } from './app-config';

describe('app-config store', () => {
  beforeEach(() => {
    vi.unstubAllGlobals();
    localStorage.clear();
    setActivePinia(createPinia());
  });

  it('allows empty api base url when bootstrap is already reachable', () => {
    const result = validateAppConfigPayload({
      apiBaseUrl: '',
    }, {
      allowEmptyApiBaseUrl: true,
    });

    expect(result.apiBaseUrlMissing).toBe(false);
  });

  it('hydrates bootstrap metadata from the anonymous bootstrap endpoint', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        data: {
          apiKeyRequired: false,
          maxUploadSizeBytes: 5120,
          allowedContentTypes: ['image/png'],
        },
      }),
    });

    vi.stubGlobal('fetch', fetchMock);

    const store = useAppConfigStore();
    await store.hydrate();

    expect(store.bootstrapAvailable).toBe(true);
    expect(store.maxUploadSizeBytes).toBe(5120);
    expect(store.allowedContentTypes).toEqual(['image/png']);
    expect(fetchMock).toHaveBeenCalledWith('/api/v1/system/bootstrap', expect.any(Object));
  });

  it('persists config only after the candidate api base url passes bootstrap inspection', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        data: {
          apiKeyRequired: false,
          maxUploadSizeBytes: 4096,
          allowedContentTypes: ['image/webp'],
        },
      }),
    });

    vi.stubGlobal('fetch', fetchMock);

    const store = useAppConfigStore();
    await store.setConfig({
      apiBaseUrl: 'https://api.example.com/',
    });

    expect(store.apiBaseUrl).toBe('https://api.example.com');
    expect(store.bootstrapAvailable).toBe(true);
    expect(store.allowedContentTypes).toEqual(['image/webp']);
    expect(localStorage.getItem('nekohub.app-config')).toContain('https://api.example.com');
  });

  it('does not overwrite persisted config when bootstrap inspection fails', async () => {
    localStorage.setItem('nekohub.app-config', JSON.stringify({
      apiBaseUrl: 'https://stable.example.com',
    }));

    const fetchMock = vi.fn()
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({
          data: {
            apiKeyRequired: false,
            maxUploadSizeBytes: 2048,
            allowedContentTypes: ['image/png'],
          },
        }),
      })
      .mockRejectedValueOnce(new Error('offline'));

    vi.stubGlobal('fetch', fetchMock);

    const store = useAppConfigStore();
    await store.hydrate();

    await expect(store.setConfig({
      apiBaseUrl: 'https://broken.example.com',
    })).rejects.toThrow('Bootstrap request could not reach the server.');

    expect(store.apiBaseUrl).toBe('https://stable.example.com');
    expect(localStorage.getItem('nekohub.app-config')).toContain('https://stable.example.com');
  });
  it('preserves equivalent backend sessions and clears sessions when the backend changes', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ data: { maxUploadSizeBytes: 1024, allowedContentTypes: ['image/png'] } }),
    }));
    const config = useAppConfigStore();
    config.apiBaseUrl = 'https://api.example.com';
    const auth = useAuthStore();
    auth.applySession({ accessToken: 'a', refreshToken: 'r', user: {
      id: 'u-1', username: 'alice', role: 'user', isActive: true, permissions: [],
    } });
    await config.setConfig({ apiBaseUrl: 'https://API.example.com:443/' });
    expect(auth.isAuthenticated).toBe(true);
    await config.setConfig({ apiBaseUrl: 'https://other.example.com' });
    expect(auth.isAuthenticated).toBe(false);
    expect(localStorage.getItem('nekohub.auth-session')).toBeNull();
  });

});
