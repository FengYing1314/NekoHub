<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue';
import {
  NAlert,
  NButton,
  NCard,
  NDescriptions,
  NDescriptionsItem,
  NForm,
  NFormItem,
  NInput,
  NSelect,
  NSpace,
  NSwitch,
  useMessage,
} from 'naive-ui';
import { useI18n } from 'vue-i18n';
import { useRoute, useRouter } from 'vue-router';
import PageHeader from '../../components/common/PageHeader.vue';
import { uploadAsset } from '../../api/assets/assets.api';
import { extractApiErrorMessage } from '../../api/client/error';
import { getStorageProviderOverview } from '../../api/system/storage.api';
import { useAppConfigStore } from '../../stores/app-config';
import { runtimeConfig } from '../../config/runtime';
import type { StorageProviderOverviewResponse } from '../../types/storage';
import { formatFileSize } from '../../utils/format';

const DEFAULT_ALLOWED_IMAGE_CONTENT_TYPES = ['image/png', 'image/jpeg', 'image/webp', 'image/gif'];
const CONTENT_TYPE_EXTENSION_MAP: Record<string, string[]> = {
  'image/png': ['.png'],
  'image/jpeg': ['.jpg', '.jpeg'],
  'image/webp': ['.webp'],
  'image/gif': ['.gif'],
};

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const message = useMessage();
const appConfigStore = useAppConfigStore();

const selectedFile = ref<File | null>(null);
const fileInputRef = ref<HTMLInputElement | null>(null);
const submitting = ref(false);
const uploadedAssetId = ref<string | null>(null);
const statusText = ref('');
const statusType = ref<'success' | 'error' | 'info' | null>(null);
const fileValidationError = ref('');
const storageOverview = ref<StorageProviderOverviewResponse | null>(null);
const storageOverviewLoading = ref(false);
const storageOverviewLoadFailed = ref(false);

const formModel = reactive({
  description: '',
  altText: '',
  isPublic: true,
  storageProviderProfileId: '',
  runEnrichment: true,
  commitMessage: '',
});

const canSubmit = computed(() => !submitting.value && !!selectedFile.value && !fileValidationError.value);
const showBackgroundProcessingHint = computed(() => uploadedAssetId.value !== null && statusType.value === 'success');
const resolvedMaxUploadSizeBytes = computed(() => appConfigStore.maxUploadSizeBytes || runtimeConfig.maxUploadSizeBytes);
const allowedImageContentTypes = computed(() => (
  appConfigStore.allowedContentTypes.length > 0
    ? appConfigStore.allowedContentTypes
    : DEFAULT_ALLOWED_IMAGE_CONTENT_TYPES
));
const allowedImageExtensions = computed(() => Array.from(new Set(
  allowedImageContentTypes.value.flatMap((contentType) => CONTENT_TYPE_EXTENSION_MAP[contentType] ?? []),
)));
const fileAccept = computed(() => allowedImageContentTypes.value.join(','));
const acceptedFileTypeSummary = computed(() => {
  const labels = allowedImageExtensions.value.map((extension) => extension.replace('.', '').toUpperCase());
  return labels.join(' / ');
});
const selectedFileDisplayName = computed(() => (
  selectedFile.value?.name ?? t('asset.upload.noFileSelected')
));
const filePickerHint = computed(() => t('asset.upload.fileHint', {
  types: acceptedFileTypeSummary.value,
  maxSize: formatFileSize(resolvedMaxUploadSizeBytes.value),
}));
const visibilityHint = computed(() => (
  formModel.isPublic
    ? t('asset.upload.visibilityPublicHint')
    : t('asset.upload.visibilityPrivateHint')
));
const enabledStorageProfiles = computed(() => (
  storageOverview.value?.profiles.filter((profile) => profile.isEnabled) ?? []
));
const selectedStorageProfile = computed(() => (
  enabledStorageProfiles.value.find((profile) => profile.id === formModel.storageProviderProfileId) ?? null
));
const isGitHubWriteTarget = computed(() => {
  const overview = storageOverview.value;
  if (!overview) {
    return false;
  }

  if (selectedStorageProfile.value) {
    return selectedStorageProfile.value.providerType === 'github-repo';
  }

  if (overview.defaultWriteProfile ?? overview.defaultProfile) {
    const defaultProfile = overview.defaultWriteProfile ?? overview.defaultProfile;
    return defaultProfile?.isEnabled && defaultProfile.providerType === 'github-repo';
  }

  return overview.runtime.providerType === 'github-repo';
});

