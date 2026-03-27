<script setup lang="ts">
import { reactive, watch } from 'vue';
import {
  NButton,
  NCard,
  NForm,
  NFormItem,
  NInput,
  NSpace,
  useMessage,
} from 'naive-ui';
import { useI18n } from 'vue-i18n';
import { useRouter } from 'vue-router';
import PageHeader from '../../components/common/PageHeader.vue';
import {
  useAppConfigStore,
  validateAppConfigPayload,
} from '../../stores/app-config';

const { t } = useI18n();
const message = useMessage();
const router = useRouter();
const appConfigStore = useAppConfigStore();

const formModel = reactive({
  apiBaseUrl: appConfigStore.apiBaseUrl,
  apiKey: appConfigStore.apiKey,
});

const validationState = reactive({
  apiBaseUrlMissing: false,
  apiKeyMissing: false,
});

function handleApiBaseUrlInput(value: string): void {
  formModel.apiBaseUrl = value;
  if (validationState.apiBaseUrlMissing && value.trim().length > 0) {
    validationState.apiBaseUrlMissing = false;
  }
}

function handleApiKeyInput(value: string): void {
  formModel.apiKey = value;
  if (validationState.apiKeyMissing && value.trim().length > 0) {
    validationState.apiKeyMissing = false;
  }
}

function handleSave(): void {
  const result = validateAppConfigPayload({
    apiBaseUrl: formModel.apiBaseUrl,
    apiKey: formModel.apiKey,
  });

  validationState.apiBaseUrlMissing = result.apiBaseUrlMissing;
  validationState.apiKeyMissing = result.apiKeyMissing;

  if (result.apiBaseUrlMissing || result.apiKeyMissing) {
    message.error(t('settings.validation.fixErrors'));
    return;
  }

  try {
    appConfigStore.setConfig({
      apiBaseUrl: formModel.apiBaseUrl,
      apiKey: formModel.apiKey,
    });

    message.success(t('settings.saveSuccess'));
  } catch (error) {
    const errorMessage = error instanceof Error ? error.message : t('settings.saveFailed');
    message.error(`${t('settings.saveFailed')}: ${errorMessage}`);
  }
}

function openProvidersPage(): void {
  void router.push('/providers');
}

watch(
  () => [appConfigStore.apiBaseUrl, appConfigStore.apiKey] as const,
  ([apiBaseUrl, apiKey]) => {
    formModel.apiBaseUrl = apiBaseUrl;
    formModel.apiKey = apiKey;
  },
);
</script>

<template>
  <div>
    <page-header :title="t('settings.title')" :description="t('settings.description')" />

    <n-card class="settings-card">
      <n-form label-placement="top" :model="formModel">
        <n-form-item
          :label="t('settings.apiBaseUrl')"
          :validation-status="validationState.apiBaseUrlMissing ? 'error' : undefined"
          :feedback="validationState.apiBaseUrlMissing ? t('settings.validation.apiBaseUrlRequired') : undefined"
        >
          <n-input
            :value="formModel.apiBaseUrl"
            :placeholder="t('settings.apiBaseUrlPlaceholder')"
            @update:value="handleApiBaseUrlInput"
          />
        </n-form-item>

        <n-form-item
          :label="t('settings.apiKey')"
          :validation-status="validationState.apiKeyMissing ? 'error' : undefined"
          :feedback="validationState.apiKeyMissing ? t('settings.validation.apiKeyRequired') : undefined"
        >
          <n-input
            :value="formModel.apiKey"
            type="password"
            show-password-on="mousedown"
            :placeholder="t('settings.apiKeyPlaceholder')"
            @update:value="handleApiKeyInput"
          />
        </n-form-item>

        <div class="settings-actions">
          <n-button type="primary" @click="handleSave">{{ t('common.save') }}</n-button>
        </div>
      </n-form>
    </n-card>

    <n-card class="settings-card" :title="t('settings.providersEntry.title')">
      <n-space vertical :size="10">
        <div class="providers-entry-description">{{ t('settings.providersEntry.description') }}</div>
        <n-space :wrap="true" align="center">
          <n-button type="primary" secondary @click="openProvidersPage">
            {{ t('settings.providersEntry.openAction') }}
          </n-button>
          <span class="providers-entry-hint">{{ t('settings.providersEntry.hint') }}</span>
        </n-space>
      </n-space>
    </n-card>
  </div>
</template>

<style scoped>
.settings-card + .settings-card {
  margin-top: 16px;
}

.providers-entry-description {
  color: #374151;
  line-height: 1.6;
}

.providers-entry-hint {
  color: #6b7280;
  font-size: 13px;
}

@media (max-width: 768px) {
  .settings-actions :deep(.n-button) {
    width: 100%;
  }

  .providers-entry-hint {
    display: block;
    width: 100%;
  }
}
</style>
