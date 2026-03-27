<script setup lang="ts">
import { computed, h, reactive, ref, watch } from 'vue';
import type { DataTableColumns, SelectOption } from 'naive-ui';
import {
  NAlert,
  NButton,
  NDataTable,
  NDrawer,
  NDrawerContent,
  NEmpty,
  NForm,
  NFormItem,
  NInput,
  NInputNumber,
  NModal,
  NSkeleton,
  NSpace,
  NSelect,
  NSwitch,
  NTag,
  useMessage,
} from 'naive-ui';
import { useI18n } from 'vue-i18n';
import { browseGitHubRepoProfile, upsertGitHubRepoProfile } from '../../api/system/storage.api';
import { extractApiError, extractApiErrorMessage } from '../../api/client/error';
import { useIsMobile } from '../../composables/useIsMobile';
import type {
  GitHubRepoBrowseItemResponse,
  GitHubRepoBrowseType,
  StorageProviderProfileResponse,
} from '../../types/storage';
import { formatFileSize } from '../../utils/format';

const DEFAULT_MAX_DEPTH = 2;
const DEFAULT_PAGE = 1;
const DEFAULT_PAGE_SIZE = 20;

const props = defineProps<{
  show: boolean;
  profile: StorageProviderProfileResponse | null;
}>();

const emit = defineEmits<{
  (event: 'update:show', value: boolean): void;
}>();

const { t } = useI18n();
const { isMobile } = useIsMobile();
const message = useMessage();

const browseLoading = ref(false);
const browseErrorMessage = ref('');
const browseResult = ref<Awaited<ReturnType<typeof browseGitHubRepoProfile>> | null>(null);

const browseState = reactive({
  path: '',
  recursive: false,
  maxDepth: DEFAULT_MAX_DEPTH,
  type: 'all' as GitHubRepoBrowseType,
  keyword: '',
  page: DEFAULT_PAGE,
  pageSize: DEFAULT_PAGE_SIZE,
});

const uploadModalVisible = ref(false);
const uploadSubmitting = ref(false);
const uploadTargetPath = ref('');
const uploadCommitMessage = ref('');
const uploadErrorMessage = ref('');
const selectedUploadFile = ref<File | null>(null);
const uploadFileInputRef = ref<HTMLInputElement | null>(null);

const profileDisplayName = computed(() => {
  if (!props.profile) {
    return '';
  }

  return props.profile.displayName?.trim() || props.profile.name;
});

const drawerTitle = computed(() => {
  const profileName = profileDisplayName.value || '-';
  return t('settings.storage.githubRepo.drawerTitle', { name: profileName });
});

const currentPath = computed(() => browseResult.value?.requestedPath ?? browseState.path);
const canGoParent = computed(() => currentPath.value.length > 0);
const usesControlledRead = computed(() => browseResult.value?.usesControlledRead ?? false);
const hasBrowseData = computed(() => (browseResult.value?.items.length ?? 0) > 0);

const browseTypeOptions = computed<SelectOption[]>(() => [
  {
    label: t('settings.storage.githubRepo.filters.types.all'),
    value: 'all',
  },
  {
    label: t('settings.storage.githubRepo.filters.types.file'),
    value: 'file',
  },
  {
    label: t('settings.storage.githubRepo.filters.types.dir'),
    value: 'dir',
  },
]);

const pageSizeOptions = computed<SelectOption[]>(() => [
  { label: '20', value: 20 },
  { label: '50', value: 50 },
  { label: '100', value: 100 },
]);