function getStorageProviderTypeLabel(providerType: string): string {
  switch (providerType) {
    case 'local':
      return t('settings.storage.providers.local');
    case 's3-compatible':
      return t('settings.storage.providers.s3Compatible');
    case 'github-repo':
      return t('settings.storage.providers.githubRepo');
    case 'github-releases':
      return t('settings.storage.providers.githubReleases');
    default:
      return providerType;
  }
}

const storageProfileOptions = computed(() => [
  {
    label: t('asset.upload.defaultStorageTarget'),
    value: '',
  },
  ...enabledStorageProfiles.value.map((profile) => ({
    label: `${profile.displayName || profile.name} (${getStorageProviderTypeLabel(profile.providerType)})`,
    value: profile.id,
  })),
]);
const storageTargetHint = computed(() => {
  if (selectedStorageProfile.value) {
    return t('asset.upload.storageTargetSelectedHint', {
      name: selectedStorageProfile.value.displayName || selectedStorageProfile.value.name,
      providerType: getStorageProviderTypeLabel(selectedStorageProfile.value.providerType),
    });
  }

  if (storageOverview.value?.defaultWriteProfile ?? storageOverview.value?.defaultProfile) {
    const defaultProfile = storageOverview.value?.defaultWriteProfile ?? storageOverview.value?.defaultProfile;
    return t('asset.upload.storageTargetDefaultHint', {
      name: defaultProfile?.displayName || defaultProfile?.name || '-',
      providerType: defaultProfile?.providerType
        ? getStorageProviderTypeLabel(defaultProfile.providerType)
        : '-',
    });
  }

  if (storageOverview.value) {
    return t('asset.upload.storageTargetRuntimeHint', {
      providerType: getStorageProviderTypeLabel(storageOverview.value.runtime.providerType),
      providerName: storageOverview.value.runtime.providerName,
    });
  }

  if (storageOverviewLoadFailed.value) {
    return t('settings.storage.loadFailed');
  }

  return t('asset.upload.storageTargetLoadingHint');
});
const enrichmentHint = computed(() => (
  formModel.runEnrichment
    ? t('asset.upload.runEnrichmentEnabledHint')
    : t('asset.upload.runEnrichmentDisabledHint')
));

function isImageFile(file: File): boolean {
  const contentType = file.type.trim().toLowerCase();
  if (contentType && allowedImageContentTypes.value.includes(contentType)) {
    return true;
  }

  const fileName = file.name.toLowerCase();
  return allowedImageExtensions.value.some((extension) => fileName.endsWith(extension));
}

function validateFile(file: File | null): string {
  if (!file) {
    return t('asset.upload.fileRequired');
  }

  if (!isImageFile(file)) {
    return t('asset.upload.invalidFileType');
  }

  if (file.size > resolvedMaxUploadSizeBytes.value) {
    return `${t('asset.upload.fileTooLarge')}（${formatFileSize(resolvedMaxUploadSizeBytes.value)}）`;
  }

  return '';
}

function handleFileChange(event: Event): void {
  const target = event.target as HTMLInputElement;
  const nextFile = target.files?.[0] ?? null;
  target.value = '';
  selectedFile.value = nextFile;
  uploadedAssetId.value = null;

  fileValidationError.value = validateFile(selectedFile.value);
  if (fileValidationError.value) {
    statusType.value = 'error';
    statusText.value = fileValidationError.value;
  } else {
    statusType.value = null;
    statusText.value = '';
  }
}

function triggerFileSelect(): void {
  fileInputRef.value?.click();
}

function clearFileInputValue(): void {
  if (fileInputRef.value) {
    fileInputRef.value.value = '';
  }
}

