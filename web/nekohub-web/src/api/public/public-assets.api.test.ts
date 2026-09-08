import { createPinia, setActivePinia } from 'pinia';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useAppConfigStore } from '../../stores/app-config';
import { listPublicAssets } from './public-assets.api';

afterEach(() => vi.unstubAllGlobals());

describe('public API backend', () => {
  it('uses the browser backend setting without sending session credentials', async () => {
    setActivePinia(createPinia());
    useAppConfigStore().apiBaseUrl = 'https://api.example.com/';
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => ({ data: { items: [] } }) });
    vi.stubGlobal('fetch', fetchMock);
    await listPublicAssets({ page: 1, pageSize: 20 });
    expect(fetchMock).toHaveBeenCalledWith('https://api.example.com/api/v1/public/assets?page=1&pageSize=20', {
      method: 'GET', credentials: 'omit', headers: { Accept: 'application/json' },
    });
  });
});
