<script setup lang="ts">
import { computed, reactive, watch } from 'vue';
import {
  NButton,
  NForm,
  NFormItem,
  NInput,
  NModal,
  NSpace,
  useMessage,
} from 'naive-ui';
import { useI18n } from 'vue-i18n';
import {
  useAppConfigStore,
  validateAppConfigPayload,
} from '../../stores/app-config';

const { t } = useI18n();
const message = useMessage();
const appConfigStore = useAppConfigStore();

const formModel = reactive({
  apiBaseUrl: '',
  apiKey: '',
});

const validationState = reactive({
  apiBaseUrlMissing: false,
  apiKeyMissing: false,
});

const showRequiredConfigModal = computed(() => !appConfigStore.isConfigured);

function syncFormFromStore(): void {
  formModel.apiBaseUrl = appConfigStore.apiBaseUrl;
  formModel.apiKey = appConfigStore.apiKey;
  validationState.apiBaseUrlMissing = false;
  validationState.apiKeyMissing = false;
}

function validateForm(): boolean {
  const result = validateAppConfigPayload({
    apiBaseUrl: formModel.apiBaseUrl,
    apiKey: formModel.apiKey,
  });

  validationState.apiBaseUrlMissing = result.apiBaseUrlMissing;
  validationState.apiKeyMissing = result.apiKeyMissing;

  if (result.apiBaseUrlMissing || result.apiKeyMissing) {
    message.error(t('settings.validation.fixErrors'));
    return false;
  }

  return true;
}

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
  if (!validateForm()) {
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

watch(
  () => showRequiredConfigModal.value,
  (show) => {
    if (show) {
      syncFormFromStore();
    }
  },
  { immediate: true },
);
</script>

<template>
  <n-modal
    :show="showRequiredConfigModal"
    preset="card"
    :mask-closable="false"
    :close-on-esc="false"
    :closable="false"
    :auto-focus="true"
    class="required-config-modal"
  >
    <template #header>
      {{ t('settings.requiredModal.title') }}
    </template>

    <n-space vertical :size="16">
      <div class="required-config-modal__description">
        {{ t('settings.requiredModal.description') }}
      </div>

      <n-form label-placement="top">
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

        <n-button type="primary" block @click="handleSave">{{ t('common.save') }}</n-button>
      </n-form>
    </n-space>
  </n-modal>
</template>

<style scoped>
.required-config-modal {
  width: min(520px, calc(100vw - 32px));
  max-height: calc(100vh - 24px);
  overflow: auto;
}

.required-config-modal__description {
  font-size: 14px;
  color: #4b5563;
  line-height: 1.6;
}

@media (max-width: 768px) {
  .required-config-modal {
    width: calc(100vw - 24px);
  }
}
</style>
