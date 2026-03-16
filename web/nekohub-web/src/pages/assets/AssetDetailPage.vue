<script setup lang="ts">
import { computed, h, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue';
import type { DataTableColumns } from 'naive-ui';
import {
  NAlert,
  NButton,
  NCard,
  NDataTable,
  NDescriptions,
  NDescriptionsItem,
  NEmpty,
  NForm,
  NFormItem,
  NImage,
  NInput,
  NPopconfirm,
  NResult,
  NSelect,
  NSpace,
  NTag,
  useMessage,
} from 'naive-ui';
import { useI18n } from 'vue-i18n';
import { useRoute, useRouter } from 'vue-router';
import PageHeader from '../../components/common/PageHeader.vue';
import AssetStatusTag from '../../components/assets/AssetStatusTag.vue';
import AssetVisibilityTag from '../../components/assets/AssetVisibilityTag.vue';
import StructuredResultRenderer from '../../components/assets/structured-results/StructuredResultRenderer.vue';
import {
  deleteAsset,
  getAssetContentBlob,
  getAsset,
  patchAsset,
  runAssetWorkflow,
} from '../../api/assets/assets.api';
import { extractApiErrorMessage } from '../../api/client/error';
import { listWorkflowProfiles } from '../../api/system/workflows.api';
import { getStorageProviderOverview } from '../../api/system/storage.api';
import { useIsMobile } from '../../composables/useIsMobile';
import { useAuthPermissions } from '../../composables/useAuthPermissions';
import { PERMISSIONS } from '../../constants/permissions';
import type {
  AssetDerivativeSummaryResponse,
  AssetLatestExecutionStepSummaryResponse,
  AssetResponse,
  BasicCaptionStructuredResultPayload,
  PatchAssetInput,
} from '../../types/assets';
import { isAssetPending } from '../../types/assets';
import type { StorageProviderOverviewResponse } from '../../types/storage';
import type { WorkflowProfileResponse } from '../../types/workflows';
import { formatDateTime, formatFileSize } from '../../utils/format';

interface EditableAssetMetadata {
  originalFileName: string | null;
  description: string | null;
  altText: string | null;
}

const EXECUTION_TABLE_SCROLL_X = 860;
const PROCESSING_POLL_INTERVAL_MS = 5_000;
const FORCED_REFRESH_CYCLES_AFTER_TRIGGER = 12;

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const message = useMessage();
const { isMobile } = useIsMobile();
const { can } = useAuthPermissions();

const loading = ref(false);
const deleting = ref(false);
const savingMetadata = ref(false);
const updatingVisibility = ref(false);
const openingContent = ref(false);
const editingMetadata = ref(false);
const loadErrorMessage = ref('');
const asset = ref<AssetResponse | null>(null);
const backgroundRefreshing = ref(false);
const initialMetadata = ref<EditableAssetMetadata | null>(null);
const deleteCommitMessage = ref('');
const storageOverview = ref<StorageProviderOverviewResponse | null>(null);
const availableWorkflows = ref<WorkflowProfileResponse[]>([]);
const workflowsLoading = ref(false);
const workflowsLoadErrorMessage = ref('');
const selectedWorkflowId = ref<string | null>(null);
const runningWorkflowId = ref<string | null>(null);
const forcedRefreshCycles = ref(0);

const metadataForm = reactive({
  originalFileName: '',
  description: '',
  altText: '',
});

const assetId = computed(() => String(route.params.id ?? ''));
const hasLoadError = computed(() => loadErrorMessage.value.length > 0);
const isEmpty = computed(() => !loading.value && !hasLoadError.value && !asset.value);
const derivatives = computed<AssetDerivativeSummaryResponse[]>(() => asset.value?.derivatives ?? []);
const structuredResults = computed(() => asset.value?.structuredResults ?? []);
const latestBasicCaptionResult = computed(() => [...structuredResults.value]
  .reverse()
  .find((result) => result.kind === 'basic_caption') ?? null);
const hasMetadataChanges = computed(() => Object.keys(buildMetadataPatchPayload()).length > 0);
const isPublicAsset = computed(() => asset.value?.isPublic ?? false);
const visibilityDescription = computed(() => (
  isPublicAsset.value
    ? t('asset.detail.visibility.publicHint')
    : t('asset.detail.visibility.privateHint')
));
const derivativePreviewUnavailableText = computed(() => (
  isPublicAsset.value
    ? t('asset.detail.derivativePreviewUnavailable')
    : t('asset.detail.privateDerivativePreviewUnavailable')
));
const publicUrlDisplay = computed(() => {
  if (!asset.value || !asset.value.isPublic) {
    return '';
  }

  return asset.value.publicUrl ?? '';
});
const canUpdateAsset = computed(() => can(PERMISSIONS.assetsUpdate));
const canDeleteAsset = computed(() => can(PERMISSIONS.assetsDelete));
const canOpenWorkflowEditor = computed(() => can(PERMISSIONS.settingsRead));
const canOpenContent = computed(() => Boolean(asset.value));
const descriptionColumns = computed(() => (isMobile.value ? 1 : 2));
const isAssetProcessing = computed(() => isAssetPending(asset.value?.status));
const workflowOptions = computed(() => availableWorkflows.value.map((workflow) => ({
  label: workflow.name,
  value: workflow.id,
})));
const selectedWorkflow = computed(() => (
  availableWorkflows.value.find((workflow) => workflow.id === selectedWorkflowId.value) ?? null
));
const basicCaptionFallbackText = computed(() => {
  if (!latestBasicCaptionResult.value?.payloadJson) {
    return null;
  }

  try {
    const parsed = JSON.parse(latestBasicCaptionResult.value.payloadJson) as BasicCaptionStructuredResultPayload;
    const caption = parsed.caption?.trim();
    return caption ? caption : null;
  } catch {
    return null;
  }
});
const descriptionDisplayText = computed(() => {
  const description = asset.value?.description?.trim();
  return description || basicCaptionFallbackText.value || '-';
});
const altTextDisplayText = computed(() => {
  const altText = asset.value?.altText?.trim();
  return altText || basicCaptionFallbackText.value || '-';
});
const showMetadataFallbackHint = computed(() => Boolean(
  basicCaptionFallbackText.value
  && asset.value
  && (!asset.value.description?.trim() || !asset.value.altText?.trim()),
));
const isWorkflowActionBlocked = computed(() => (
  loading.value
  || deleting.value
  || savingMetadata.value
  || updatingVisibility.value
  || !canUpdateAsset.value
));
const shouldPollAsset = computed(() => isAssetProcessing.value || forcedRefreshCycles.value > 0);

let assetPollingTimerId: number | null = null;
const isGitHubAsset = computed(() => {
  if (!asset.value || !storageOverview.value) {
    return false;
  }

  if (asset.value.storageProviderProfileId) {
    const matchedProfile = storageOverview.value.profiles.find((profile) => (
      profile.id === asset.value?.storageProviderProfileId
    ));
    return matchedProfile?.providerType === 'github-repo';
  }

  return asset.value.storageProvider === storageOverview.value.runtime.providerName
    && storageOverview.value.runtime.providerType === 'github-repo';
});

const executionStepColumns = computed<DataTableColumns<AssetLatestExecutionStepSummaryResponse>>(() => [
  {
    title: t('asset.detail.executionColumns.stepName'),
    key: 'stepName',
  },
  {
    title: t('asset.detail.executionColumns.succeeded'),
    key: 'succeeded',
    width: 100,
    render: (row) =>
      h(
        NTag,
        { type: row.succeeded ? 'success' : 'error', size: 'small', bordered: false },
        () => (row.succeeded ? t('common.yes') : t('common.no')),
      ),
  },
  {
    title: t('asset.detail.executionColumns.errorMessage'),
    key: 'errorMessage',
    ellipsis: {
      tooltip: true,
    },
    render: (row) => row.errorMessage ?? '-',
  },
  {
    title: t('asset.detail.executionColumns.startedAt'),
    key: 'startedAtUtc',
    width: 180,
    render: (row) => formatDateTime(row.startedAtUtc),
  },
  {
    title: t('asset.detail.executionColumns.completedAt'),
    key: 'completedAtUtc',
    width: 180,
    render: (row) => formatDateTime(row.completedAtUtc),
  },
]);

function normalizeMetadataValue(value: string | null | undefined): string | null {
  if (value === null || value === undefined) {
    return null;
  }

  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}

function createEditableMetadata(source: AssetResponse): EditableAssetMetadata {
  return {
    originalFileName: normalizeMetadataValue(source.originalFileName),
    description: normalizeMetadataValue(source.description),
    altText: normalizeMetadataValue(source.altText),
  };
}

function syncMetadataForm(metadata: EditableAssetMetadata): void {
  metadataForm.originalFileName = metadata.originalFileName ?? '';
  metadataForm.description = metadata.description ?? '';
  metadataForm.altText = metadata.altText ?? '';
}

function clearMetadataForm(): void {
  metadataForm.originalFileName = '';
  metadataForm.description = '';
  metadataForm.altText = '';
}

function exitMetadataEdit(): void {
  editingMetadata.value = false;
  initialMetadata.value = null;
}

function buildMetadataPatchPayload(): PatchAssetInput {
  if (!initialMetadata.value) {
    return {};
  }

  const currentMetadata: EditableAssetMetadata = {
    originalFileName: normalizeMetadataValue(metadataForm.originalFileName),
    description: normalizeMetadataValue(metadataForm.description),
    altText: normalizeMetadataValue(metadataForm.altText),
  };

  const payload: PatchAssetInput = {};
  if (currentMetadata.originalFileName !== initialMetadata.value.originalFileName) {
    payload.originalFileName = currentMetadata.originalFileName;
  }

  if (currentMetadata.description !== initialMetadata.value.description) {
    payload.description = currentMetadata.description;
  }

  if (currentMetadata.altText !== initialMetadata.value.altText) {
    payload.altText = currentMetadata.altText;
  }

  return payload;
}

function startMetadataEdit(): void {
  if (!asset.value) {
    return;
  }

  const metadata = createEditableMetadata(asset.value);
  initialMetadata.value = metadata;
  syncMetadataForm(metadata);
  editingMetadata.value = true;
}

function cancelMetadataEdit(): void {
  if (asset.value) {
    syncMetadataForm(createEditableMetadata(asset.value));
  } else {
    clearMetadataForm();
  }

  exitMetadataEdit();
}

async function loadAsset(options: { silent?: boolean } = {}): Promise<void> {
  if (!assetId.value) {
    asset.value = null;
    loadErrorMessage.value = '';
    exitMetadataEdit();
    clearMetadataForm();
    return;
  }

  const silent = options.silent ?? false;
  if (silent) {
    if (loading.value || backgroundRefreshing.value) {
      return;
    }

    backgroundRefreshing.value = true;
  } else {
    loading.value = true;
    loadErrorMessage.value = '';
  }

  try {
    asset.value = await getAsset(assetId.value);
    loadErrorMessage.value = '';
  } catch (error) {
    if (!silent) {
      loadErrorMessage.value = extractApiErrorMessage(error);
      asset.value = null;
      message.error(`${t('asset.detail.loadFailed')}: ${loadErrorMessage.value}`);
    }
  } finally {
    if (silent) {
      backgroundRefreshing.value = false;
    } else {
      loading.value = false;
    }
  }
}

async function loadStorageOverview(): Promise<void> {
  try {
    storageOverview.value = await getStorageProviderOverview();
  } catch {
    storageOverview.value = null;
  }
}

async function loadWorkflows(): Promise<void> {
  workflowsLoading.value = true;
  workflowsLoadErrorMessage.value = '';

  try {
    const workflows = await listWorkflowProfiles();
    availableWorkflows.value = workflows;

    // 默认优先选自动运行 workflow；没有时再回退到第一个，减少详情页手动触发时的额外选择成本。
    if (!selectedWorkflowId.value || !workflows.some((workflow) => workflow.id === selectedWorkflowId.value)) {
      selectedWorkflowId.value = workflows.find((workflow) => workflow.isAutoRun)?.id ?? workflows[0]?.id ?? null;
    }
  } catch (error) {
    workflowsLoadErrorMessage.value = extractApiErrorMessage(error);
    availableWorkflows.value = [];
    selectedWorkflowId.value = null;
  } finally {
    workflowsLoading.value = false;
  }
}

async function handleDelete(): Promise<void> {
  if (!canDeleteAsset.value) {
    message.warning(t('auth.permissionDenied'));
    return;
  }

  if (!asset.value) {
    return;
  }

  deleting.value = true;

  try {
    await deleteAsset(asset.value.id, isGitHubAsset.value
      ? { commitMessage: deleteCommitMessage.value }
      : undefined);
    message.success(t('common.deleteSuccess'));
    await router.push({
      path: '/assets',
      query: route.query,
    });
  } catch (error) {
    message.error(`${t('common.deleteFailed')}: ${extractApiErrorMessage(error)}`);
  } finally {
    deleting.value = false;
  }
}

async function handleMetadataSave(): Promise<void> {
  if (!canUpdateAsset.value) {
    message.warning(t('auth.permissionDenied'));
    return;
  }

  if (!asset.value || !initialMetadata.value) {
    return;
  }

  const payload = buildMetadataPatchPayload();
  if (Object.keys(payload).length === 0) {
    cancelMetadataEdit();
    return;
  }

  savingMetadata.value = true;

  try {
    const patchedAsset = await patchAsset(asset.value.id, payload);
    let latestAsset = patchedAsset;
    loadErrorMessage.value = '';

    try {
      // PATCH 返回的数据足够展示，但这里仍再读一次详情，统一收敛服务端可能补齐的衍生字段。
      latestAsset = await getAsset(patchedAsset.id);
    } catch {
      message.warning(t('asset.detail.metadataEdit.refreshWarning'));
    }

    asset.value = latestAsset;
    syncMetadataForm(createEditableMetadata(latestAsset));
    exitMetadataEdit();
    message.success(t('asset.detail.metadataEdit.saveSuccess'));
  } catch (error) {
    message.error(`${t('asset.detail.metadataEdit.saveFailed')}: ${extractApiErrorMessage(error)}`);
  } finally {
    savingMetadata.value = false;
  }
}

async function handleVisibilityToggle(): Promise<void> {
  if (!canUpdateAsset.value) {
    message.warning(t('auth.permissionDenied'));
    return;
  }

  if (!asset.value) {
    return;
  }

  const nextIsPublic = !asset.value.isPublic;
  updatingVisibility.value = true;

  try {
    const patchedAsset = await patchAsset(asset.value.id, { isPublic: nextIsPublic });
    let latestAsset = patchedAsset;
    loadErrorMessage.value = '';

    try {
      latestAsset = await getAsset(patchedAsset.id);
    } catch {
      message.warning(t('asset.detail.visibility.refreshWarning'));
    }

    asset.value = latestAsset;
    message.success(
      nextIsPublic
        ? t('asset.detail.visibility.makePublicSuccess')
        : t('asset.detail.visibility.makePrivateSuccess'),
    );
  } catch (error) {
    message.error(`${t('asset.detail.visibility.updateFailed')}: ${extractApiErrorMessage(error)}`);
  } finally {
    updatingVisibility.value = false;
  }
}

function openExternal(url: string): void {
  const popup = window.open(url, '_blank', 'noopener,noreferrer');
  if (!popup) {
    window.location.assign(url);
  }
}

async function handleOpenContent(): Promise<void> {
  if (!asset.value || openingContent.value) {
    return;
  }

  if (asset.value.isPublic && asset.value.publicUrl) {
    openExternal(asset.value.publicUrl);
    return;
  }

  openingContent.value = true;

  try {
    const blob = await getAssetContentBlob(asset.value.id);
    const objectUrl = URL.createObjectURL(blob);
    const popup = window.open(objectUrl, '_blank', 'noopener,noreferrer');
    if (!popup) {
      message.warning(t('asset.detail.openContentBlocked'));
    }

    // 给新窗口留出读取时间后再回收 object URL，避免下载流刚创建就被提前释放。
    window.setTimeout(() => URL.revokeObjectURL(objectUrl), 60_000);
  } catch (error) {
    message.error(`${t('asset.detail.openContentFailed')}: ${extractApiErrorMessage(error)}`);
  } finally {
    openingContent.value = false;
  }
}

async function handleRunWorkflow(): Promise<void> {
  if (!canUpdateAsset.value) {
    message.warning(t('auth.permissionDenied'));
    return;
  }

  const workflow = selectedWorkflow.value;
  if (!asset.value || !workflow || runningWorkflowId.value) {
    return;
  }

  runningWorkflowId.value = workflow.id;

  try {
    await runAssetWorkflow(asset.value.id, workflow.id);
    message.success(t('asset.detail.workflows.runSuccess', { workflowName: workflow.name }));
    // 手动触发后即使资产很快回到 ready，也保留一小段强制轮询窗口，尽量把最新执行摘要刷回来。
    forcedRefreshCycles.value = FORCED_REFRESH_CYCLES_AFTER_TRIGGER;
    await loadAsset({ silent: true });
    if (isAssetProcessing.value) {
      forcedRefreshCycles.value = 0;
    }
  } catch (error) {
    message.error(`${t('asset.detail.workflows.runFailed', { workflowName: workflow.name })}: ${extractApiErrorMessage(error)}`);
  } finally {
    runningWorkflowId.value = null;
  }
}

function handleOpenWorkflowEditor(): void {
  void router.push('/workflows');
}

async function handleBackgroundRefresh(): Promise<void> {
  await loadAsset({ silent: true });
}

async function handleAssetRefresh(): Promise<void> {
  await loadAsset();
}

function startAssetPolling(): void {
  if (assetPollingTimerId !== null) {
    return;
  }

  assetPollingTimerId = window.setInterval(() => {
    void pollAssetInBackground();
  }, PROCESSING_POLL_INTERVAL_MS);
}

function stopAssetPolling(): void {
  if (assetPollingTimerId === null) {
    return;
  }

  window.clearInterval(assetPollingTimerId);
  assetPollingTimerId = null;
}

async function pollAssetInBackground(): Promise<void> {
  await loadAsset({ silent: true });

  if (forcedRefreshCycles.value === 0) {
    return;
  }

  if (isAssetProcessing.value) {
    // 一旦重新进入 processing，后续轮询交给 shouldPollAsset 维护，不再消耗强制刷新计数。
    forcedRefreshCycles.value = 0;
    return;
  }

  forcedRefreshCycles.value -= 1;
}

function backToList(): void {
  void router.push({
    path: '/assets',
    query: route.query,
  });
}

watch(
  () => assetId.value,
  () => {
    stopAssetPolling();
    forcedRefreshCycles.value = 0;
    exitMetadataEdit();
    clearMetadataForm();
    deleteCommitMessage.value = '';
    void loadAsset();
  },
  { immediate: true },
);

watch(
  shouldPollAsset,
  (nextValue) => {
    if (nextValue) {
      startAssetPolling();
      return;
    }

    stopAssetPolling();
  },
  { immediate: true },
);

onMounted(() => {
  void loadStorageOverview();
  void loadWorkflows();
});

onBeforeUnmount(() => {
  stopAssetPolling();
});
</script>

<template>
  <div>
    <page-header :title="t('asset.detail.title')" :description="t('asset.detail.description')">
      <template #actions>
        <n-space wrap>
          <n-button @click="backToList">{{ t('asset.detail.backToList') }}</n-button>
          <n-button :loading="loading" @click="handleAssetRefresh">{{ t('common.refresh') }}</n-button>
          <n-button v-if="canOpenContent" type="primary" ghost :loading="openingContent" @click="handleOpenContent">
            {{ t('asset.detail.openContent') }}
          </n-button>
        </n-space>
      </template>
    </page-header>

    <n-result
      v-if="hasLoadError"
      status="error"
      :title="t('asset.detail.loadFailed')"
      :description="loadErrorMessage"
    >
      <template #footer>
        <n-space wrap>
          <n-button type="primary" @click="handleAssetRefresh">{{ t('common.retry') }}</n-button>
          <n-button @click="backToList">{{ t('asset.detail.backToList') }}</n-button>
        </n-space>
      </template>
    </n-result>

    <n-empty v-else-if="isEmpty" :description="t('common.noData')" style="padding: 48px 0" />

    <n-space v-else vertical :size="16">
      <n-card :title="t('asset.detail.basicInfo')" :loading="loading" class="section-card">
        <template #header-extra>
          <n-tag v-if="isAssetProcessing" type="warning" size="small" :bordered="false">
            {{ t('asset.detail.processing.badge') }}
          </n-tag>
        </template>

        <n-descriptions v-if="asset" label-placement="left" bordered :column="descriptionColumns">
          <n-descriptions-item :label="t('asset.detail.fields.id')">{{ asset.id }}</n-descriptions-item>
          <n-descriptions-item :label="t('asset.detail.fields.status')">
            <asset-status-tag :status="asset.status" />
          </n-descriptions-item>
          <n-descriptions-item :label="t('asset.detail.fields.visibility')">
            <asset-visibility-tag :is-public="asset.isPublic" />
          </n-descriptions-item>
          <n-descriptions-item :label="t('asset.detail.fields.fileName')">{{ asset.originalFileName || '-' }}</n-descriptions-item>
          <n-descriptions-item :label="t('asset.detail.fields.type')">{{ asset.type }}</n-descriptions-item>
          <n-descriptions-item :label="t('asset.detail.fields.contentType')">{{ asset.contentType }}</n-descriptions-item>
          <n-descriptions-item :label="t('asset.detail.fields.size')">{{ formatFileSize(asset.size) }}</n-descriptions-item>
          <n-descriptions-item :label="t('asset.detail.fields.width')">{{ asset.width ?? '-' }}</n-descriptions-item>
          <n-descriptions-item :label="t('asset.detail.fields.height')">{{ asset.height ?? '-' }}</n-descriptions-item>
          <n-descriptions-item :label="t('asset.detail.fields.createdAt')">{{ formatDateTime(asset.createdAtUtc) }}</n-descriptions-item>
          <n-descriptions-item :label="t('asset.detail.fields.updatedAt')">{{ formatDateTime(asset.updatedAtUtc) }}</n-descriptions-item>
        </n-descriptions>
      </n-card>

      <n-alert v-if="isAssetProcessing" type="info" data-testid="asset-detail-processing-banner">
        <template #header>{{ t('asset.detail.processing.title') }}</template>
        <n-space vertical :size="8">
          <div>{{ t('asset.detail.backgroundProcessingHint') }}</div>
          <div class="processing-alert-footer">
            <span class="metadata-edit-hint">{{ t('asset.detail.processing.autoRefresh') }}</span>
            <n-button text size="small" :loading="backgroundRefreshing" @click="handleBackgroundRefresh">
              {{ t('common.refresh') }}
            </n-button>
          </div>
        </n-space>
      </n-alert>

      <n-card :title="t('asset.detail.metadata')" :loading="loading" class="section-card">
        <template #header-extra>
          <n-button
            v-if="asset && !editingMetadata"
            size="small"
            ghost
            type="primary"
            :disabled="updatingVisibility || !canUpdateAsset"
            @click="startMetadataEdit"
          >
            {{ t('asset.detail.metadataEdit.editAction') }}
          </n-button>
        </template>

        <n-space v-if="asset" vertical :size="12">
          <n-card size="small" :title="t('asset.detail.visibility.title')">
            <n-space vertical :size="12">
              <div class="visibility-row">
                <div class="visibility-current">
                  <div class="section-label">{{ t('asset.detail.visibility.current') }}</div>
                  <asset-visibility-tag :is-public="asset.isPublic" />
                </div>

                <n-button
                  size="small"
                  ghost
                  type="primary"
                  :loading="updatingVisibility"
                  :disabled="editingMetadata || savingMetadata || loading || !canUpdateAsset"
                  @click="handleVisibilityToggle"
                >
                  {{
                    asset.isPublic
                      ? t('asset.detail.visibility.makePrivate')
                      : t('asset.detail.visibility.makePublic')
                  }}
                </n-button>
              </div>

              <div class="metadata-edit-hint">{{ visibilityDescription }}</div>

              <n-descriptions label-placement="left" bordered :column="1">
                <n-descriptions-item :label="t('asset.detail.visibility.publicUrlLabel')">
                  <span v-if="publicUrlDisplay" class="url-text">{{ publicUrlDisplay }}</span>
                  <span v-else class="muted-text">
                    {{
                      asset.isPublic
                        ? t('asset.detail.visibility.publicUrlResolvedAtOpen')
                        : t('asset.detail.visibility.privateNoPublicUrl')
                    }}
                  </span>
                </n-descriptions-item>
              </n-descriptions>
            </n-space>
          </n-card>

          <n-card v-if="editingMetadata" size="small" :title="t('asset.detail.metadataEdit.formTitle')">
            <n-space vertical :size="12">
              <div class="metadata-edit-hint">{{ t('asset.detail.metadataEdit.hint') }}</div>

              <n-form label-placement="top">
                <n-form-item :label="t('asset.detail.fields.fileName')">
                  <n-input
                    v-model:value="metadataForm.originalFileName"
                    clearable
                    :disabled="savingMetadata"
                  />
                </n-form-item>

                <n-form-item :label="t('asset.detail.fields.description')">
                  <n-input
                    v-model:value="metadataForm.description"
                    type="textarea"
                    :autosize="{ minRows: 2, maxRows: 4 }"
                    :disabled="savingMetadata"
                  />
                </n-form-item>

                <n-form-item :label="t('asset.detail.fields.altText')">
                  <n-input
                    v-model:value="metadataForm.altText"
                    type="textarea"
                    :autosize="{ minRows: 2, maxRows: 4 }"
                    :disabled="savingMetadata"
                  />
                </n-form-item>

                <n-space wrap>
                  <n-button
                    type="primary"
                    :loading="savingMetadata"
                    :disabled="!hasMetadataChanges"
                    @click="handleMetadataSave"
                  >
                    {{ t('common.save') }}
                  </n-button>
                  <n-button :disabled="savingMetadata" @click="cancelMetadataEdit">
                    {{ t('common.cancel') }}
                  </n-button>
                </n-space>
              </n-form>
            </n-space>
          </n-card>

          <n-card v-else size="small" :title="t('asset.detail.metadataGroups.text')">
            <n-descriptions label-placement="left" bordered :column="1">
              <n-descriptions-item :label="t('asset.detail.fields.description')">{{ descriptionDisplayText }}</n-descriptions-item>
              <n-descriptions-item :label="t('asset.detail.fields.altText')">{{ altTextDisplayText }}</n-descriptions-item>
            </n-descriptions>
            <div v-if="showMetadataFallbackHint" class="metadata-edit-hint">
              {{ t('asset.detail.metadataFallbackHint') }}
            </div>
          </n-card>

          <n-card size="small" :title="t('asset.detail.metadataGroups.storage')">
            <n-descriptions label-placement="left" bordered :column="1">
              <n-descriptions-item :label="t('asset.detail.fields.storageProvider')">{{ asset.storageProvider }}</n-descriptions-item>
              <n-descriptions-item :label="t('asset.detail.fields.storageKey')">{{ asset.storageKey }}</n-descriptions-item>
              <n-descriptions-item :label="t('asset.detail.fields.extension')">{{ asset.extension }}</n-descriptions-item>
              <n-descriptions-item :label="t('asset.detail.fields.checksum')">{{ asset.checksumSha256 || '-' }}</n-descriptions-item>
            </n-descriptions>
          </n-card>
        </n-space>
      </n-card>

      <n-card :title="t('asset.detail.derivatives')" :loading="loading" class="section-card">
        <n-empty v-if="derivatives.length === 0" :description="t('asset.detail.noDerivatives')" />

        <div v-else class="derivative-grid">
          <n-card v-for="(item, index) in derivatives" :key="`${item.kind}-${item.createdAtUtc}-${index}`" size="small">
            <n-space vertical :size="10">
              <div class="section-label">{{ t('asset.detail.preview') }}</div>

              <div class="preview-box">
                <n-image
                  v-if="item.publicUrl"
                  :src="item.publicUrl"
                  object-fit="cover"
                  width="220"
                  preview-disabled
                />
                <n-empty v-else :description="derivativePreviewUnavailableText" />
              </div>

              <n-button v-if="item.publicUrl" size="small" quaternary type="primary" @click="openExternal(item.publicUrl)">
                {{ t('asset.detail.openDerivative') }}
              </n-button>

              <div class="section-label">{{ t('asset.detail.derivativeInfo') }}</div>
              <dl class="info-list">
                <dt>{{ t('asset.detail.derivativeFields.kind') }}</dt>
                <dd>{{ item.kind }}</dd>
                <dt>{{ t('asset.detail.derivativeFields.contentType') }}</dt>
                <dd>{{ item.contentType }}</dd>
                <dt>{{ t('asset.detail.fields.extension') }}</dt>
                <dd>{{ item.extension }}</dd>
                <dt>{{ t('asset.detail.derivativeFields.size') }}</dt>
                <dd>{{ formatFileSize(item.size) }}</dd>
                <dt>{{ t('asset.detail.derivativeFields.dimension') }}</dt>
                <dd>{{ item.width ?? '-' }} x {{ item.height ?? '-' }}</dd>
                <dt>{{ t('asset.detail.derivativeFields.createdAt') }}</dt>
                <dd>{{ formatDateTime(item.createdAtUtc) }}</dd>
              </dl>
            </n-space>
          </n-card>
        </div>
      </n-card>

      <n-card :title="t('asset.detail.workflows.title')" class="section-card">
        <template #header-extra>
          <n-space :size="8">
            <n-button text :loading="workflowsLoading" @click="loadWorkflows">
              {{ t('common.refresh') }}
            </n-button>
            <n-button v-if="canOpenWorkflowEditor" text type="primary" @click="handleOpenWorkflowEditor">
              {{ t('asset.detail.workflows.openEditor') }}
            </n-button>
          </n-space>
        </template>

        <n-space vertical :size="12">
          <n-alert v-if="workflowsLoadErrorMessage" type="warning" :show-icon="false">
            {{ t('asset.detail.workflows.loadFailed') }}: {{ workflowsLoadErrorMessage }}
          </n-alert>

          <n-empty
            v-else-if="availableWorkflows.length === 0"
            :description="t('asset.detail.workflows.empty')"
          >
            <template #extra>
              <n-button v-if="canOpenWorkflowEditor" size="small" type="primary" ghost @click="handleOpenWorkflowEditor">
                {{ t('asset.detail.workflows.openEditor') }}
              </n-button>
            </template>
          </n-empty>

          <template v-else>
            <n-form label-placement="top">
              <n-form-item :label="t('asset.detail.workflows.selectLabel')">
                <n-select
                  v-model:value="selectedWorkflowId"
                  :options="workflowOptions"
                  :placeholder="t('asset.detail.workflows.selectPlaceholder')"
                  :disabled="workflowsLoading || !canUpdateAsset"
                />
              </n-form-item>
            </n-form>

            <n-card v-if="selectedWorkflow" size="small">
              <n-space vertical :size="10">
                <div class="section-label">{{ selectedWorkflow.name }}</div>
                <div class="skill-description">
                  {{ selectedWorkflow.description || t('asset.detail.workflows.noDescription') }}
                </div>
                <n-space wrap :size="8" align="center">
                  <n-tag
                    v-if="selectedWorkflow.isAutoRun"
                    size="small"
                    type="success"
                    :bordered="false"
                  >
                    {{ t('asset.detail.workflows.autoRunTag') }}
                  </n-tag>
                  <span class="metadata-edit-hint">{{ t('asset.detail.workflows.executionHint') }}</span>
                </n-space>
                <n-button
                  data-testid="asset-workflow-run"
                  size="small"
                  type="primary"
                  ghost
                  :loading="runningWorkflowId === selectedWorkflow.id"
                  :disabled="isWorkflowActionBlocked || !selectedWorkflowId"
                  @click="handleRunWorkflow"
                >
                  {{
                    runningWorkflowId === selectedWorkflow.id
                      ? t('asset.detail.workflows.runningAction')
                      : t('asset.detail.workflows.runAction')
                  }}
                </n-button>
              </n-space>
            </n-card>
          </template>
        </n-space>
      </n-card>

      <n-card :title="t('asset.detail.structuredResults')" :loading="loading" class="section-card">
        <n-empty v-if="structuredResults.length === 0" :description="t('asset.detail.noStructuredResults')" />

        <div v-else class="structured-grid">
          <structured-result-renderer
            v-for="(item, index) in structuredResults"
            :key="`${item.kind}-${item.createdAtUtc}-${index}`"
            :result="item"
          />
        </div>
      </n-card>

      <n-card :title="t('asset.detail.latestExecutionSummary')" :loading="loading" class="section-card">
        <n-empty
          v-if="!asset || !asset.latestExecutionSummary"
          :description="t('asset.detail.noExecutionSummary')"
        />

        <n-space v-else vertical :size="12">
          <n-descriptions bordered :column="descriptionColumns" label-placement="left">
            <n-descriptions-item :label="t('asset.detail.executionSummaryFields.skillName')">
              {{ asset.latestExecutionSummary.skillName }}
            </n-descriptions-item>
            <n-descriptions-item :label="t('asset.detail.executionSummaryFields.succeeded')">
              {{ asset.latestExecutionSummary.succeeded ? t('common.yes') : t('common.no') }}
            </n-descriptions-item>
            <n-descriptions-item :label="t('asset.detail.executionSummaryFields.triggerSource')">
              {{ asset.latestExecutionSummary.triggerSource }}
            </n-descriptions-item>
            <n-descriptions-item :label="t('asset.detail.executionSummaryFields.startedAt')">
              {{ formatDateTime(asset.latestExecutionSummary.startedAtUtc) }}
            </n-descriptions-item>
            <n-descriptions-item :label="t('asset.detail.executionSummaryFields.completedAt')">
              {{ formatDateTime(asset.latestExecutionSummary.completedAtUtc) }}
            </n-descriptions-item>
          </n-descriptions>

          <div v-if="asset.latestExecutionSummary.steps.length > 0" class="table-wrapper">
            <n-data-table
              :columns="executionStepColumns"
              :data="asset.latestExecutionSummary.steps"
              :row-key="(row) => `${row.stepName}-${row.startedAtUtc}`"
              :scroll-x="EXECUTION_TABLE_SCROLL_X"
            />
          </div>
        </n-space>
      </n-card>

      <n-card v-if="canDeleteAsset" :title="t('asset.detail.deleteAreaTitle')" class="section-card">
        <n-space vertical :size="12">
          <div class="danger-text">{{ t('asset.detail.deleteAreaHint') }}</div>
          <n-form v-if="isGitHubAsset" label-placement="top">
            <n-form-item :label="t('asset.detail.commitMessageLabel')">
              <n-input
                v-model:value="deleteCommitMessage"
                type="textarea"
                :autosize="{ minRows: 2, maxRows: 4 }"
                :placeholder="t('asset.detail.commitMessagePlaceholder')"
                :disabled="deleting"
              />
              <div class="metadata-edit-hint">{{ t('asset.detail.commitMessageHint') }}</div>
            </n-form-item>
          </n-form>
          <n-popconfirm
            :negative-text="t('common.cancel')"
            :positive-text="t('common.delete')"
            @positive-click="handleDelete"
          >
            <template #trigger>
              <n-button type="error" :loading="deleting">
                {{ t('common.delete') }}
              </n-button>
            </template>
            {{ t('common.confirmDelete') }}
          </n-popconfirm>
        </n-space>
      </n-card>
    </n-space>
  </div>
</template>

<style scoped>
.section-card :deep(.n-card-header__main) {
  font-weight: 600;
}

.section-card :deep(.n-card-header) {
  gap: 8px;
  flex-wrap: wrap;
}

.section-card :deep(.n-descriptions-table-content) {
  word-break: break-word;
}

.section-label {
  font-size: 13px;
  color: #6b7280;
  font-weight: 600;
}

.derivative-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
  gap: 12px;
}

.preview-box {
  min-height: 120px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.preview-box :deep(img) {
  max-width: 100%;
  height: auto;
}

.structured-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
  gap: 12px;
}

.skill-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
  gap: 12px;
}

.skill-description,
.skill-steps {
  color: #4b5563;
  font-size: 13px;
  line-height: 1.6;
}

.info-list {
  margin: 0;
  display: grid;
  grid-template-columns: 108px 1fr;
  row-gap: 6px;
  column-gap: 8px;
}

.info-list dt {
  color: #6b7280;
}

.info-list dd {
  margin: 0;
  color: #111827;
  word-break: break-word;
}

.danger-text {
  color: #991b1b;
}

.metadata-edit-hint {
  font-size: 13px;
  color: #6b7280;
}

.processing-alert-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
}

.visibility-row {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 12px;
  flex-wrap: wrap;
}

.visibility-current {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.url-text {
  word-break: break-all;
}

.muted-text {
  color: #6b7280;
}

.table-wrapper {
  overflow-x: auto;
}

@media (max-width: 768px) {
  .derivative-grid,
  .structured-grid {
    grid-template-columns: 1fr;
  }

  .info-list {
    grid-template-columns: 96px 1fr;
  }
}
</style>