const browseColumns = computed<DataTableColumns<GitHubRepoBrowseItemResponse>>(() => [
  {
    title: t('settings.storage.githubRepo.table.columns.name'),
    key: 'name',
    minWidth: 220,
    render: (item) => {
      if (!item.isDirectory) {
        return h('span', item.name);
      }

      return h(
        NButton,
        {
          text: true,
          size: 'small',
          disabled: browseLoading.value,
          onClick: () => enterDirectory(item),
        },
        { default: () => item.name },
      );
    },
  },
  {
    title: t('settings.storage.githubRepo.table.columns.type'),
    key: 'type',
    minWidth: 100,
    render: (item) => h(
      NTag,
      {
        size: 'small',
        type: item.isDirectory ? 'warning' : 'info',
        bordered: false,
      },
      { default: () => (item.isDirectory ? 'dir' : 'file') },
    ),
  },
  {
    title: t('settings.storage.githubRepo.table.columns.size'),
    key: 'size',
    minWidth: 120,
    render: (item) => (item.size === null ? '-' : formatFileSize(item.size)),
  },
  {
    title: t('settings.storage.githubRepo.table.columns.sha'),
    key: 'sha',
    minWidth: 160,
    render: (item) => formatSha(item.sha),
  },
  {
    title: t('settings.storage.githubRepo.table.columns.actions'),
    key: 'actions',
    minWidth: 200,
    render: (item) => h(
      NSpace,
      {
        size: 6,
        wrap: true,
      },
      {
        default: () => [
          item.isDirectory
            ? h(
              NButton,
              {
                size: 'small',
                quaternary: true,
                disabled: browseLoading.value,
                onClick: () => enterDirectory(item),
              },
              { default: () => t('settings.storage.githubRepo.actions.enterDirectory') },
            )
            : null,
          item.isFile
            ? h(
              NButton,
              {
                size: 'small',
                quaternary: true,
                type: 'primary',
                disabled: browseLoading.value || uploadSubmitting.value,
                onClick: () => openUploadModal(item.path),
              },
              { default: () => t('settings.storage.githubRepo.actions.uploadOverwrite') },
            )
            : null,
          item.isFile && item.publicUrl
            ? h(
              NButton,
              {
                size: 'small',
                quaternary: true,
                type: 'info',
                onClick: () => {
                  if (item.publicUrl) {
                    openPublicUrl(item.publicUrl);
                  }
                },
              },
              { default: () => t('settings.storage.githubRepo.actions.openLink') },
            )
            : null,
        ],
      },
    ),
  },
]);

const uploadDialogTitle = computed(() => t('settings.storage.githubRepo.upload.title'));
const selectedFileDisplayName = computed(() => selectedUploadFile.value?.name ?? t('settings.storage.githubRepo.upload.noFileSelected'));
const uploadTargetExpectedSha = computed(() => {
  const normalizedTargetPath = normalizePath(uploadTargetPath.value);
  if (!normalizedTargetPath || !browseResult.value) {
    return null;
  }

  const matchedItem = browseResult.value.items.find((item) =>
    item.isFile
    && normalizePath(item.path) === normalizedTargetPath);
  return matchedItem?.sha ?? null;
});

const pagingText = computed(() => {
  if (!browseResult.value) {
    return '';
  }

  return t('settings.storage.githubRepo.pagination.summary', {
    total: browseResult.value.total,
    page: browseResult.value.page,
    pageSize: browseResult.value.pageSize,
  });
});

watch(
  () => props.show,
  (show) => {
    if (!show) {
      resetUploadModal();
      return;
    }

    if (!props.profile) {
      return;
    }

    resetBrowseState();
    void fetchBrowse();
  },
);

watch(
  () => props.profile?.id,
  (profileId, previousProfileId) => {
    if (!props.show || !profileId || profileId === previousProfileId) {
      return;
    }

    resetBrowseState();
    void fetchBrowse();
  },
);

function handleDrawerVisibilityChange(value: boolean): void {
  emit('update:show', value);
}

function resetBrowseState(): void {
  browseErrorMessage.value = '';
  browseResult.value = null;
  browseState.path = '';
  browseState.recursive = false;
  browseState.maxDepth = DEFAULT_MAX_DEPTH;
  browseState.type = 'all';
  browseState.keyword = '';
  browseState.page = DEFAULT_PAGE;
  browseState.pageSize = DEFAULT_PAGE_SIZE;
}

function resetUploadModal(): void {
  uploadModalVisible.value = false;
  uploadSubmitting.value = false;
  uploadTargetPath.value = '';
  uploadCommitMessage.value = '';
  uploadErrorMessage.value = '';
  selectedUploadFile.value = null;
  clearUploadFileInputValue();
}

