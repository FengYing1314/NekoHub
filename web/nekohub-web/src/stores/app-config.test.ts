import { createPinia, setActivePinia } from 'pinia';
import { beforeEach, describe, expect, it, vi } from 'vitest';
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
});
