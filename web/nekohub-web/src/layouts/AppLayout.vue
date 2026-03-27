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
  NTag,
} from 'naive-ui';
import { useRoute, useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { useAppConfigStore } from '../stores/app-config';
import { readFromLocalStorage, writeToLocalStorage } from '../utils/local-storage';
import { useIsMobile } from '../composables/useIsMobile';

interface NavigationItem {
  key: string;
  label: string;
  shortLabel: string;
}

const SIDEBAR_COLLAPSED_STORAGE_KEY = 'nekohub.sidebar.collapsed';

const route = useRoute();
const router = useRouter();
const { t } = useI18n();
const appConfigStore = useAppConfigStore();
const { isMobile } = useIsMobile();

const desktopSidebarCollapsed = ref(readFromLocalStorage<boolean>(SIDEBAR_COLLAPSED_STORAGE_KEY) ?? false);
const mobileDrawerVisible = ref(false);

const navigationItems = computed<NavigationItem[]>(() => [
  {
    key: '/assets',
    label: t('nav.assets'),
    shortLabel: t('navShort.assets'),
  },
  {
    key: '/assets/upload',
    label: t('nav.upload'),
    shortLabel: t('navShort.upload'),
  },
  {
    key: '/providers',
    label: t('nav.providers'),
    shortLabel: t('navShort.providers'),
  },
  {
    key: '/settings',
    label: t('nav.settings'),
    shortLabel: t('navShort.settings'),
  },
]);

const activeMenuKey = computed(() => {
  const navKey = route.meta.navKey;
  if (typeof navKey === 'string') {
    return navKey;
  }

  if (route.path.startsWith('/settings')) {
    return '/settings';
  }

  if (route.path.startsWith('/providers')) {
    return '/providers';
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

const apiKeyTagType = computed(() => (appConfigStore.isApiKeyConfigured ? 'success' : 'warning'));
const apiKeyLabel = computed(() =>
  appConfigStore.isApiKeyConfigured ? t('layout.apiKeyConfigured') : t('layout.apiKeyMissing'),
);

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
        {{ desktopSidebarCollapsed ? 'N' : 'NekoHub' }}
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
        <div class="brand">NekoHub</div>

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

        <n-tag size="small" :type="apiKeyTagType" :bordered="false">{{ apiKeyLabel }}</n-tag>
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
  background: #ffffff;
}

.brand {
  height: 40px;
  display: flex;
  align-items: center;
  padding: 0 12px;
  margin-bottom: 12px;
  font-size: 18px;
  font-weight: 700;
  color: #111827;
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
  border-radius: 10px;
  background: transparent;
  color: #374151;
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 12px;
  font: inherit;
  cursor: pointer;
  transition: background-color 0.2s ease, color 0.2s ease;
}

.nav-item:hover {
  background: #f3f4f6;
}

.nav-item--active {
  background: #e8f1ff;
  color: #0f3d99;
  font-weight: 600;
}

.nav-item--collapsed {
  justify-content: center;
  padding: 10px 0;
}

.nav-item__short {
  width: 28px;
  height: 28px;
  border-radius: 8px;
  background: #f3f4f6;
  color: inherit;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 13px;
  font-weight: 700;
  flex-shrink: 0;
}

.nav-item--active .nav-item__short {
  background: #dbeafe;
}

.nav-item__label {
  min-width: 0;
  text-align: left;
}

.app-header {
  min-height: 60px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 0 20px;
  background: #ffffff;
}

.app-header__main {
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 12px;
}

.app-header__title {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
  color: #111827;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.app-content {
  padding: 20px;
  min-width: 0;
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

  .app-content {
    padding: 16px 12px;
  }

  .nav-item {
    padding: 12px;
  }
}
</style>