async function loadStorageOverview(): Promise<void> {
  storageOverviewLoading.value = true;
  storageOverviewLoadFailed.value = false;

  try {
    storageOverview.value = await getStorageProviderOverview();
  } catch {
    storageOverview.value = null;
    storageOverviewLoadFailed.value = true;
  } finally {
    storageOverviewLoading.value = false;
  }
}

async function handleSubmit(): Promise<void> {
  fileValidationError.value = validateFile(selectedFile.value);
  if (fileValidationError.value) {
    message.warning(fileValidationError.value);
    return;
  }

  if (!selectedFile.value) {
    return;
  }

  submitting.value = true;
  statusType.value = 'info';
  statusText.value = t('common.uploading');

  try {
    const uploaded = await uploadAsset({
      file: selectedFile.value,
      description: formModel.description,
      altText: formModel.altText,
      isPublic: formModel.isPublic,
      storageProviderProfileId: formModel.storageProviderProfileId || undefined,
      runEnrichment: formModel.runEnrichment,
      commitMessage: isGitHubWriteTarget.value ? formModel.commitMessage : undefined,
    });

    uploadedAssetId.value = uploaded.id;
    statusType.value = 'success';
    statusText.value = t('asset.upload.uploadAccepted');
    message.success(t('common.uploadSuccess'));
  } catch (error) {
    statusType.value = 'error';
    statusText.value = `${t('common.uploadFailed')}: ${extractApiErrorMessage(error)}`;
    message.error(statusText.value);
  } finally {
    submitting.value = false;
  }
}

function goToDetail(): void {
  if (!uploadedAssetId.value) {
    return;
  }

  void router.push({
    path: `/assets/${uploadedAssetId.value}`,
    query: route.query,
  });
}

function goBackToList(): void {
  void router.push({
    path: '/assets',
    query: route.query,
  });
}

function continueUpload(): void {
  selectedFile.value = null;
  uploadedAssetId.value = null;
  statusText.value = '';
  statusType.value = null;
  fileValidationError.value = '';
  clearFileInputValue();
}

onMounted(() => {
  void loadStorageOverview();
});
</script>

