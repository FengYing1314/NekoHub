<script setup lang="ts">
import { reactive } from 'vue';
import { NButton, NCard, NForm, NFormItem, NInput, useMessage } from 'naive-ui';
import { useI18n } from 'vue-i18n';
import PageHeader from '../../components/common/PageHeader.vue';
import { useAppConfigStore } from '../../stores/app-config';

const { t } = useI18n();
const message = useMessage();
const appConfigStore = useAppConfigStore();

const formModel = reactive({
  apiBaseUrl: appConfigStore.apiBaseUrl,
  apiKey: appConfigStore.apiKey,
});

function handleSave(): void {
  appConfigStore.setConfig({
    apiBaseUrl: formModel.apiBaseUrl,
    apiKey: formModel.apiKey,
  });

  message.success(t('settings.saveSuccess'));
}
</script>

<template>
  <div>
    <page-header :title="t('settings.title')" :description="t('settings.description')" />

    <n-card>
      <n-form label-placement="top" :model="formModel">
        <n-form-item :label="t('settings.apiBaseUrl')">
          <n-input v-model:value="formModel.apiBaseUrl" :placeholder="t('settings.apiBaseUrlPlaceholder')" />
        </n-form-item>

        <n-form-item :label="t('settings.apiKey')">
          <n-input
            v-model:value="formModel.apiKey"
            type="password"
            show-password-on="mousedown"
            :placeholder="t('settings.apiKeyPlaceholder')"
          />
        </n-form-item>

        <n-button type="primary" @click="handleSave">{{ t('common.save') }}</n-button>
      </n-form>
    </n-card>
  </div>
</template>
