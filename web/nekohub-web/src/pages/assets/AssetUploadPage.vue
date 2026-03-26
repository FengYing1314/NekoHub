<script setup lang="ts">
import { computed, reactive, ref } from 'vue';
import {
  NAlert,
  NButton,
  NCard,
  NDescriptions,
  NDescriptionsItem,
  NForm,
  NFormItem,
  NInput,
  NSpace,
  useMessage,
} from 'naive-ui';
import { useI18n } from 'vue-i18n';
import { useRoute, useRouter } from 'vue-router';
import PageHeader from '../../components/common/PageHeader.vue';
import { uploadAsset } from '../../api/assets/assets.api';
import { extractApiErrorMessage } from '../../api/client/error';
import { runtimeConfig } from '../../config/runtime';
import { formatFileSize } from '../../utils/format';

const MAX_UPLOAD_SIZE_BYTES = runtimeConfig.maxUploadSizeBytes;
const ALLOWED_IMAGE_CONTENT_TYPES = new Set(['image/png', 'image/jpeg', 'image/webp', 'image/gif']);
const ALLOWED_IMAGE_EXTENSIONS = ['.png', '.jpg', '.jpeg', '.webp', '.gif'];

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const message = useMessage();

const selectedFile = ref<File | null>(null);
const submitting = ref(false);
const uploadedAssetId = ref<string | null>(null);
const statusText = ref('');
const statusType = ref<'success' | 'error' | 'info' | null>(null);
const fileValidationError = ref('');

const formModel = reactive({
  description: '',
  altText: '',
});

const canSubmit = computed(() => !submitting.value && !!selectedFile.value && !fileValidationError.value);

function isImageFile(file: File): boolean {
  const contentType = file.type.trim().toLowerCase();
  if (contentType && ALLOWED_IMAGE_CONTENT_TYPES.has(contentType)) {
    return true;
  }

  const fileName = file.name.toLowerCase();
  return ALLOWED_IMAGE_EXTENSIONS.some((extension) => fileName.endsWith(extension));
}

function validateFile(file: File | null): string {
  if (!file) {
    return t('asset.upload.fileRequired');
  }

  if (!isImageFile(file)) {
    return t('asset.upload.invalidFileType');
  }

  if (file.size > MAX_UPLOAD_SIZE_BYTES) {
    return `${t('asset.upload.fileTooLarge')}（${formatFileSize(MAX_UPLOAD_SIZE_BYTES)}）`;
  }

  return '';
}

function handleFileChange(event: Event): void {
  const target = event.target as HTMLInputElement;
  selectedFile.value = target.files?.[0] ?? null;
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
    });

    uploadedAssetId.value = uploaded.id;
    statusType.value = 'success';
    statusText.value = t('common.uploadSuccess');
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
}
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
          <input
            class="file-input"
            type="file"
            accept="image/png,image/jpeg,image/webp,image/gif"
            :disabled="submitting"
            @change="handleFileChange"
          />
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

        <n-space>
          <n-button type="primary" :loading="submitting" :disabled="!canSubmit" @click="handleSubmit">
            {{ t('asset.upload.submit') }}
          </n-button>
          <n-button :disabled="submitting" @click="goBackToList">{{ t('common.cancel') }}</n-button>
        </n-space>
      </n-form>

      <n-alert v-if="statusText" :type="statusType ?? 'info'" style="margin-top: 16px">
        <template #header>{{ statusText }}</template>
        <n-space v-if="uploadedAssetId">
          <n-button type="primary" size="small" @click="goToDetail">{{ t('asset.upload.goToDetail') }}</n-button>
          <n-button size="small" @click="continueUpload">{{ t('asset.upload.continueUpload') }}</n-button>
        </n-space>
      </n-alert>
    </n-card>
  </div>
</template>

<style scoped>
.file-input {
  width: 100%;
  padding: 10px 12px;
  border: 1px solid #d1d5db;
  border-radius: 8px;
  background: #ffffff;
  color: #111827;
}

.file-input:focus {
  outline: 2px solid #93c5fd;
  outline-offset: 1px;
}

.section-card :deep(.n-card-header__main) {
  font-weight: 600;
}
</style>
