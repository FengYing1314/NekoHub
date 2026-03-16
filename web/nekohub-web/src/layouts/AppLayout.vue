<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import {
  NButton,
  NDrawer,
  NDrawerContent,
  NLayout,
  NLayoutContent,
  NLayoutHeader,
  NLayoutSider,
  NSpace,
} from 'naive-ui';
import { useRoute, useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { useAuthStore } from '../stores/auth.store';
import { PERMISSIONS } from '../constants/permissions';
import type { PermissionKey } from '../types/auth';
import { readFromLocalStorage, writeToLocalStorage } from '../utils/local-storage';
import { useIsMobile } from '../composables/useIsMobile';

interface NavigationItem {
  key: string;
  label: string;
  shortLabel: string;
  requiredPermissions: PermissionKey[];
}

const SIDEBAR_COLLAPSED_STORAGE_KEY = 'nekohub.sidebar.collapsed';

const route = useRoute();
const router = useRouter();
const { t } = useI18n();
const authStore = useAuthStore();
const { isMobile } = useIsMobile();

const desktopSidebarCollapsed = ref(readFromLocalStorage<boolean>(SIDEBAR_COLLAPSED_STORAGE_KEY) ?? false);
const mobileDrawerVisible = ref(false);

const navigationItems = computed<NavigationItem[]>(() => [
  {
    key: '/assets',
    label: t('nav.assets'),
    shortLabel: t('navShort.assets'),
    requiredPermissions: [PERMISSIONS.assetsRead],
  },
  {
    key: '/assets/upload',
    label: t('nav.upload'),
    shortLabel: t('navShort.upload'),
    requiredPermissions: [PERMISSIONS.assetsCreate],
  },
  {
    key: '/providers',
    label: t('nav.providers'),
    shortLabel: t('navShort.providers'),
    requiredPermissions: [PERMISSIONS.providersRead],
  },
  {
    key: '/ai-providers',
    label: t('nav.aiProviders'),
    shortLabel: t('navShort.aiProviders'),
    requiredPermissions: [PERMISSIONS.aiProvidersRead],
  },
  {
    key: '/workflows',
    label: t('nav.workflows'),
    shortLabel: t('navShort.workflows'),
    requiredPermissions: [PERMISSIONS.settingsRead],
  },
  {
    key: '/settings',
    label: t('nav.settings'),
    shortLabel: t('navShort.settings'),
    requiredPermissions: [PERMISSIONS.settingsRead],
  },
  {
    key: '/users',
    label: t('nav.users'),
    shortLabel: t('navShort.users'),
    requiredPermissions: [PERMISSIONS.usersRead],
  },
].filter((item) => authStore.hasAnyPermission(item.requiredPermissions)));

const activeMenuKey = computed(() => {
  const navKey = route.meta.navKey;
  if (typeof navKey === 'string') {
    return navKey;
  }

  if (route.path.startsWith('/settings')) {
    return '/settings';
  }

  if (route.path.startsWith('/workflows')) {
    return '/workflows';
  }

  if (route.path.startsWith('/users')) {
    return '/users';
  }

  if (route.path.startsWith('/providers')) {
    return '/providers';
  }

  if (route.path.startsWith('/ai-providers')) {
    return '/ai-providers';
  }

  if (route.path.startsWith('/assets/upload')) {
    return '/assets/upload';
  }

  return '/assets';
});

const headerTitle = computed(() => {
  const titleKey = route.meta.titleKey;

  if (typeof titleKey === 'string') {
    return t(titleKey);
  }

  return t('layout.title');
});

const sidebarToggleLabel = computed(() => (
  isMobile.value
    ? t('layout.menu')
    : (desktopSidebarCollapsed.value ? t('layout.expandSidebar') : t('layout.collapseSidebar'))
));

const roleLabel = computed(() => {
  if (authStore.role === 'superAdmin' || authStore.role === 'admin' || authStore.role === 'user') {
    return t(`auth.role.${authStore.role}`);
  }

  return authStore.role;
});

watch(
  () => desktopSidebarCollapsed.value,
  (collapsed) => {
    writeToLocalStorage(SIDEBAR_COLLAPSED_STORAGE_KEY, collapsed);
  },
);

watch(
  () => route.fullPath,
  () => {
    if (isMobile.value) {
      mobileDrawerVisible.value = false;
    }
  },
);

watch(
  () => isMobile.value,
  (mobile) => {
    if (!mobile) {
      mobileDrawerVisible.value = false;
    }
  },
);

function handleNavigationSelect(key: string): void {
  if (isMobile.value) {
    mobileDrawerVisible.value = false;
  }

  void router.push(key);
}

function handleSidebarToggle(): void {
  if (isMobile.value) {
    mobileDrawerVisible.value = true;
    return;
  }

  desktopSidebarCollapsed.value = !desktopSidebarCollapsed.value;
}

async function handleLogout(): Promise<void> {
  await authStore.logout();
  const redirect = route.fullPath;
  await router.push({
    path: '/login',
    query: {
      redirect,
    },
  });
}
</script>

<template>
  <n-layout :has-sider="!isMobile" class="app-shell">
    <n-layout-sider
      v-if="!isMobile"
      bordered
      :width="220"
      :collapsed="desktopSidebarCollapsed"
      collapse-mode="width"
      :collapsed-width="72"
      :native-scrollbar="false"
      content-style="padding: 16px 8px"
      class="app-sider"
    >
      <div class="brand" :class="{ 'brand--collapsed': desktopSidebarCollapsed }">
        {{ desktopSidebarCollapsed ? 'N' : 'NekoHub 管理台' }}
      </div>

      <nav class="nav-list" :class="{ 'nav-list--collapsed': desktopSidebarCollapsed }">
        <button
          v-for="item in navigationItems"
          :key="item.key"
          type="button"
          class="nav-item"
          :class="{
            'nav-item--active': activeMenuKey === item.key,
            'nav-item--collapsed': desktopSidebarCollapsed,
          }"
          :title="desktopSidebarCollapsed ? item.label : undefined"
          @click="handleNavigationSelect(item.key)"
        >
          <span class="nav-item__short">{{ item.shortLabel }}</span>
          <span v-if="!desktopSidebarCollapsed" class="nav-item__label">{{ item.label }}</span>
        </button>
      </nav>
    </n-layout-sider>

    <n-drawer v-model:show="mobileDrawerVisible" placement="left" :width="280">
      <n-drawer-content :title="t('layout.navigation')" closable body-content-style="padding: 16px 12px">
        <div class="brand">NekoHub 管理台</div>

        <nav class="nav-list nav-list--drawer">
          <button
            v-for="item in navigationItems"
            :key="item.key"
            type="button"
            class="nav-item"
            :class="{ 'nav-item--active': activeMenuKey === item.key }"
            @click="handleNavigationSelect(item.key)"
          >
            <span class="nav-item__short">{{ item.shortLabel }}</span>
            <span class="nav-item__label">{{ item.label }}</span>
          </button>
        </nav>
      </n-drawer-content>
    </n-drawer>

    <n-layout class="app-main">
      <n-layout-header bordered class="app-header">
        <div class="app-header__main">
          <n-button size="small" quaternary @click="handleSidebarToggle">
            {{ sidebarToggleLabel }}
          </n-button>
          <h2 class="app-header__title">{{ headerTitle }}</h2>
        </div>

        <n-space :size="8" align="center">
          <div class="user-identity">
            <span class="user-identity__name">{{ authStore.username }}</span>
            <span class="user-identity__role">{{ roleLabel }}</span>
          </div>
          <n-button size="small" @click="handleLogout">{{ t('auth.logout') }}</n-button>
        </n-space>
      </n-layout-header>

      <n-layout-content class="app-content">
        <router-view />
      </n-layout-content>
    </n-layout>
  </n-layout>
