import { defineStore } from 'pinia';
import axios from 'axios';
import type {
  AuthTokenResponse,
  AuthTokens,
  AuthenticatedUser,
  LoginRequest,
  PermissionKey,
  UserRole,
} from '../types/auth';
import { readFromLocalStorage, writeToLocalStorage } from '../utils/local-storage';
import {
  getCurrentUser,
  login as loginApi,
  logout as logoutApi,
  refreshToken as refreshTokenApi,
} from '../api/auth/auth.api';

const AUTH_STORAGE_KEY = 'nekohub.auth-session';

interface PersistedAuthState {
  accessToken: string;
  refreshToken: string;
  user: AuthenticatedUser | null;
}

interface AuthState {
  accessToken: string;
  refreshToken: string;
  user: AuthenticatedUser | null;
  initialized: boolean;
}

function isBrowser(): boolean {
  return typeof window !== 'undefined';
}

function normalizeRole(role: UserRole): UserRole {
  if (typeof role !== 'string') {
    return 'user';
  }

  const normalized = role.trim().toLowerCase();
  if (normalized === 'superadmin' || normalized === 'super_admin') {
    return 'superAdmin';
  }
  if (normalized === 'admin') {
    return 'admin';
  }

  if (normalized === 'user') {
    return 'user';
  }

  return role;
}

function normalizeUser(user: AuthenticatedUser | null): AuthenticatedUser | null {
  if (!user) {
    return null;
  }

  // 后端角色值在不同阶段可能出现大小写或别名差异，这里统一收敛成前端内部约定。
  return {
    ...user,
    role: normalizeRole(user.role),
    permissions: Array.isArray(user.permissions) ? [...new Set(user.permissions)] : [],
  };
}

function readPersistedState(): PersistedAuthState | null {
  return readFromLocalStorage<PersistedAuthState>(AUTH_STORAGE_KEY);
}

function writePersistedState(state: PersistedAuthState): void {
  writeToLocalStorage<PersistedAuthState>(AUTH_STORAGE_KEY, state);
}

function clearPersistedState(): void {
  if (!isBrowser()) {
    return;
  }

  window.localStorage.removeItem(AUTH_STORAGE_KEY);
}

function shouldClearSessionForError(error: unknown): boolean {
  return axios.isAxiosError(error) && error.response?.status === 401;
}

export const useAuthStore = defineStore('auth', {
  state: (): AuthState => ({
    accessToken: '',
    refreshToken: '',
    user: null,
    initialized: false,
  }),
  getters: {
    isAuthenticated: (state) => state.accessToken.length > 0 && state.refreshToken.length > 0,
    username: (state) => state.user?.username ?? '',
    role: (state) => normalizeRole(state.user?.role ?? 'user'),
    permissions: (state) => state.user?.permissions ?? [],
  },
  actions: {
    hydrate() {
      // hydrate 只恢复本地会话快照，不在这里判断 access token 是否仍然有效。
      const persisted = readPersistedState();
      if (!persisted) {
        this.initialized = true;
        return;
      }

      this.accessToken = persisted.accessToken?.trim() ?? '';
      this.refreshToken = persisted.refreshToken?.trim() ?? '';
      this.user = normalizeUser(persisted.user);
      this.initialized = true;
    },
    persist() {
      if (!this.isAuthenticated) {
        clearPersistedState();
        return;
      }

      writePersistedState({
        accessToken: this.accessToken,
        refreshToken: this.refreshToken,
        user: this.user,
      });
    },
    applySession(payload: AuthTokenResponse) {
      const tokens: AuthTokens = {
        accessToken: payload.accessToken?.trim() ?? '',
        refreshToken: payload.refreshToken?.trim() ?? '',
      };

      this.accessToken = tokens.accessToken;
      this.refreshToken = tokens.refreshToken;
      this.user = normalizeUser(payload.user);
      this.persist();
    },
    clearSession() {
      this.accessToken = '';
      this.refreshToken = '';
      this.user = null;
      clearPersistedState();
    },
    hasPermission(permission: PermissionKey): boolean {
      if (!this.user) {
        return false;
      }

      if (normalizeRole(this.user.role) === 'superAdmin') {
        return true;
      }

      return this.permissions.includes(permission);
    },
    hasAnyPermission(permissions: PermissionKey[]): boolean {
      if (permissions.length === 0) {
        return true;
      }

      return permissions.some((permission) => this.hasPermission(permission));
    },
    async login(request: LoginRequest) {
      const response = await loginApi(request);
      this.applySession(response);
      if (!this.user) {
        await this.fetchCurrentUser();
      }
    },
    async refreshSession(): Promise<string | null> {
      if (!this.refreshToken) {
        return null;
      }

      try {
        const response = await refreshTokenApi({ refreshToken: this.refreshToken });
        this.applySession(response);
        if (!this.user) {
          await this.fetchCurrentUser();
        }

        return this.accessToken;
      } catch (error) {
        // refresh 被服务端明确拒绝时，说明本地整套会话已经不可恢复，直接清空。
        if (shouldClearSessionForError(error)) {
          this.clearSession();
        }

        return null;
      }
    },
    async fetchCurrentUser(): Promise<AuthenticatedUser | null> {
      if (!this.accessToken) {
        return null;
      }

      const user = await getCurrentUser();
      this.user = normalizeUser(user);
      this.persist();
      return this.user;
    },
    async bootstrapSession() {
      if (!this.initialized) {
        this.hydrate();
      }

      if (!this.isAuthenticated) {
        return;
      }

      try {
        await this.fetchCurrentUser();
      } catch (error) {
        if (axios.isAxiosError(error) && error.response?.status === 401) {
          // 页面刷新后常见场景是 access token 过期但 refresh token 仍有效，这里先尝试静默续期一次。
          const refreshedToken = await this.refreshSession();
          if (!refreshedToken) {
            return;
          }

          try {
            await this.fetchCurrentUser();
          } catch (retryError) {
            if (shouldClearSessionForError(retryError)) {
              this.clearSession();
            }
          }
          return;
        }

        if (shouldClearSessionForError(error)) {
          this.clearSession();
        }
      }
    },
    async logout() {
      const refreshToken = this.refreshToken;
      if (refreshToken && this.accessToken) {
        try {
          await logoutApi({ refreshToken });
        } catch {
          // 登出接口失败不阻塞本地退出，避免服务端瞬时异常导致用户卡在伪登录状态。
        }
      }

      this.clearSession();
    },
  },
});
