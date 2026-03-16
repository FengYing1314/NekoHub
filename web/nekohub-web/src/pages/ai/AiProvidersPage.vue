<script setup lang="ts">
import { computed, h, onMounted, reactive, ref, watch } from 'vue';
import type { DataTableColumns } from 'naive-ui';
import {
  NAlert,
  NButton,
  NCard,
  NDataTable,
  NEmpty,
  NForm,
  NFormItem,
  NInput,
  NModal,
  NResult,
  NSpace,
  NSwitch,
  NTag,
  useDialog,
  useMessage,
} from 'naive-ui';
import { useI18n } from 'vue-i18n';
import PageHeader from '../../components/common/PageHeader.vue';
import { extractApiErrorMessage } from '../../api/client/error';
import {
  createAiProviderProfile,
  deleteAiProviderProfile,
  listAiProviderProfiles,
  testAiProviderProfile,
  updateAiProviderProfile,
} from '../../api/system/ai-providers.api';
import { useIsMobile } from '../../composables/useIsMobile';
import type {
  AiProviderProfile,
  AiProviderProfileTestRequest,
  AiProviderProfileTestResponse,
  CreateAiProviderProfileRequest,
  UpdateAiProviderProfileRequest,
} from '../../types/ai-providers';
import { formatDateTime } from '../../utils/format';
import { useAuthPermissions } from '../../composables/useAuthPermissions';
import { PERMISSIONS } from '../../constants/permissions';

const TABLE_SCROLL_X = 1020;

type ProfileModalMode = 'create' | 'edit';

interface ValidationState {
  name: string;
  apiBaseUrl: string;
  apiKey: string;
  modelName: string;
}

const { t } = useI18n();
const message = useMessage();
const dialog = useDialog();
const { isMobile } = useIsMobile();
const { can } = useAuthPermissions();

const loading = ref(false);
const loadErrorMessage = ref('');
const profileModalVisible = ref(false);
const profileModalMode = ref<ProfileModalMode>('create');
const profileModalSubmitting = ref(false);
const profileModalTesting = ref(false);
const deletingProfileId = ref<string | null>(null);
const editingProfile = ref<AiProviderProfile | null>(null);
const currentMaskedApiKey = ref('');
const profiles = ref<AiProviderProfile[]>([]);
const latestTestResult = ref<AiProviderProfileTestResponse | null>(null);
const canCreateAiProviders = computed(() => can(PERMISSIONS.aiProvidersCreate));
const canUpdateAiProviders = computed(() => can(PERMISSIONS.aiProvidersUpdate));
const canDeleteAiProviders = computed(() => can(PERMISSIONS.aiProvidersDelete));
const isAiProvidersReadOnly = computed(() => (
  !canCreateAiProviders.value
  && !canUpdateAiProviders.value
  && !canDeleteAiProviders.value
));

const formModel = reactive({
  name: '',
  apiBaseUrl: '',
  apiKey: '',
  modelName: '',
  defaultSystemPrompt: '',
  isActive: false,
});

const validationState = reactive<ValidationState>({
  name: '',
  apiBaseUrl: '',
  apiKey: '',
  modelName: '',
});

const hasLoadError = computed(() => loadErrorMessage.value.length > 0);
const showLoadErrorResult = computed(() => hasLoadError.value && profiles.value.length === 0);
const isEmpty = computed(() => !loading.value && !hasLoadError.value && profiles.value.length === 0);
const isEditMode = computed(() => profileModalMode.value === 'edit');
const isModalBusy = computed(() => profileModalSubmitting.value || profileModalTesting.value);
const modalTitle = computed(() => (
  isEditMode.value
    ? t('aiProviders.modal.editTitle')
    : t('aiProviders.modal.createTitle')
));
const modalSubmitText = computed(() => (
  isEditMode.value
    ? t('aiProviders.actions.save')
    : t('aiProviders.actions.create')
));
const modalStyle = computed(() => ({
  width: isMobile.value ? 'calc(100vw - 20px)' : '720px',
}));
const apiKeyPlaceholder = computed(() => (
  isEditMode.value
    ? t('aiProviders.form.apiKeyPlaceholderEdit')
    : t('aiProviders.form.apiKeyPlaceholderCreate')
));
const apiKeyHint = computed(() => {
  if (!isEditMode.value) {
    return t('aiProviders.form.apiKeyCreateHint');
  }

  if (currentMaskedApiKey.value) {
    return t('aiProviders.form.apiKeyMaskedHint', {
      maskedKey: currentMaskedApiKey.value,
    });
  }

  return t('aiProviders.form.apiKeyOptionalHint');
});

