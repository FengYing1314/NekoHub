import { computed } from 'vue';
import { useAuthStore } from '../stores/auth.store';
import type { PermissionKey } from '../types/auth';

export function useAuthPermissions() {
  const authStore = useAuthStore();

  const isAuthenticated = computed(() => authStore.isAuthenticated);
  const userRole = computed(() => authStore.role);

  function can(permission: PermissionKey): boolean {
    return authStore.hasPermission(permission);
  }

  function canAny(permissions: PermissionKey[]): boolean {
    return authStore.hasAnyPermission(permissions);
  }

  return {
    authStore,
    isAuthenticated,
    userRole,
    can,
    canAny,
  };
}