async function fetchBrowse(): Promise<void> {
  if (!props.profile) {
    return;
  }

  browseLoading.value = true;
  browseErrorMessage.value = '';

  try {
    const result = await browseGitHubRepoProfile(props.profile.id, {
      path: normalizeOptionalPath(browseState.path),
      recursive: browseState.recursive,
      maxDepth: browseState.maxDepth,
      type: browseState.type,
      keyword: normalizeOptionalString(browseState.keyword) ?? undefined,
      page: browseState.page,
      pageSize: browseState.pageSize,
    });

    browseResult.value = result;
    browseState.path = result.requestedPath;
    browseState.page = result.page;
    browseState.pageSize = result.pageSize;
  } catch (error) {
    browseErrorMessage.value = extractApiErrorMessage(error);
  } finally {
    browseLoading.value = false;
  }
}

function applyCurrentPath(): void {
  browseState.page = DEFAULT_PAGE;
  browseState.path = normalizePath(browseState.path);
  void fetchBrowse();
}

function applyFilters(): void {
  browseState.page = DEFAULT_PAGE;
  void fetchBrowse();
}

function resetFilters(): void {
  browseState.keyword = '';
  browseState.type = 'all';
  browseState.page = DEFAULT_PAGE;
  browseState.pageSize = DEFAULT_PAGE_SIZE;
  void fetchBrowse();
}

function enterDirectory(item: GitHubRepoBrowseItemResponse): void {
  if (!item.isDirectory) {
    return;
  }

  browseState.path = normalizePath(item.path);
  browseState.page = DEFAULT_PAGE;
  void fetchBrowse();
}

function goParentDirectory(): void {
  if (!canGoParent.value) {
    return;
  }

  const normalizedPath = normalizePath(currentPath.value);
  if (!normalizedPath) {
    browseState.path = '';
    browseState.page = DEFAULT_PAGE;
    void fetchBrowse();
    return;
  }

  const pathSegments = normalizedPath.split('/');
  pathSegments.pop();
  browseState.path = pathSegments.join('/');
  browseState.page = DEFAULT_PAGE;
  void fetchBrowse();
}

function goPreviousPage(): void {
  if (browseLoading.value || browseState.page <= 1) {
    return;
  }

  browseState.page -= 1;
  void fetchBrowse();
}

function goNextPage(): void {
  if (browseLoading.value || !browseResult.value?.hasMore) {
    return;
  }

  browseState.page += 1;
  void fetchBrowse();
}

function handlePageSizeChange(value: number | null): void {
  if (!value) {
    return;
  }

  browseState.pageSize = value;
  browseState.page = DEFAULT_PAGE;
  void fetchBrowse();
}

function openUploadModal(prefillPath?: string): void {
  uploadErrorMessage.value = '';
  uploadCommitMessage.value = '';
  selectedUploadFile.value = null;
  clearUploadFileInputValue();

  if (prefillPath) {
    uploadTargetPath.value = normalizePath(prefillPath);
  } else {
    uploadTargetPath.value = normalizePath(currentPath.value);
  }

  uploadModalVisible.value = true;
}

function closeUploadModal(): void {
  if (uploadSubmitting.value) {
    return;
  }

  resetUploadModal();
}

function triggerUploadFileSelect(): void {
  uploadFileInputRef.value?.click();
}

function handleUploadFileChange(event: Event): void {
  const input = event.target as HTMLInputElement;
  selectedUploadFile.value = input.files?.[0] ?? null;

  if (!selectedUploadFile.value) {
    return;
  }

  const normalizedTargetPath = normalizePath(uploadTargetPath.value);
  const normalizedCurrentPath = normalizePath(currentPath.value);
  if (!normalizedTargetPath || normalizedTargetPath === normalizedCurrentPath) {
    uploadTargetPath.value = joinPath(normalizedCurrentPath, selectedUploadFile.value.name);
  }
}

