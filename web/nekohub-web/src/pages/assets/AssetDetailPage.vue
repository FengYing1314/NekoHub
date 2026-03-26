<script setup lang="ts">
import { computed, h, reactive, ref, watch } from 'vue';
import type { DataTableColumns, SelectOption } from 'naive-ui';
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
  buildAssetContentUrl,
  deleteAsset,
  getAsset,
  listSkills,
  patchAsset,
  runAssetSkill,
} from '../../api/assets/assets.api';
import { extractApiErrorMessage } from '../../api/client/error';
import { useIsMobile } from '../../composables/useIsMobile';
import { useAppConfigStore } from '../../stores/app-config';
import type {
  AssetDerivativeSummaryResponse,
  AssetLatestExecutionStepSummaryResponse,
  AssetResponse,
  AssetSkillSummaryResponse,
  PatchAssetInput,
} from '../../types/assets';
import { formatDateTime, formatFileSize } from '../../utils/format';

interface EditableAssetMetadata {
  originalFileName: string | null;
  description: string | null;
  altText: string | null;
}

const EXECUTION_TABLE_SCROLL_X = 860;

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const message = useMessage();
const appConfigStore = useAppConfigStore();
const { isMobile } = useIsMobile();

const loading = ref(false);
const deleting = ref(false);
const savingMetadata = ref(false);
const updatingVisibility = ref(false);
const editingMetadata = ref(false);
const loadingSkills = ref(false);
const runningSkill = ref(false);
const loadErrorMessage = ref('');
const skillLoadErrorMessage = ref('');
const asset = ref<AssetResponse | null>(null);
const initialMetadata = ref<EditableAssetMetadata | null>(null);
const availableSkills = ref<AssetSkillSummaryResponse[]>([]);
const selectedSkillName = ref('');

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
const hasMetadataChanges = computed(() => Object.keys(buildMetadataPatchPayload()).length > 0);
const hasSkillLoadError = computed(() => skillLoadErrorMessage.value.length > 0);
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

  return asset.value.publicUrl || contentUrl.value;
});
const skillOptions = computed<SelectOption[]>(() => (
  availableSkills.value.map((skill) => ({
    label: skill.skillName,
    value: skill.skillName,
  }))
));
const selectedSkill = computed(() => (
  availableSkills.value.find((skill) => skill.skillName === selectedSkillName.value) ?? null
));
const descriptionColumns = computed(() => (isMobile.value ? 1 : 2));

