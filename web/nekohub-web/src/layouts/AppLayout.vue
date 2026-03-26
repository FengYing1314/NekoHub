<script setup lang="ts">
import { computed } from 'vue';
import type { MenuOption } from 'naive-ui';
import { NLayout, NLayoutSider, NLayoutHeader, NLayoutContent, NMenu, NTag } from 'naive-ui';
import { useRoute, useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { useAppConfigStore } from '../stores/app-config';

const route = useRoute();
const router = useRouter();
const { t } = useI18n();
const appConfigStore = useAppConfigStore();

const menuOptions = computed<MenuOption[]>(() => [
  {
    key: '/assets',
    label: t('nav.assets'),
  },
  {
    key: '/assets/upload',
    label: t('nav.upload'),
  },
  {
    key: '/settings',
    label: t('nav.settings'),
  },
]);

const activeMenuKey = computed(() => {
  if (route.path.startsWith('/settings')) {
    return '/settings';
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

const apiKeyTagType = computed(() => (appConfigStore.isApiKeyConfigured ? 'success' : 'warning'));
const apiKeyLabel = computed(() =>
  appConfigStore.isApiKeyConfigured ? t('layout.apiKeyConfigured') : t('layout.apiKeyMissing'),
);

function handleMenuSelect(key: string | number): void {
  void router.push(String(key));
}
</script>

<template>
  <n-layout has-sider class="app-shell">
    <n-layout-sider
      bordered
      :width="220"
      collapse-mode="width"
      :collapsed-width="72"
      show-trigger="bar"
      :native-scrollbar="false"
      content-style="padding: 16px 8px"
    >
      <div class="brand">NekoHub</div>
      <n-menu :value="activeMenuKey" :options="menuOptions" @update:value="handleMenuSelect" />
    </n-layout-sider>

    <n-layout>
      <n-layout-header bordered class="app-header">
        <h2 class="app-header__title">{{ headerTitle }}</h2>
        <n-tag size="small" :type="apiKeyTagType" :bordered="false">{{ apiKeyLabel }}</n-tag>
      </n-layout-header>

      <n-layout-content content-style="padding: 20px;">
        <router-view />
      </n-layout-content>
    </n-layout>
  </n-layout>
</template>

<style scoped>
.app-shell {
  min-height: 100vh;
}

.brand {
  height: 40px;
  display: flex;
  align-items: center;
  padding: 0 12px;
  margin-bottom: 8px;
  font-size: 18px;
  font-weight: 700;
  color: #111827;
}

.app-header {
  height: 60px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 20px;
  background: #ffffff;
}

.app-header__title {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
  color: #111827;
}
</style>