</template>

<style scoped>
.app-shell {
  min-height: 100vh;
}

.app-main {
  min-width: 0;
}

.app-sider {
  background:
    linear-gradient(180deg, rgba(255, 255, 255, 0.98), rgba(248, 250, 252, 0.96));
  box-shadow: inset -1px 0 0 rgba(226, 232, 240, 0.82);
}

.brand {
  height: 46px;
  display: flex;
  align-items: center;
  padding: 0 14px;
  margin-bottom: 14px;
  font-family: 'Sora', 'Noto Sans SC', sans-serif;
  font-size: 20px;
  font-weight: 700;
  letter-spacing: -0.04em;
  color: var(--app-text-strong);
}

.brand--collapsed {
  justify-content: center;
  padding: 0;
}

.nav-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.nav-item {
  width: 100%;
  border: 0;
  border-radius: 14px;
  background: transparent;
  color: #334155;
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 11px 12px;
  font: inherit;
  cursor: pointer;
  transition: background-color 0.2s ease, color 0.2s ease, transform 0.2s ease, box-shadow 0.2s ease;
}

.nav-item:hover {
  transform: translateX(1px);
  background: rgba(37, 99, 235, 0.08);
}

.nav-item--active {
  background:
    linear-gradient(135deg, rgba(37, 99, 235, 0.14), rgba(59, 130, 246, 0.08));
  color: #1744a7;
  font-weight: 600;
  box-shadow: inset 0 0 0 1px rgba(147, 197, 253, 0.72);
}

.nav-item--collapsed {
  justify-content: center;
  padding: 10px 0;
}

.nav-item__short {
  width: 28px;
  height: 28px;
  border-radius: 10px;
  background: rgba(226, 232, 240, 0.7);
  color: inherit;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 13px;
  font-weight: 700;
  flex-shrink: 0;
}

.nav-item--active .nav-item__short {
  background: rgba(191, 219, 254, 0.92);
}

.nav-item__label {
  min-width: 0;
  text-align: left;
}

.app-header {
  min-height: 68px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 0 20px;
  background: rgba(255, 255, 255, 0.78);
  backdrop-filter: blur(16px);
}

.app-header__main {
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 12px;
}

.app-header__title {
  margin: 0;
  font-size: 20px;
  font-weight: 700;
  letter-spacing: -0.02em;
  color: var(--app-text-strong);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.app-content {
  padding: 20px;
  min-width: 0;
}

.user-identity {
  display: inline-flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 2px;
}

.user-identity__name {
  font-size: 13px;
  line-height: 1.2;
  font-weight: 600;
  color: var(--app-text-strong);
}

.user-identity__role {
  font-size: 12px;
  line-height: 1.2;
  color: var(--app-text-muted);
}

@media (max-width: 768px) {
  .app-header {
    padding: 12px;
    min-height: unset;
    align-items: flex-start;
    flex-wrap: wrap;
  }

  .app-header__main {
    width: 100%;
    align-items: center;
  }

  .app-header__title {
    white-space: normal;
    overflow: visible;
    text-overflow: clip;
    font-size: 17px;
  }

  .user-identity {
    align-items: flex-start;
  }

  .app-content {
    padding: 16px 12px;
  }

  .nav-item {
    padding: 12px;
  }
}
</style>