const contentUrl = computed(() => {
  if (!asset.value || !asset.value.isPublic) {
    return '';
  }

  return buildAssetContentUrl(appConfigStore.apiBaseUrl, asset.value.id);
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

async function loadAsset(): Promise<void> {
  if (!assetId.value) {
    asset.value = null;
    loadErrorMessage.value = '';
    exitMetadataEdit();
    clearMetadataForm();
    return;
  }

  loading.value = true;
  loadErrorMessage.value = '';

  try {
    asset.value = await getAsset(assetId.value);
  } catch (error) {
    loadErrorMessage.value = extractApiErrorMessage(error);
    asset.value = null;
    message.error(`${t('asset.detail.loadFailed')}: ${loadErrorMessage.value}`);
  } finally {
    loading.value = false;
  }
}

async function loadSkills(force = false): Promise<void> {
  if (loadingSkills.value) {
    return;
  }

  if (!force && availableSkills.value.length > 0 && !skillLoadErrorMessage.value) {
    return;
  }

  loadingSkills.value = true;
  skillLoadErrorMessage.value = '';

  try {
    const skills = await listSkills();
    availableSkills.value = skills;
    selectedSkillName.value = skills.some((skill) => skill.skillName === selectedSkillName.value)
      ? selectedSkillName.value
      : (skills[0]?.skillName ?? '');
  } catch (error) {
    availableSkills.value = [];
    selectedSkillName.value = '';
    skillLoadErrorMessage.value = extractApiErrorMessage(error);
  } finally {
    loadingSkills.value = false;
  }
}

async function handleDelete(): Promise<void> {
  if (!asset.value) {
    return;
  }

  deleting.value = true;

  try {
    await deleteAsset(asset.value.id);
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

function handleSkillSelection(nextValue: string | number | null): void {
  selectedSkillName.value = typeof nextValue === 'string' ? nextValue : '';
}

async function handleRunSkill(): Promise<void> {
  if (!asset.value) {
    return;
  }

  if (!selectedSkillName.value) {
    message.warning(t('asset.detail.skillRun.selectRequired'));
    return;
  }

  runningSkill.value = true;

  try {
    const runResult = await runAssetSkill(asset.value.id, selectedSkillName.value);
    let latestAsset: AssetResponse | null = null;
    loadErrorMessage.value = '';

    try {
      latestAsset = await getAsset(runResult.asset.id);
    } catch {
      latestAsset = {
        ...asset.value,
        ...runResult.asset,
      };
      message.warning(t('asset.detail.skillRun.refreshWarning'));
    }

    if (latestAsset) {
      asset.value = latestAsset;
    }
    message.success(t('asset.detail.skillRun.runSuccess'));
  } catch (error) {
    message.error(`${t('asset.detail.skillRun.runFailed')}: ${extractApiErrorMessage(error)}`);
  } finally {
    runningSkill.value = false;
  }
}

function openExternal(url: string): void {
  window.open(url, '_blank', 'noopener,noreferrer');
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
    exitMetadataEdit();
    clearMetadataForm();
    void loadAsset();
    void loadSkills();
  },
  { immediate: true },
);
</script>

<template>
  <div>
    <page-header :title="t('asset.detail.title')" :description="t('asset.detail.description')">
      <template #actions>
        <n-space wrap>
          <n-button @click="backToList">{{ t('asset.detail.backToList') }}</n-button>
          <n-button :loading="loading" @click="loadAsset">{{ t('common.refresh') }}</n-button>
          <n-button v-if="contentUrl" type="primary" ghost @click="openExternal(contentUrl)">
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
          <n-button type="primary" @click="loadAsset">{{ t('common.retry') }}</n-button>
          <n-button @click="backToList">{{ t('asset.detail.backToList') }}</n-button>
        </n-space>
      </template>
    </n-result>

    <n-empty v-else-if="isEmpty" :description="t('common.noData')" style="padding: 48px 0" />

    <n-space v-else vertical :size="16">
      <n-card :title="t('asset.detail.basicInfo')" :loading="loading" class="section-card">
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

      <n-card :title="t('asset.detail.metadata')" :loading="loading" class="section-card">
        <template #header-extra>
          <n-button
            v-if="asset && !editingMetadata"
            size="small"
            ghost
            type="primary"
            :disabled="updatingVisibility || runningSkill"
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
                  :disabled="editingMetadata || savingMetadata || loading || runningSkill"
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
                        ? t('asset.detail.visibility.publicUrlUnavailable')
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
              <n-descriptions-item :label="t('asset.detail.fields.description')">{{ asset.description || '-' }}</n-descriptions-item>
              <n-descriptions-item :label="t('asset.detail.fields.altText')">{{ asset.altText || '-' }}</n-descriptions-item>
            </n-descriptions>
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

      <n-card :title="t('asset.detail.skillRun.title')" :loading="loading" class="section-card" size="small">
        <template #header-extra>
          <n-button text size="small" :loading="loadingSkills" @click="loadSkills(true)">
            {{ t('common.refresh') }}
          </n-button>
        </template>

        <n-space vertical :size="12">
          <div class="metadata-edit-hint">{{ t('asset.detail.skillRun.hint') }}</div>

          <n-alert v-if="hasSkillLoadError" type="warning" :show-icon="false">
            <div class="skill-alert">
              <span>{{ t('asset.detail.skillRun.skillsLoadFailed') }}: {{ skillLoadErrorMessage }}</span>
              <n-button text size="small" :loading="loadingSkills" @click="loadSkills(true)">
                {{ t('common.retry') }}
              </n-button>
            </div>
          </n-alert>

          <n-empty
            v-if="!loadingSkills && availableSkills.length === 0"
            :description="t('asset.detail.skillRun.noSkills')"
          />

          <n-form v-else label-placement="top">
            <n-form-item :label="t('asset.detail.skillRun.selectLabel')">
              <n-select
                :value="selectedSkillName"
                :options="skillOptions"
                :loading="loadingSkills"
                :disabled="loading || runningSkill || editingMetadata || savingMetadata || updatingVisibility || deleting"
                @update:value="handleSkillSelection"
              />
            </n-form-item>

            <div v-if="selectedSkill" class="skill-meta-block">
              <div class="section-label">{{ t('asset.detail.skillRun.descriptionLabel') }}</div>
              <div class="skill-description">{{ selectedSkill.description }}</div>
              <div v-if="selectedSkill.steps.length > 0" class="metadata-edit-hint">
                {{ t('asset.detail.skillRun.stepsLabel') }}{{ selectedSkill.steps.join(' / ') }}
              </div>
            </div>

            <n-space wrap>
              <n-button
                type="primary"
                :loading="runningSkill"
                :disabled="loading || !selectedSkillName || loadingSkills || editingMetadata || savingMetadata || updatingVisibility || deleting"
                @click="handleRunSkill"
              >
                {{
                  runningSkill
                    ? t('asset.detail.skillRun.runningAction')
                    : t('asset.detail.skillRun.runAction')
                }}
              </n-button>
            </n-space>
          </n-form>
        </n-space>
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

      <n-card :title="t('asset.detail.deleteAreaTitle')" class="section-card">
        <n-space vertical :size="12">
          <div class="danger-text">{{ t('asset.detail.deleteAreaHint') }}</div>
          <n-popconfirm
            :negative-text="t('common.cancel')"
            :positive-text="t('common.delete')"
            @positive-click="handleDelete"
          >
            <template #trigger>
              <n-button type="error" :loading="deleting" :disabled="runningSkill">
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

.skill-alert {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
}

.skill-meta-block {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.skill-description {
  color: #111827;
  line-height: 1.6;
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
