import { beforeEach, describe, expect, it } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { router } from './index';
import { useAuthStore } from '../../stores/auth.store';
import { PERMISSIONS } from '../../constants/permissions';

function patchAuthenticatedUser(permissions: string[]): void {
  const authStore = useAuthStore();
  authStore.$patch({
    accessToken: 'access-token',
    refreshToken: 'refresh-token',
    user: {
      id: 'u-1',
      username: 'tester',
      role: 'user',
      isActive: true,
      permissions,
    },
  });
}

describe('app router auth guard', () => {
  beforeEach(async () => {
    setActivePinia(createPinia());
    const authStore = useAuthStore();
    authStore.clearSession();
    await router.push('/gallery');
  });

  it('redirects unauthenticated access to login with redirect query', async () => {
    await router.push('/assets');

    expect(router.currentRoute.value.path).toBe('/login');
    expect(router.currentRoute.value.query.redirect).toBe('/assets');
  });

  it('allows authenticated user with required permission', async () => {
    patchAuthenticatedUser([PERMISSIONS.assetsRead]);

    await router.push('/assets');

    expect(router.currentRoute.value.path).toBe('/assets');
  });

  it('redirects authenticated user without route permission to first allowed route', async () => {
    patchAuthenticatedUser([PERMISSIONS.assetsRead]);

    await router.push('/users');

    expect(router.currentRoute.value.path).toBe('/assets');
  });
});