const sortedProfiles = computed(() => [...profiles.value].sort((left, right) => {
  if (left.isActive !== right.isActive) {
    return left.isActive ? -1 : 1;
  }

  return left.name.localeCompare(right.name, 'zh-CN');
}));

const columns = computed<DataTableColumns<AiProviderProfile>>(() => [
  {
    title: t('aiProviders.table.columns.name'),
    key: 'name',
    minWidth: 180,
    ellipsis: {
      tooltip: true,
    },
  },
  {
    title: t('aiProviders.table.columns.modelName'),
    key: 'modelName',
    minWidth: 180,
    ellipsis: {
      tooltip: true,
    },
  },
  {
    title: t('aiProviders.table.columns.apiBaseUrl'),
    key: 'apiBaseUrl',
    minWidth: 240,
    ellipsis: {
      tooltip: true,
    },
  },
  {
    title: t('aiProviders.table.columns.isActive'),
    key: 'isActive',
    width: 120,
    render: (row) => h(
      NTag,
      {
        size: 'small',
        type: row.isActive ? 'success' : 'default',
        bordered: false,
      },
      {
        default: () => (row.isActive ? t('aiProviders.status.active') : t('aiProviders.status.inactive')),
      },
    ),
  },
  {
    title: t('aiProviders.table.columns.updatedAt'),
    key: 'updatedAtUtc',
    width: 180,
    render: (row) => formatDateTime(row.updatedAtUtc),
  },
  {
    title: t('aiProviders.table.columns.actions'),
    key: 'actions',
    width: 170,
    render: (row) => h(
      NSpace,
      { size: 8 },
      {
        default: () => [
          h(
            NButton,
              {
                size: 'small',
                quaternary: true,
                disabled: profileModalSubmitting.value || deletingProfileId.value === row.id || !canUpdateAiProviders.value,
                onClick: () => openEditModal(row),
              },
              {
                default: () => t('aiProviders.actions.edit'),
              },
          ),
          h(
            NButton,
            {
              size: 'small',
                quaternary: true,
                type: 'error',
                loading: deletingProfileId.value === row.id,
                disabled: profileModalSubmitting.value || !canDeleteAiProviders.value,
                onClick: () => confirmDelete(row),
              },
              {
                default: () => t('aiProviders.actions.delete'),
              },
          ),
        ],
      },
    ),
  },
]);

function clearValidationState(): void {
  validationState.name = '';
  validationState.apiBaseUrl = '';
  validationState.apiKey = '';
  validationState.modelName = '';
}

function clearTestState(): void {
  latestTestResult.value = null;
}

function resetForm(): void {
  formModel.name = '';
  formModel.apiBaseUrl = '';
  formModel.apiKey = '';
  formModel.modelName = '';
  formModel.defaultSystemPrompt = '';
  formModel.isActive = profiles.value.every((profile) => !profile.isActive);
  currentMaskedApiKey.value = '';
  editingProfile.value = null;
  clearValidationState();
  clearTestState();
}

function normalizeRequiredValue(value: string): string {
  return value.trim();
}

function normalizeOptionalValue(value: string): string | undefined {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : undefined;
}

function isAbsoluteHttpUrl(value: string): boolean {
  try {
    const parsed = new URL(value);
    return parsed.protocol === 'http:' || parsed.protocol === 'https:';
  } catch {
    return false;
  }
}

function validateForm(): boolean {
  clearValidationState();

  const normalizedName = normalizeRequiredValue(formModel.name);
  const normalizedApiBaseUrl = normalizeRequiredValue(formModel.apiBaseUrl);
  const normalizedApiKey = normalizeOptionalValue(formModel.apiKey);
  const normalizedModelName = normalizeRequiredValue(formModel.modelName);

  if (!normalizedName) {
    validationState.name = t('aiProviders.validation.nameRequired');
  }

  if (!normalizedApiBaseUrl) {
    validationState.apiBaseUrl = t('aiProviders.validation.apiBaseUrlRequired');
  } else if (!isAbsoluteHttpUrl(normalizedApiBaseUrl)) {
    validationState.apiBaseUrl = t('aiProviders.validation.apiBaseUrlInvalid');
  }

  if (!isEditMode.value && !normalizedApiKey) {
    validationState.apiKey = t('aiProviders.validation.apiKeyRequired');
  }

  if (!normalizedModelName) {
    validationState.modelName = t('aiProviders.validation.modelNameRequired');
  }

  return Object.values(validationState).every((value) => value.length === 0);
}