async function submitUpload(): Promise<void> {
  if (!props.profile) {
    return;
  }

  if (!selectedUploadFile.value) {
    uploadErrorMessage.value = t('settings.storage.githubRepo.upload.validation.fileRequired');
    return;
  }

  const normalizedTargetPath = normalizePath(uploadTargetPath.value);
  if (!normalizedTargetPath) {
    uploadErrorMessage.value = t('settings.storage.githubRepo.upload.validation.pathRequired');
    return;
  }

  uploadSubmitting.value = true;
  uploadErrorMessage.value = '';

  try {
    const contentBase64 = await readFileAsBase64(selectedUploadFile.value);
    const result = await upsertGitHubRepoProfile(props.profile.id, {
      path: normalizedTargetPath,
      contentBase64,
      commitMessage: normalizeOptionalString(uploadCommitMessage.value) ?? undefined,
      expectedSha: uploadTargetExpectedSha.value ?? undefined,
    });

    message.success(
      result.operation === 'created'
        ? t('settings.storage.githubRepo.messages.uploadCreated')
        : t('settings.storage.githubRepo.messages.uploadUpdated'),
    );

    resetUploadModal();
    await fetchBrowse();
  } catch (error) {
    const apiError = extractApiError(error);
    const isShaConflict = apiError?.code === 'storage_provider_upsert_expected_sha_conflict'
      || apiError?.code === 'storage_provider_upsert_expected_sha_target_missing'
      || apiError?.status === 409;

    if (isShaConflict) {
      const conflictMessage = apiError?.message || extractApiErrorMessage(error);
      message.warning(`${t('settings.storage.githubRepo.messages.upsertConflict')}: ${conflictMessage}`);
      uploadErrorMessage.value = t('settings.storage.githubRepo.messages.upsertConflictHint');
      return;
    }

    uploadErrorMessage.value = extractApiErrorMessage(error);
    message.error(`${t('settings.storage.githubRepo.messages.uploadFailed')}: ${uploadErrorMessage.value}`);
  } finally {
    uploadSubmitting.value = false;
  }
}

function openPublicUrl(publicUrl: string): void {
  window.open(publicUrl, '_blank', 'noopener,noreferrer');
}

function clearUploadFileInputValue(): void {
  if (uploadFileInputRef.value) {
    uploadFileInputRef.value.value = '';
  }
}

function formatSha(value: string | null): string {
  if (!value) {
    return '-';
  }

  if (value.length <= 12) {
    return value;
  }

  return `${value.slice(0, 6)}...${value.slice(-6)}`;
}

function normalizePath(value: string): string {
  return value
    .trim()
    .replaceAll('\\', '/')
    .replace(/^\/+/, '')
    .replace(/\/+$/, '');
}

function normalizeOptionalPath(value: string): string | undefined {
  const normalized = normalizePath(value);
  return normalized.length > 0 ? normalized : undefined;
}

function normalizeOptionalString(value: string): string | null {
  const normalized = value.trim();
  return normalized.length > 0 ? normalized : null;
}

function joinPath(basePath: string, fileName: string): string {
  const normalizedBasePath = normalizePath(basePath);
  const normalizedFileName = normalizePath(fileName);

  if (!normalizedBasePath) {
    return normalizedFileName;
  }

  if (!normalizedFileName) {
    return normalizedBasePath;
  }

  return `${normalizedBasePath}/${normalizedFileName}`;
}

function readFileAsBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      const result = reader.result;
      if (typeof result !== 'string') {
        reject(new Error('FileReader result is not a data URL.'));
        return;
      }

      const commaIndex = result.indexOf(',');
      if (commaIndex < 0) {
        reject(new Error('Invalid data URL.'));
        return;
      }

      resolve(result.slice(commaIndex + 1));
    };

    reader.onerror = () => reject(reader.error ?? new Error('Failed to read file.'));
    reader.readAsDataURL(file);
  });
}
</script>

