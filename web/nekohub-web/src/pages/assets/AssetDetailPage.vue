<script setup lang="ts">
import { computed, h, ref, watch } from 'vue';
import type { DataTableColumns } from 'naive-ui';
import {
  NButton,
  NCard,
  NDataTable,
  NDescriptions,
  NDescriptionsItem,
  NEmpty,
  NImage,
  NPopconfirm,
  NResult,
  NSpace,
  NTag,
  useMessage,
} from 'naive-ui';
import { useI18n } from 'vue-i18n';
import { useRoute, useRouter } from 'vue-router';
import PageHeader from '../../components/common/PageHeader.vue';
import AssetStatusTag from '../../components/assets/AssetStatusTag.vue';
import StructuredResultRenderer from '../../components/assets/structured-results/StructuredResultRenderer.vue';
import { buildAssetContentUrl, deleteAssetById, getAssetById } from '../../api/assets/assets.api';
import { extractApiErrorMessage } from '../../api/client/error';
import { useAppConfigStore } from '../../stores/app-config';
import type {
  AssetDerivativeSummaryResponse,
  AssetLatestExecutionStepSummaryResponse,
  AssetResponse,
} from '../../types/assets';
import { formatDateTime, formatFileSize } from '../../utils/format';

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const message = useMessage();
const appConfigStore = useAppConfigStore();

const loading = ref(false);
const deleting = ref(false);
const loadErrorMessage = ref('');
const asset = ref<AssetResponse | null>(null);

const assetId = computed(() => String(route.params.id ?? ''));
const hasLoadError = computed(() => loadErrorMessage.value.length > 0);
const isEmpty = computed(() => !loading.value && !hasLoadError.value && !asset.value);
const derivatives = computed<AssetDerivativeSummaryResponse[]>(() => asset.value?.derivatives ?? []);
const structuredResults = computed(() => asset.value?.structuredResults ?? []);

const contentUrl = computed(() => {
  if (!asset.value) {
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

async function loadAsset(): Promise<void> {
  if (!assetId.value) {
    asset.value = null;
    loadErrorMessage.value = '';
    return;
  }

  loading.value = true;
  loadErrorMessage.value = '';

  try {
    asset.value = await getAssetById(assetId.value);
  } catch (error) {
    loadErrorMessage.value = extractApiErrorMessage(error);
    asset.value = null;
    message.error(`${t('asset.detail.loadFailed')}: ${loadErrorMessage.value}`);
  } finally {
    loading.value = false;
  }
}

async function handleDelete(): Promise<void> {
  if (!asset.value) {
    return;
  }

  deleting.value = true;

  try {
    await deleteAssetById(asset.value.id);
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
    void loadAsset();
  },
  { immediate: true },
);
</script>

<template>
  <div>
    <page-header :title="t('asset.detail.title')" :description="t('asset.detail.description')">
      <template #actions>
        <n-space>
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
        <n-space>
          <n-button type="primary" @click="loadAsset">{{ t('common.retry') }}</n-button>
          <n-button @click="backToList">{{ t('asset.detail.backToList') }}</n-button>
        </n-space>
      </template>
    </n-result>

    <n-empty v-else-if="isEmpty" :description="t('common.noData')" style="padding: 48px 0" />

    <n-space v-else vertical :size="16">
      <n-card :title="t('asset.detail.basicInfo')" :loading="loading" class="section-card">
        <n-descriptions v-if="asset" label-placement="left" bordered :column="2">
          <n-descriptions-item :label="t('asset.detail.fields.id')">{{ asset.id }}</n-descriptions-item>
          <n-descriptions-item :label="t('asset.detail.fields.status')">
            <asset-status-tag :status="asset.status" />
          </n-descriptions-item>
          <n-descriptions-item :label="t('asset.detail.fields.fileName')">{{ asset.originalFileName }}</n-descriptions-item>
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
        <n-space v-if="asset" vertical :size="12">
          <n-card size="small" :title="t('asset.detail.metadataGroups.text')">
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
                <n-empty v-else :description="t('asset.detail.derivativePreviewUnavailable')" />
              </div>

              <n-button v-if="item.publicUrl" size="small" quaternary type="primary" @click="openExternal(item.publicUrl)">
                {{ t('asset.detail.openOriginal') }}
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

      <n-card :title="t('asset.detail.latestExecutionSummary')" :loading="loading" class="section-card">
        <n-empty
          v-if="!asset || !asset.latestExecutionSummary"
          :description="t('asset.detail.noExecutionSummary')"
        />

        <n-space v-else vertical :size="12">
          <n-descriptions bordered :column="2" label-placement="left">
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

          <n-data-table
            v-if="asset.latestExecutionSummary.steps.length > 0"
            :columns="executionStepColumns"
            :data="asset.latestExecutionSummary.steps"
            :row-key="(row) => `${row.stepName}-${row.startedAtUtc}`"
          />
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
              <n-button type="error" :loading="deleting">{{ t('common.delete') }}</n-button>
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
}

.danger-text {
  color: #991b1b;
}
</style>