function validateTestForm(): boolean {
  validationState.apiBaseUrl = '';
  validationState.apiKey = '';
  validationState.modelName = '';

  const normalizedApiBaseUrl = normalizeRequiredValue(formModel.apiBaseUrl);
  const normalizedApiKey = normalizeOptionalValue(formModel.apiKey);
  const normalizedModelName = normalizeRequiredValue(formModel.modelName);

  if (!normalizedApiBaseUrl) {
    validationState.apiBaseUrl = t('aiProviders.validation.apiBaseUrlRequired');
  } else if (!isAbsoluteHttpUrl(normalizedApiBaseUrl)) {
    validationState.apiBaseUrl = t('aiProviders.validation.apiBaseUrlInvalid');
  }

  if (!isEditMode.value && !normalizedApiKey) {
    validationState.apiKey = t('aiProviders.validation.apiKeyRequired');
  }

  if (!normalizedModelName) {
    validationState.modelName = t('aiProviders.validation.modelNameRequired');
  }

  return !validationState.apiBaseUrl && !validationState.apiKey && !validationState.modelName;
}

function buildCreateRequest(): CreateAiProviderProfileRequest {
  const normalizedDefaultSystemPrompt = normalizeOptionalValue(formModel.defaultSystemPrompt);

  return {
    name: normalizeRequiredValue(formModel.name),
    apiBaseUrl: normalizeRequiredValue(formModel.apiBaseUrl).replace(/\/+$/, ''),
    apiKey: normalizeRequiredValue(formModel.apiKey),
    modelName: normalizeRequiredValue(formModel.modelName),
    defaultSystemPrompt: normalizedDefaultSystemPrompt,
    isActive: formModel.isActive,
  };
}

function buildUpdateRequest(profile: AiProviderProfile): UpdateAiProviderProfileRequest {
  const request: UpdateAiProviderProfileRequest = {};
  const normalizedName = normalizeRequiredValue(formModel.name);
  const normalizedApiBaseUrl = normalizeRequiredValue(formModel.apiBaseUrl).replace(/\/+$/, '');
  const normalizedModelName = normalizeRequiredValue(formModel.modelName);
  const normalizedDefaultSystemPrompt = normalizeOptionalValue(formModel.defaultSystemPrompt);
  const normalizedApiKey = normalizeOptionalValue(formModel.apiKey);

  if (normalizedName !== profile.name) {
    request.name = normalizedName;
  }

  if (normalizedApiBaseUrl !== profile.apiBaseUrl) {
    request.apiBaseUrl = normalizedApiBaseUrl;
  }

  if (normalizedApiKey !== undefined) {
    request.apiKey = normalizedApiKey;
  }

  if (normalizedModelName !== profile.modelName) {
    request.modelName = normalizedModelName;
  }

  if (normalizedDefaultSystemPrompt === undefined) {
    if (profile.defaultSystemPrompt.length > 0) {
      request.defaultSystemPrompt = '';
    }
  } else if (normalizedDefaultSystemPrompt !== profile.defaultSystemPrompt) {
    request.defaultSystemPrompt = normalizedDefaultSystemPrompt;
  }

  if (formModel.isActive !== profile.isActive) {
    request.isActive = formModel.isActive;
  }

  return request;
}

function buildTestRequest(): AiProviderProfileTestRequest {
  const normalizedApiBaseUrl = normalizeOptionalValue(formModel.apiBaseUrl)?.replace(/\/+$/, '');
  const normalizedApiKey = normalizeOptionalValue(formModel.apiKey);
  const normalizedModelName = normalizeOptionalValue(formModel.modelName);
  const normalizedDefaultSystemPrompt = formModel.defaultSystemPrompt.trim();

  if (isEditMode.value && editingProfile.value) {
    return {
      profileId: editingProfile.value.id,
      apiBaseUrl: normalizedApiBaseUrl,
      apiKey: normalizedApiKey,
      modelName: normalizedModelName,
      defaultSystemPrompt: normalizedDefaultSystemPrompt,
    };
  }

  return {
    apiBaseUrl: normalizedApiBaseUrl,
    apiKey: normalizedApiKey,
    modelName: normalizedModelName,
    defaultSystemPrompt: normalizedDefaultSystemPrompt.length > 0
      ? normalizedDefaultSystemPrompt
      : undefined,
  };
}