<template>
  <n-drawer :show="show" placement="right" :width="isMobile ? '100%' : 920" @update:show="handleDrawerVisibilityChange">
    <n-drawer-content :title="drawerTitle" closable body-content-style="padding: 12px">
      <n-space vertical :size="12">
        <n-alert type="info" :show-icon="false">
          {{ t('settings.storage.githubRepo.runtimeNotice') }}
        </n-alert>

        <n-alert v-if="usesControlledRead" type="warning" :show-icon="false">
          {{ t('settings.storage.githubRepo.controlledReadHint') }}
        </n-alert>

        <n-space class="github-browser-toolbar" :size="8" wrap>
          <n-button
            size="small"
            secondary
            :disabled="browseLoading || !canGoParent"
            @click="goParentDirectory"
          >
            {{ t('settings.storage.githubRepo.actions.goParent') }}
          </n-button>
          <n-input
            v-model:value="browseState.path"
            size="small"
            class="github-path-input"
            :placeholder="t('settings.storage.githubRepo.filters.pathPlaceholder')"
          />
          <n-button size="small" :loading="browseLoading" @click="applyCurrentPath">
            {{ t('settings.storage.githubRepo.actions.openPath') }}
          </n-button>
          <n-button
            size="small"
            type="primary"
            secondary
            :disabled="browseLoading"
            @click="openUploadModal()"
          >
            {{ t('settings.storage.githubRepo.actions.uploadFile') }}
          </n-button>
          <n-button text size="small" :loading="browseLoading" @click="fetchBrowse">
            {{ t('common.refresh') }}
          </n-button>
        </n-space>

        <n-space class="github-browser-filters" :size="8" wrap>
          <n-select
            v-model:value="browseState.type"
            size="small"
            class="github-filter-type"
            :options="browseTypeOptions"
          />
          <n-input
            v-model:value="browseState.keyword"
            size="small"
            class="github-filter-keyword"
            clearable
            :placeholder="t('settings.storage.githubRepo.filters.keywordPlaceholder')"
            @keyup.enter="applyFilters"
          />
          <n-switch v-model:value="browseState.recursive" size="small">
            <template #checked>{{ t('settings.storage.githubRepo.filters.recursive') }}</template>
            <template #unchecked>{{ t('settings.storage.githubRepo.filters.recursive') }}</template>
          </n-switch>
          <n-input-number
            v-model:value="browseState.maxDepth"
            size="small"
            :min="1"
            :precision="0"
            :disabled="!browseState.recursive"
            class="github-filter-depth"
          />
          <n-select
            :value="browseState.pageSize"
            size="small"
            class="github-filter-page-size"
            :options="pageSizeOptions"
            @update:value="handlePageSizeChange"
          />
          <n-button size="small" type="primary" :loading="browseLoading" @click="applyFilters">
            {{ t('settings.storage.githubRepo.actions.search') }}
          </n-button>
          <n-button size="small" :disabled="browseLoading" @click="resetFilters">
            {{ t('settings.storage.githubRepo.actions.resetFilters') }}
          </n-button>
        </n-space>

        <n-alert v-if="browseErrorMessage" type="error" :show-icon="false">
          <div class="github-error-row">
            <span>{{ t('settings.storage.githubRepo.messages.browseFailed') }}: {{ browseErrorMessage }}</span>
            <n-button text size="small" :loading="browseLoading" @click="fetchBrowse">
              {{ t('common.retry') }}
            </n-button>
          </div>
        </n-alert>

        <template v-if="browseLoading && !browseResult">
          <n-skeleton text :repeat="4" />
        </template>

        <template v-else-if="browseResult">
          <div class="github-browser-meta">
            <span>{{ t('settings.storage.githubRepo.meta.currentPath') }}: {{ currentPath || '/' }}</span>
            <span>{{ pagingText }}</span>
          </div>

          <n-empty
            v-if="!hasBrowseData"
            :description="t('settings.storage.githubRepo.messages.emptyDirectory')"
          />

          <div v-else class="github-table-wrapper">
            <n-data-table
              size="small"
              :columns="browseColumns"
              :data="browseResult.items"
              :row-key="(row) => row.path"
              :scroll-x="860"
            />
          </div>

          <div class="github-pagination">
            <n-space justify="space-between" :wrap="true">
              <span>{{ pagingText }}</span>
              <n-space :size="8">
                <n-button
                  size="small"
                  :disabled="browseLoading || browseResult.page <= 1"
                  @click="goPreviousPage"
                >
                  {{ t('settings.storage.githubRepo.pagination.previous') }}
                </n-button>
                <n-button
                  size="small"
                  :disabled="browseLoading || !browseResult.hasMore"
                  @click="goNextPage"
                >
                  {{ t('settings.storage.githubRepo.pagination.next') }}
                </n-button>
              </n-space>
            </n-space>
          </div>
        </template>
      </n-space>
    </n-drawer-content>
  </n-drawer>

  <n-modal
    v-model:show="uploadModalVisible"
    preset="card"
    class="github-upload-modal"
    :title="uploadDialogTitle"
    :mask-closable="!uploadSubmitting"
    :closable="!uploadSubmitting"
  >
    <n-form label-placement="top">
      <n-form-item :label="t('settings.storage.githubRepo.upload.file')">
        <div class="github-upload-file-row">
          <n-button size="small" :disabled="uploadSubmitting" @click="triggerUploadFileSelect">
            {{ t('settings.storage.githubRepo.upload.selectFile') }}
          </n-button>
          <span class="github-upload-file-name">{{ selectedFileDisplayName }}</span>
        </div>
        <input
          ref="uploadFileInputRef"
          class="github-hidden-file-input"
          type="file"
          :disabled="uploadSubmitting"
          @change="handleUploadFileChange"
        />
      </n-form-item>

      <n-form-item :label="t('settings.storage.githubRepo.upload.targetPath')">
        <n-input
          v-model:value="uploadTargetPath"
          :placeholder="t('settings.storage.githubRepo.upload.targetPathPlaceholder')"
          :disabled="uploadSubmitting"
        />
      </n-form-item>

      <n-form-item :label="t('settings.storage.githubRepo.upload.commitMessage')">
        <n-input
          v-model:value="uploadCommitMessage"
          :placeholder="t('settings.storage.githubRepo.upload.commitMessagePlaceholder')"
          :disabled="uploadSubmitting"
        />
      </n-form-item>

      <n-alert v-if="uploadTargetExpectedSha" type="info" :show-icon="false" class="github-upload-alert">
        {{ t('settings.storage.githubRepo.upload.expectedShaHint', { sha: uploadTargetExpectedSha }) }}
      </n-alert>

      <n-alert v-if="uploadErrorMessage" type="error" :show-icon="false" class="github-upload-alert">
        {{ uploadErrorMessage }}
      </n-alert>
    </n-form>

    <template #footer>
      <n-space justify="end" :wrap="true">
        <n-button :disabled="uploadSubmitting" @click="closeUploadModal">
          {{ t('common.cancel') }}
        </n-button>
        <n-button type="primary" :loading="uploadSubmitting" @click="submitUpload">
          {{ t('settings.storage.githubRepo.actions.uploadFile') }}
        </n-button>
      </n-space>
    </template>
  </n-modal>
