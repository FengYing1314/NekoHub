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
import { useRoute } from 'vue-router';
import {
  useAppConfigStore,
  validateAppConfigPayload,
} from '../../stores/app-config';

const { t } = useI18n();
const message = useMessage();
const route = useRoute();
const appConfigStore = useAppConfigStore();

const formModel = reactive({
  apiBaseUrl: '',
});

const validationState = reactive({
  apiBaseUrlMissing: false,
});

const isAdminRoute = computed(() => (
  route.path.startsWith('/assets')
  || route.path.startsWith('/settings')
  || route.path.startsWith('/providers')
  || route.path.startsWith('/ai-providers')
  || route.path.startsWith('/users')
));

const shouldBlockForMissingConfig = computed(() => (
  isAdminRoute.value && !appConfigStore.isConfigured
));
const showRequiredConfigModal = computed(() => (
  shouldBlockForMissingConfig.value || appConfigStore.isConfigModalOpen
));

function syncFormFromStore(): void {
  formModel.apiBaseUrl = appConfigStore.apiBaseUrl;
  validationState.apiBaseUrlMissing = false;
}

async function validateForm(): Promise<boolean> {
  const result = validateAppConfigPayload({
    apiBaseUrl: formModel.apiBaseUrl,
  }, {
    allowEmptyApiBaseUrl: true,
  });

  validationState.apiBaseUrlMissing = result.apiBaseUrlMissing;

  if (result.apiBaseUrlMissing) {
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

function handleModalVisibilityChange(show: boolean): void {
  if (!show && !shouldBlockForMissingConfig.value) {
    appConfigStore.closeConfigModal();
  }
}

async function handleSave(): Promise<void> {
  if (!await validateForm()) {
    return;
  }

  try {
    await appConfigStore.setConfig({
      apiBaseUrl: formModel.apiBaseUrl,
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
    :mask-closable="!shouldBlockForMissingConfig"
    :close-on-esc="!shouldBlockForMissingConfig"
    :closable="!shouldBlockForMissingConfig"
    :auto-focus="true"
    class="required-config-modal"
    @update:show="handleModalVisibilityChange"
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
        <n-space vertical :size="8">
          <n-button type="primary" block @click="handleSave">{{ t('common.save') }}</n-button>
          <n-button
            v-if="!shouldBlockForMissingConfig"
            block
            tertiary
            @click="appConfigStore.closeConfigModal()"
          >
            {{ t('common.cancel') }}
          </n-button>
        </n-space>
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