async function loadProfiles(showErrorMessage = true): Promise<void> {
  loading.value = true;
  loadErrorMessage.value = '';

  try {
    profiles.value = await listAiProviderProfiles();
  } catch (error) {
    loadErrorMessage.value = extractApiErrorMessage(error);

    if (showErrorMessage) {
      message.error(`${t('aiProviders.messages.loadFailed')}: ${loadErrorMessage.value}`);
    }
  } finally {
    loading.value = false;
  }
}

function openCreateModal(): void {
  if (!canCreateAiProviders.value) {
    message.warning(t('auth.permissionDenied'));
    return;
  }

  profileModalMode.value = 'create';
  resetForm();
  profileModalVisible.value = true;
}

function openEditModal(profile: AiProviderProfile): void {
  if (!canUpdateAiProviders.value) {
    message.warning(t('auth.permissionDenied'));
    return;
  }

  profileModalMode.value = 'edit';
  editingProfile.value = profile;
  currentMaskedApiKey.value = profile.apiKey;
  clearValidationState();

  formModel.name = profile.name;
  formModel.apiBaseUrl = profile.apiBaseUrl;
  formModel.apiKey = '';
  formModel.modelName = profile.modelName;
  formModel.defaultSystemPrompt = profile.defaultSystemPrompt;
  formModel.isActive = profile.isActive;
  clearTestState();

  profileModalVisible.value = true;
}

function closeProfileModal(): void {
  if (isModalBusy.value) {
    return;
  }

  profileModalVisible.value = false;
}

async function submitProfileModal(): Promise<void> {
  if (!isEditMode.value && !canCreateAiProviders.value) {
    message.warning(t('auth.permissionDenied'));
    return;
  }

  if (isEditMode.value && !canUpdateAiProviders.value) {
    message.warning(t('auth.permissionDenied'));
    return;
  }

  if (!validateForm()) {
    message.error(t('aiProviders.validation.fixErrors'));
    return;
  }

  profileModalSubmitting.value = true;

  try {
    if (profileModalMode.value === 'create') {
      await createAiProviderProfile(buildCreateRequest());
      message.success(t('aiProviders.messages.createSuccess'));
    } else {
      const profile = editingProfile.value;
      if (!profile) {
        throw new Error(t('aiProviders.messages.missingEditingProfile'));
      }

      const request = buildUpdateRequest(profile);
      if (Object.keys(request).length === 0) {
        message.info(t('aiProviders.messages.noChanges'));
        profileModalVisible.value = false;
        return;
      }

      await updateAiProviderProfile(profile.id, request);
      message.success(t('aiProviders.messages.updateSuccess'));
    }

    profileModalVisible.value = false;
    await loadProfiles(false);
  } catch (error) {
    message.error(`${t('aiProviders.messages.saveFailed')}: ${extractApiErrorMessage(error)}`);
  } finally {
    profileModalSubmitting.value = false;
  }
}

async function handleProfileTest(): Promise<void> {
  if (!validateTestForm()) {
    message.error(t('aiProviders.validation.fixErrors'));
    return;
  }

  profileModalTesting.value = true;
  clearTestState();

  try {
    latestTestResult.value = await testAiProviderProfile(buildTestRequest());
    if (latestTestResult.value.succeeded) {
      message.success(t('aiProviders.messages.testSuccess'));
    } else {
      message.warning(t('aiProviders.messages.testFailed'));
    }
  } catch (error) {
    latestTestResult.value = {
      succeeded: false,
      caption: null,
      resolvedModelName: normalizeOptionalValue(formModel.modelName) ?? '-',
      resolvedApiBaseUrl: normalizeOptionalValue(formModel.apiBaseUrl)?.replace(/\/+$/, '') ?? '-',
      errorMessage: extractApiErrorMessage(error),
    };
    message.error(`${t('aiProviders.messages.testFailed')}: ${latestTestResult.value.errorMessage}`);
  } finally {
    profileModalTesting.value = false;
  }
}