</template>

<style scoped>
.github-browser-toolbar {
  align-items: center;
}

.github-path-input {
  width: min(460px, 100%);
}

.github-browser-filters {
  align-items: center;
}

.github-filter-type {
  width: 130px;
}

.github-filter-keyword {
  width: min(280px, 100%);
}

.github-filter-depth {
  width: 120px;
}

.github-filter-page-size {
  width: 120px;
}

.github-error-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}

.github-browser-meta {
  display: flex;
  justify-content: space-between;
  gap: 10px;
  flex-wrap: wrap;
  color: #6b7280;
  font-size: 13px;
}

.github-table-wrapper {
  overflow-x: auto;
}

.github-pagination {
  color: #6b7280;
  font-size: 13px;
}

.github-upload-modal {
  width: min(640px, calc(100vw - 24px));
}

.github-upload-file-row {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}

.github-upload-file-name {
  color: #374151;
  font-size: 13px;
  word-break: break-all;
}

.github-hidden-file-input {
  position: absolute;
  width: 1px;
  height: 1px;
  opacity: 0;
  pointer-events: none;
}

.github-upload-alert {
  margin-bottom: 10px;
}

@media (max-width: 768px) {
  .github-path-input,
  .github-filter-keyword,
  .github-filter-type,
  .github-filter-depth,
  .github-filter-page-size {
    width: 100%;
  }

  .github-pagination :deep(.n-space) {
    width: 100%;
  }

  .github-pagination :deep(.n-space-item) {
    width: 100%;
  }

  .github-pagination :deep(.n-space-item .n-button) {
    width: 100%;
  }
}
</style>