<template>
  <div>
    <page-header :title="t('asset.upload.title')" :description="t('asset.upload.description')">
      <template #actions>
        <n-button @click="goBackToList">{{ t('common.back') }}</n-button>
      </template>
    </page-header>

    <n-card class="section-card">
      <n-form label-placement="top">
        <n-form-item :label="t('asset.upload.file')">
          <div class="file-picker">
            <n-button secondary :disabled="submitting" @click="triggerFileSelect">
              {{ t('asset.upload.selectFile') }}
            </n-button>
            <span class="file-picker__name">{{ selectedFileDisplayName }}</span>
            <input
              ref="fileInputRef"
              class="file-input"
              type="file"
              :accept="fileAccept"
              :disabled="submitting"
              @change="handleFileChange"
            />
          </div>
          <div class="form-hint">{{ filePickerHint }}</div>
        </n-form-item>

        <n-alert v-if="fileValidationError" type="error" style="margin-bottom: 16px">
          {{ fileValidationError }}
        </n-alert>

        <n-card v-if="selectedFile" size="small" style="margin-bottom: 16px">
          <template #header>{{ t('asset.upload.selectedFileInfo') }}</template>
          <n-descriptions label-placement="left" :column="1">
            <n-descriptions-item :label="t('asset.upload.selectedFileName')">
              {{ selectedFile.name }}
            </n-descriptions-item>
            <n-descriptions-item :label="t('asset.upload.selectedFileType')">
              {{ selectedFile.type || '-' }}
            </n-descriptions-item>
            <n-descriptions-item :label="t('asset.upload.selectedFileSize')">
              {{ formatFileSize(selectedFile.size) }}
            </n-descriptions-item>
          </n-descriptions>
        </n-card>

        <n-form-item :label="t('asset.upload.descriptionLabel')">
          <n-input
            v-model:value="formModel.description"
            type="textarea"
            :autosize="{ minRows: 2, maxRows: 4 }"
            :disabled="submitting"
          />
        </n-form-item>

        <n-form-item :label="t('asset.upload.altText')">
          <n-input
            v-model:value="formModel.altText"
            type="textarea"
            :autosize="{ minRows: 2, maxRows: 4 }"
            :disabled="submitting"
          />
        </n-form-item>

        <n-form-item :label="t('asset.upload.storageTargetLabel')">
          <n-space vertical :size="8" style="width: 100%">
            <n-select
              v-model:value="formModel.storageProviderProfileId"
              :options="storageProfileOptions"
              :disabled="submitting || storageOverviewLoading || storageOverview === null"
            />
            <div class="form-hint">{{ storageTargetHint }}</div>
            <n-alert v-if="storageOverviewLoadFailed" type="warning" :show-icon="false">
              {{ t('settings.storage.loadFailed') }}
            </n-alert>
          </n-space>
        </n-form-item>

        <n-form-item :label="t('asset.upload.runEnrichmentLabel')">
          <n-space vertical :size="8">
            <n-switch v-model:value="formModel.runEnrichment" :disabled="submitting">
              <template #checked>{{ t('common.enabled') }}</template>
              <template #unchecked>{{ t('common.disabled') }}</template>
            </n-switch>
            <div class="form-hint">{{ enrichmentHint }}</div>
          </n-space>
        </n-form-item>

        <n-form-item v-if="isGitHubWriteTarget" :label="t('asset.upload.commitMessageLabel')">
          <n-space vertical :size="8" style="width: 100%">
            <n-input
              v-model:value="formModel.commitMessage"
              type="textarea"
              :autosize="{ minRows: 2, maxRows: 4 }"
              :placeholder="t('asset.upload.commitMessagePlaceholder')"
              :disabled="submitting"
            />
            <div class="form-hint">{{ t('asset.upload.commitMessageHint') }}</div>
          </n-space>
        </n-form-item>

        <n-form-item :label="t('asset.upload.visibilityLabel')">
          <n-space vertical :size="8">
            <n-switch v-model:value="formModel.isPublic" :disabled="submitting">
              <template #checked>{{ t('asset.visibility.public') }}</template>
              <template #unchecked>{{ t('asset.visibility.private') }}</template>
            </n-switch>
            <div class="form-hint">{{ visibilityHint }}</div>
          </n-space>
        </n-form-item>

        <n-space wrap class="form-actions">
          <n-button type="primary" :loading="submitting" :disabled="!canSubmit" @click="handleSubmit">
            {{ t('asset.upload.submit') }}
          </n-button>
          <n-button :disabled="submitting" @click="goBackToList">{{ t('common.cancel') }}</n-button>
        </n-space>
      </n-form>

      <n-alert v-if="statusText" :type="statusType ?? 'info'" style="margin-top: 16px">
        <template #header>{{ statusText }}</template>
        <div v-if="showBackgroundProcessingHint" class="form-hint" style="margin-bottom: 12px">
          {{ t('asset.upload.backgroundProcessingHint') }}
        </div>
        <n-space v-if="uploadedAssetId" wrap class="form-actions">
          <n-button type="primary" size="small" @click="goToDetail">{{ t('asset.upload.goToDetail') }}</n-button>
          <n-button size="small" @click="continueUpload">{{ t('asset.upload.continueUpload') }}</n-button>
        </n-space>
      </n-alert>
    </n-card>
  </div>
</template>

<style scoped>
.file-input {
  position: absolute;
  width: 1px;
  height: 1px;
  opacity: 0;
  pointer-events: none;
}

.file-picker {
  position: relative;
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
  padding: 14px 16px;
  border: 1px dashed rgba(148, 163, 184, 0.72);
  border-radius: 14px;
  background: linear-gradient(180deg, rgba(248, 250, 252, 0.88), rgba(255, 255, 255, 0.98));
}

.file-picker__name {
  font-size: 13px;
  line-height: 1.6;
  color: #475569;
  word-break: break-all;
}

.section-card :deep(.n-card-header__main) {
  font-weight: 600;
}

.form-hint {
  font-size: 13px;
  color: #6b7280;
}

@media (max-width: 768px) {
  .form-actions {
    width: 100%;
  }

  .form-actions :deep(.n-space-item) {
    width: 100%;
  }

  .form-actions :deep(.n-space-item > *) {
    width: 100%;
  }
}
</style>