async function handleDelete(profile: AiProviderProfile): Promise<void> {
  if (!canDeleteAiProviders.value) {
    message.warning(t('auth.permissionDenied'));
    return;
  }

  deletingProfileId.value = profile.id;

  try {
    await deleteAiProviderProfile(profile.id);
    message.success(t('aiProviders.messages.deleteSuccess'));
    await loadProfiles(false);
  } catch (error) {
    message.error(`${t('aiProviders.messages.deleteFailed')}: ${extractApiErrorMessage(error)}`);
  } finally {
    deletingProfileId.value = null;
  }
}

function confirmDelete(profile: AiProviderProfile): void {
  if (!canDeleteAiProviders.value) {
    message.warning(t('auth.permissionDenied'));
    return;
  }

  dialog.warning({
    title: t('aiProviders.actions.delete'),
    content: t('aiProviders.confirmations.delete', {
      name: profile.name,
    }),
    positiveText: t('common.delete'),
    negativeText: t('common.cancel'),
    onPositiveClick: () => handleDelete(profile),
  });
}

onMounted(() => {
  void loadProfiles();
});

watch(
  () => [
    formModel.name,
    formModel.apiBaseUrl,
    formModel.apiKey,
    formModel.modelName,
    formModel.defaultSystemPrompt,
    formModel.isActive,
  ],
  () => {
    if (profileModalVisible.value && latestTestResult.value) {
      clearTestState();
    }
  },
);
</script>

