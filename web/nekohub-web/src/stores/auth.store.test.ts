import { createPinia, setActivePinia } from 'pinia';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useAuthStore } from './auth.store';
import {
  getCurrentUser,
  login as loginApi,
  logout as logoutApi,
  refreshToken as refreshTokenApi,
} from '../api/auth/auth.api';

vi.mock('../api/auth/auth.api', () => ({
  login: vi.fn(),
  refreshToken: vi.fn(),
  logout: vi.fn(),
  getCurrentUser: vi.fn(),
}));

describe('auth store', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
    setActivePinia(createPinia());
  });

  it('logs in and persists tokens and user', async () => {
    vi.mocked(loginApi).mockResolvedValue({
      accessToken: 'token-a',
      refreshToken: 'token-r',
      user: {
        id: 'u-1',
        username: 'admin',
        role: 'superAdmin',
        isActive: true,
        permissions: [],
      },
    });

    const store = useAuthStore();
    await store.login({
      username: 'admin',
      password: 'pass',
    });

    expect(store.isAuthenticated).toBe(true);
    expect(store.username).toBe('admin');
    const persistedRaw = localStorage.getItem('nekohub.auth-session');
    expect(persistedRaw).toBeTruthy();
    expect(persistedRaw).toContain('token-a');
  });

  it('keeps the session when refresh fails without an auth error', async () => {
    const store = useAuthStore();
    store.$patch({
      accessToken: 'token-a',
      refreshToken: 'token-r',
      user: {
        id: 'u-1',
        username: 'operator',
        role: 'admin',
        isActive: true,
        permissions: ['assets.read'],
      },
      initialized: true,
    });

    vi.mocked(refreshTokenApi).mockRejectedValue(new Error('refresh failed'));

    const refreshed = await store.refreshSession();
    expect(refreshed).toBeNull();
    expect(store.isAuthenticated).toBe(true);
  });

  it('clears session when refresh fails with 401', async () => {
    const store = useAuthStore();
    store.$patch({
      accessToken: 'token-a',
      refreshToken: 'token-r',
      user: {
        id: 'u-1',
        username: 'operator',
        role: 'admin',
        isActive: true,
        permissions: ['assets.read'],
      },
      initialized: true,
    });

    vi.mocked(refreshTokenApi).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 401,
      },
    });

    const refreshed = await store.refreshSession();
    expect(refreshed).toBeNull();
    expect(store.isAuthenticated).toBe(false);
  });

  it('grants all permissions to super admin', () => {
    const store = useAuthStore();
    store.$patch({
      user: {
        id: 'u-1',
        username: 'super',
        role: 'superAdmin',
        isActive: true,
        permissions: [],
      },
    });

    expect(store.hasPermission('users.managePermissions')).toBe(true);
    expect(store.hasPermission('providers.delete')).toBe(true);
  });

  it('hydrates and bootstraps current user from token session', async () => {
    localStorage.setItem('nekohub.auth-session', JSON.stringify({
      accessToken: 'token-a',
      refreshToken: 'token-r',
      user: null,
    }));

    vi.mocked(getCurrentUser).mockResolvedValue({
      id: 'u-2',
      username: 'alice',
      role: 'user',
      isActive: true,
      permissions: ['assets.read'],
    });

    const store = useAuthStore();
    store.hydrate();
    await store.bootstrapSession();

    expect(store.isAuthenticated).toBe(true);
    expect(store.username).toBe('alice');
  });

  it('keeps the local session when bootstrap fails due to timeout', async () => {
    localStorage.setItem('nekohub.auth-session', JSON.stringify({
      accessToken: 'token-a',
      refreshToken: 'token-r',
      user: {
        id: 'u-2',
        username: 'alice',
        role: 'user',
        isActive: true,
        permissions: ['assets.read'],
      },
    }));

    vi.mocked(getCurrentUser).mockRejectedValue({
      isAxiosError: true,
      code: 'ECONNABORTED',
      message: 'timeout',
    });

    const store = useAuthStore();
    store.hydrate();
    await store.bootstrapSession();

    expect(store.isAuthenticated).toBe(true);
    expect(store.username).toBe('alice');
  });

  it('logout clears local session even if API call fails', async () => {
    vi.mocked(logoutApi).mockRejectedValue(new Error('network'));

    const store = useAuthStore();
    store.$patch({
      accessToken: 'token-a',
      refreshToken: 'token-r',
      user: {
        id: 'u-1',
        username: 'admin',
        role: 'admin',
        isActive: true,
        permissions: ['assets.read'],
      },
    });

    await store.logout();

    expect(store.isAuthenticated).toBe(false);
    expect(localStorage.getItem('nekohub.auth-session')).toBeNull();
  });
});
