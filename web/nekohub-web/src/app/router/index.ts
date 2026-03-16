import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router';
import AppLayout from '../../layouts/AppLayout.vue';
import PublicLayout from '../../layouts/PublicLayout.vue';
import SettingsPage from '../../pages/settings/SettingsPage.vue';
import ProvidersPage from '../../pages/providers/ProvidersPage.vue';
import AiProvidersPage from '../../pages/ai/AiProvidersPage.vue';
import AssetListPage from '../../pages/assets/AssetListPage.vue';
import AssetUploadPage from '../../pages/assets/AssetUploadPage.vue';
import AssetDetailPage from '../../pages/assets/AssetDetailPage.vue';
import GalleryListPage from '../../pages/gallery/GalleryListPage.vue';
import GalleryDetailPage from '../../pages/gallery/GalleryDetailPage.vue';
import LoginPage from '../../pages/auth/LoginPage.vue';
import UsersPage from '../../pages/users/UsersPage.vue';
import WorkflowEditorPage from '../../pages/workflows/WorkflowEditorPage.vue';
import { useAuthStore } from '../../stores/auth.store';
import { PERMISSIONS } from '../../constants/permissions';
import type { PermissionKey } from '../../types/auth';

interface AppRouteMeta {
  titleKey?: string;
  navKey?: string;
  requiresAuth?: boolean;
  requiredPermissions?: PermissionKey[];
  publicOnly?: boolean;
}

function resolveDefaultAuthenticatedPath(): string {
  const authStore = useAuthStore();
  // 登录后的默认落点按权限能力递减选择，避免把用户送到自己无权访问的首页。
  if (authStore.hasPermission(PERMISSIONS.assetsRead)) {
    return '/assets';
  }

  if (authStore.hasPermission(PERMISSIONS.providersRead)) {
    return '/providers';
  }

  if (authStore.hasPermission(PERMISSIONS.aiProvidersRead)) {
    return '/ai-providers';
  }

  if (authStore.hasPermission(PERMISSIONS.settingsRead)) {
    return '/settings';
  }

  if (authStore.hasPermission(PERMISSIONS.usersRead)) {
    return '/users';
  }

  return '/gallery';
}

const routes: RouteRecordRaw[] = [
  {
    path: '/',
    redirect: '/gallery',
  },
  {
    path: '/login',
    name: 'login',
    component: LoginPage,
    meta: {
      titleKey: 'auth.login.title',
      publicOnly: true,
    } satisfies AppRouteMeta,
  },
  {
    path: '/gallery',
    component: PublicLayout,
    children: [
      {
        path: '',
        name: 'gallery-list',
        component: GalleryListPage,
      },
      {
        path: ':id',
        name: 'gallery-detail',
        component: GalleryDetailPage,
      },
    ],
  },
  {
    path: '/',
    component: AppLayout,
    children: [
      {
        path: 'workflows',
        name: 'workflows',
        component: WorkflowEditorPage,
        meta: {
          titleKey: 'workflows.title',
          navKey: '/workflows',
          requiresAuth: true,
          requiredPermissions: [PERMISSIONS.settingsRead],
        },
      },
      {
        path: 'settings',
        name: 'settings',
        component: SettingsPage,
        meta: {
          titleKey: 'settings.title',
          navKey: '/settings',
          requiresAuth: true,
          requiredPermissions: [PERMISSIONS.settingsRead],
        },
      },
      {
        path: 'providers',
        name: 'providers',
        component: ProvidersPage,
        meta: {
          titleKey: 'providers.title',
          navKey: '/providers',
          requiresAuth: true,
          requiredPermissions: [PERMISSIONS.providersRead],
        },
      },
      {
        path: 'ai-providers',
        name: 'ai-providers',
        component: AiProvidersPage,
        meta: {
          titleKey: 'aiProviders.title',
          navKey: '/ai-providers',
          requiresAuth: true,
          requiredPermissions: [PERMISSIONS.aiProvidersRead],
        },
      },
      {
        path: 'assets',
        name: 'asset-list',
        component: AssetListPage,
        meta: {
          titleKey: 'asset.list.title',
          navKey: '/assets',
          requiresAuth: true,
          requiredPermissions: [PERMISSIONS.assetsRead],
        },
      },
      {
        path: 'assets/upload',
        name: 'asset-upload',
        component: AssetUploadPage,
        meta: {
          titleKey: 'asset.upload.title',
          navKey: '/assets/upload',
          requiresAuth: true,
          requiredPermissions: [PERMISSIONS.assetsCreate],
        },
      },
      {
        path: 'assets/:id',
        name: 'asset-detail',
        component: AssetDetailPage,
        meta: {
          titleKey: 'asset.detail.title',
          navKey: '/assets',
          requiresAuth: true,
          requiredPermissions: [PERMISSIONS.assetsRead],
        },
      },
      {
        path: 'users',
        name: 'users',
        component: UsersPage,
        meta: {
          titleKey: 'users.title',
          navKey: '/users',
          requiresAuth: true,
          requiredPermissions: [PERMISSIONS.usersRead],
        },
      },
    ],
  },
];

export const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
});

router.beforeEach((to) => {
  const authStore = useAuthStore();
  const meta = to.meta as AppRouteMeta;

  if (meta.publicOnly && authStore.isAuthenticated) {
    // 登录页等 publicOnly 路由在已登录状态下直接回跳，优先尊重显式 redirect。
    const redirect = typeof to.query.redirect === 'string' ? to.query.redirect : '';
    return redirect || resolveDefaultAuthenticatedPath();
  }

  if (!meta.requiresAuth) {
    return true;
  }

  if (!authStore.isAuthenticated) {
    return {
      path: '/login',
      query: {
        redirect: to.fullPath,
      },
    };
  }

  const requiredPermissions = meta.requiredPermissions ?? [];
  if (requiredPermissions.length > 0 && !authStore.hasAnyPermission(requiredPermissions)) {
    const fallbackPath = resolveDefaultAuthenticatedPath();
    // 若 fallback 仍落到当前无权限路由，则退回公开 gallery，避免守卫重定向打转。
    return fallbackPath === to.path ? '/gallery' : fallbackPath;
  }

  return true;
});