<template>
  <div>
    <page-header :title="t('aiProviders.title')" :description="t('aiProviders.description')">
      <template #actions>
        <n-button v-if="canCreateAiProviders" type="primary" @click="openCreateModal">{{ t('aiProviders.actions.create') }}</n-button>
      </template>
    </page-header>

    <n-alert v-if="isAiProvidersReadOnly" type="info" style="margin-bottom: 16px">
      {{ t('common.readOnlyNotice') }}
    </n-alert>

    <n-card class="section-card">
      <template #header>{{ t('aiProviders.table.title') }}</template>
      <template #header-extra>
        <n-button text :loading="loading" @click="loadProfiles()">
          {{ t('common.refresh') }}
        </n-button>
      </template>

      <n-space vertical :size="12">
        <n-alert v-if="hasLoadError && !showLoadErrorResult" type="warning" :show-icon="false">
          {{ t('aiProviders.messages.loadFailed') }}: {{ loadErrorMessage }}
        </n-alert>

        <n-result
          v-if="showLoadErrorResult"
          status="error"
          :title="t('aiProviders.messages.loadFailed')"
          :description="loadErrorMessage"
        >
          <template #footer>
            <n-button type="primary" @click="loadProfiles()">{{ t('common.retry') }}</n-button>
          </template>
        </n-result>

        <n-empty
          v-else-if="isEmpty"
          :description="t('aiProviders.table.empty')"
          style="padding: 40px 0"
        >
          <template #extra>
            <n-button v-if="canCreateAiProviders" type="primary" @click="openCreateModal">{{ t('aiProviders.actions.create') }}</n-button>
          </template>
        </n-empty>

        <div v-else class="table-wrapper">
          <n-data-table
            :loading="loading"
            :columns="columns"
            :data="sortedProfiles"
            :row-key="(row) => row.id"
            :scroll-x="TABLE_SCROLL_X"
          />
        </div>
      </n-space>
    </n-card>

    <n-modal
      v-model:show="profileModalVisible"
      preset="card"
      class="profile-modal"
      :style="modalStyle"
      :title="modalTitle"
      :mask-closable="!isModalBusy"
      :closable="!isModalBusy"
    >
      <n-form label-placement="top">
        <n-form-item
          :label="t('aiProviders.form.name')"
          :validation-status="validationState.name ? 'error' : undefined"
          :feedback="validationState.name || undefined"
        >
          <n-input
            v-model:value="formModel.name"
            :disabled="isModalBusy"
            :placeholder="t('aiProviders.form.namePlaceholder')"
          />
        </n-form-item>

        <n-form-item
          :label="t('aiProviders.form.apiBaseUrl')"
          :validation-status="validationState.apiBaseUrl ? 'error' : undefined"
          :feedback="validationState.apiBaseUrl || undefined"
        >
          <n-input
            v-model:value="formModel.apiBaseUrl"
            :disabled="isModalBusy"
            :placeholder="t('aiProviders.form.apiBaseUrlPlaceholder')"
          />
        </n-form-item>

        <n-form-item
          :label="t('aiProviders.form.apiKey')"
          :validation-status="validationState.apiKey ? 'error' : undefined"
          :feedback="validationState.apiKey || undefined"
        >
          <n-input
            v-model:value="formModel.apiKey"
            type="password"
            show-password-on="mousedown"
            :disabled="isModalBusy"
            :placeholder="apiKeyPlaceholder"
          />
          <div class="form-hint">{{ apiKeyHint }}</div>
        </n-form-item>

        <n-form-item
          :label="t('aiProviders.form.modelName')"
          :validation-status="validationState.modelName ? 'error' : undefined"
          :feedback="validationState.modelName || undefined"
        >
          <n-input
            v-model:value="formModel.modelName"
            :disabled="isModalBusy"
            :placeholder="t('aiProviders.form.modelNamePlaceholder')"
          />
        </n-form-item>

        <n-form-item
          :label="t('aiProviders.form.defaultSystemPrompt')"
        >
          <n-input
            v-model:value="formModel.defaultSystemPrompt"
            type="textarea"
            :autosize="{ minRows: 4, maxRows: 8 }"
            :disabled="isModalBusy"
            :placeholder="t('aiProviders.form.defaultSystemPromptPlaceholder')"
          />
          <div class="form-hint">{{ t('aiProviders.form.defaultSystemPromptHint') }}</div>
        </n-form-item>

        <n-form-item :label="t('aiProviders.form.isActive')">
          <n-space vertical :size="8">
            <n-switch v-model:value="formModel.isActive" :disabled="isModalBusy" />
            <div class="form-hint">{{ t('aiProviders.form.activeHint') }}</div>
          </n-space>
        </n-form-item>
      </n-form>

      <n-alert
        v-if="latestTestResult"
        :type="latestTestResult.succeeded ? 'success' : 'error'"
        :title="latestTestResult.succeeded ? t('aiProviders.test.resultSuccess') : t('aiProviders.test.resultFailed')"
        style="margin-top: 12px"
      >
        <div class="test-result-line">
          {{ t('aiProviders.test.resolvedApiBaseUrl') }}: {{ latestTestResult.resolvedApiBaseUrl }}
        </div>
        <div class="test-result-line">
          {{ t('aiProviders.test.resolvedModelName') }}: {{ latestTestResult.resolvedModelName }}
        </div>
        <div v-if="latestTestResult.caption" class="test-result-line">
          {{ t('aiProviders.test.caption') }}: {{ latestTestResult.caption }}
        </div>
        <div v-if="latestTestResult.errorMessage" class="test-result-line">
          {{ t('aiProviders.test.errorMessage') }}: {{ latestTestResult.errorMessage }}
        </div>
      </n-alert>

      <template #footer>
        <div class="profile-modal-footer">
          <n-space justify="end" :wrap="true">
            <n-button :disabled="isModalBusy" @click="closeProfileModal">
              {{ t('common.cancel') }}
            </n-button>
            <n-button :loading="profileModalTesting" :disabled="profileModalSubmitting" @click="handleProfileTest">
              {{ t('aiProviders.actions.test') }}
            </n-button>
            <n-button type="primary" :loading="profileModalSubmitting" :disabled="profileModalTesting" @click="submitProfileModal">
              {{ modalSubmitText }}
            </n-button>
          </n-space>
        </div>
      </template>
    </n-modal>
  </div>
</template>

<style scoped>
.section-card :deep(.n-card-header__main) {
  font-weight: 600;
}

.table-wrapper {
  overflow-x: auto;
}

.profile-modal {
  margin: 30px auto;
}

.profile-modal :deep(.n-card) {
  border-radius: 14px;
}

.profile-modal :deep(.n-card__content) {
  padding: 20px 24px 16px;
  max-height: min(74vh, 760px);
  overflow-y: auto;
}

.profile-modal-footer {
  width: 100%;
  border-top: 1px solid #f0f2f5;
  padding-top: 12px;
}

.profile-modal-footer :deep(.n-space) {
  width: 100%;
}

.form-hint {
  margin-top: 6px;
  color: #6b7280;
  font-size: 12px;
  line-height: 1.5;
}

.test-result-line {
  line-height: 1.6;
  word-break: break-word;
}

@media (max-width: 768px) {
  .profile-modal {
    margin: 8px auto;
  }

  .profile-modal :deep(.n-card__content) {
    padding: 14px;
    max-height: 76vh;
  }

  .profile-modal-footer :deep(.n-space-item) {
    flex: 1 1 auto;
  }

  .profile-modal-footer :deep(.n-space-item .n-button) {
    width: 100%;
  }
}
</style>
