import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AxiosError, type AxiosResponse } from 'axios';
import { createPinia, setActivePinia } from 'pinia';
import { useAuthStore } from '../../stores/auth.store';

function createUnauthorizedError(url: string): AxiosError {
  const requestConfig = {
    url,
    headers: {},
  } as never;

  return new AxiosError(
    'Unauthorized',
    'ERR_BAD_REQUEST',
    requestConfig,
    undefined,
    {
      status: 401,
      statusText: 'Unauthorized',
      headers: {},
      config: requestConfig,
      data: {},
    },
  );
}

describe('http client refresh queue', () => {
  beforeEach(() => {
    vi.resetModules();
    setActivePinia(createPinia());
  });

  it('refreshes once for concurrent 401 requests and retries both requests once', async () => {
    const pinia = createPinia();
    setActivePinia(pinia);

    const { httpClient, setupHttpClientInterceptors } = await import('./http-client');
    const authStore = useAuthStore(pinia);
    authStore.$patch({
      accessToken: 'access-token-old',
      refreshToken: 'refresh-token',
      user: {
        id: 'u-1',
        username: 'alice',
        role: 'user',
        isActive: true,
        permissions: [],
      },
      initialized: true,
    });

    const refreshedAccessToken = 'access-token-new';
    const refreshSessionSpy = vi.spyOn(authStore, 'refreshSession').mockImplementation(async () => {
      await Promise.resolve();
      authStore.accessToken = refreshedAccessToken;
      return refreshedAccessToken;
    });
    const logoutSpy = vi.spyOn(authStore, 'logout').mockResolvedValue();
    const unauthorizedSpy = vi.fn();

    setupHttpClientInterceptors({
      pinia,
      onUnauthorized: unauthorizedSpy,
    });

    const requestSpy = vi.spyOn(httpClient, 'request').mockResolvedValue({
      status: 200,
      statusText: 'OK',
      headers: {},
      config: { url: '/api/v1/assets' },
      data: {
        data: {},
      },
    } as AxiosResponse);

    const handlers = (httpClient.interceptors.response as never as { handlers: Array<{ rejected: (error: unknown) => Promise<unknown> }> }).handlers;
    const rejectedHandler = handlers[handlers.length - 1]?.rejected;
    if (!rejectedHandler) {
      throw new Error('Expected response interceptor rejected handler to exist.');
    }

    await Promise.all([
      rejectedHandler(createUnauthorizedError('/api/v1/assets?page=1')),
      rejectedHandler(createUnauthorizedError('/api/v1/assets?page=2')),
    ]);

    expect(refreshSessionSpy).toHaveBeenCalledTimes(1);
    expect(requestSpy).toHaveBeenCalledTimes(2);
    expect((requestSpy.mock.calls[0]?.[0]?.headers as Record<string, string>).Authorization).toBe(`Bearer ${refreshedAccessToken}`);
    expect((requestSpy.mock.calls[1]?.[0]?.headers as Record<string, string>).Authorization).toBe(`Bearer ${refreshedAccessToken}`);
    expect(logoutSpy).not.toHaveBeenCalled();
    expect(unauthorizedSpy).not.toHaveBeenCalled();
  });

  it('keeps the session when refresh cannot complete because of a network failure', async () => {
    const pinia = createPinia();
    setActivePinia(pinia);

    const { httpClient, setupHttpClientInterceptors } = await import('./http-client');
    const authStore = useAuthStore(pinia);
    authStore.$patch({
      accessToken: 'access-token-old',
      refreshToken: 'refresh-token',
      user: {
        id: 'u-1',
        username: 'alice',
        role: 'user',
        isActive: true,
        permissions: [],
      },
      initialized: true,
    });

    const refreshSessionSpy = vi.spyOn(authStore, 'refreshSession').mockResolvedValue(null);
    const logoutSpy = vi.spyOn(authStore, 'logout').mockResolvedValue();
    const unauthorizedSpy = vi.fn();

    setupHttpClientInterceptors({
      pinia,
      onUnauthorized: unauthorizedSpy,
    });

    const handlers = (httpClient.interceptors.response as never as { handlers: Array<{ rejected: (error: unknown) => Promise<unknown> }> }).handlers;
    const rejectedHandler = handlers[handlers.length - 1]?.rejected;
    if (!rejectedHandler) {
      throw new Error('Expected response interceptor rejected handler to exist.');
    }

    await expect(rejectedHandler(createUnauthorizedError('/api/v1/assets?page=1'))).rejects.toBeInstanceOf(AxiosError);
    expect(refreshSessionSpy).toHaveBeenCalledTimes(1);
    expect(authStore.isAuthenticated).toBe(true);
    expect(logoutSpy).not.toHaveBeenCalled();
    expect(unauthorizedSpy).not.toHaveBeenCalled();
  });
});
