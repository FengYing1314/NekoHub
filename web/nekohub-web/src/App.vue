<script setup lang="ts">
import {
  NConfigProvider,
  NDialogProvider,
  NGlobalStyle,
  NMessageProvider,
  NNotificationProvider,
} from 'naive-ui';
import RequiredConfigModal from './components/settings/RequiredConfigModal.vue';
import { themeOverrides } from './app/providers/naive-theme';
</script>

<template>
  <n-config-provider :theme-overrides="themeOverrides">
    <n-global-style />
    <n-dialog-provider>
      <n-notification-provider>
        <n-message-provider>
          <router-view />
          <required-config-modal />
        </n-message-provider>
      </n-notification-provider>
    </n-dialog-provider>
  </n-config-provider>
</template>
