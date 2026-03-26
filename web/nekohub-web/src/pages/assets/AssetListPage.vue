<script setup lang="ts">
import { computed, h, ref, watch } from 'vue';
import type { DataTableColumns, SelectOption } from 'naive-ui';
import {
  NAlert,
  NButton,
  NCard,
  NDataTable,
  NEmpty,
  NInput,
  NPagination,
  NPopconfirm,
  NResult,
  NSelect,
  NSpace,
  useMessage,
} from 'naive-ui';
import { useI18n } from 'vue-i18n';
import {
  useRoute,
  useRouter,
  type LocationQuery,
  type LocationQueryRaw,
} from 'vue-router';
import PageHeader from '../../components/common/PageHeader.vue';
import AssetStatusTag from '../../components/assets/AssetStatusTag.vue';
import { deleteAssetById, listAssets } from '../../api/assets/assets.api';
import { extractApiErrorMessage } from '../../api/client/error';
import type {
  AssetListItemResponse,
  AssetListSortBy,
  AssetListSortDirection,
  ListAssetsInput,
} from '../../types/assets';
import { formatDateTime, formatFileSize } from '../../utils/format';

const DEFAULT_PAGE = 1;
const DEFAULT_PAGE_SIZE = 20;
const DEFAULT_SORT_BY: AssetListSortBy = 'createdAt';
const DEFAULT_SORT_DIRECTION: AssetListSortDirection = 'desc';
const PAGE_SIZE_OPTIONS = [10, 20, 50];
const ASSET_QUERY_KEYS = ['page', 'pageSize', 'keyword', 'contentType', 'sortBy', 'sortDirection'] as const;

type SortOptionValue = 'createdAt:desc' | 'createdAt:asc' | 'size:desc' | 'size:asc';
type ContentTypeFilterValue = 'all' | 'image' | 'image/png' | 'image/jpeg' | 'image/webp' | 'image/gif';

interface AssetListRouteState {
  page: number;
  pageSize: number;
  keyword: string;
  contentType: ContentTypeFilterValue;
  sortBy: AssetListSortBy;
  sortDirection: AssetListSortDirection;
}

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const message = useMessage();

const loading = ref(false);
const deletingId = ref<string | null>(null);
const loadErrorMessage = ref('');
const invalidQueryFallback = ref(false);
const restoredFromQuery = ref(false);
const isFirstRouteSync = ref(true);

const assets = ref<AssetListItemResponse[]>([]);
const totalFromServer = ref(0);

const page = ref(DEFAULT_PAGE);
const pageSize = ref(DEFAULT_PAGE_SIZE);
const keyword = ref('');
const keywordDraft = ref('');
const contentTypeFilter = ref<ContentTypeFilterValue>('all');
const sortBy = ref<AssetListSortBy>(DEFAULT_SORT_BY);
const sortDirection = ref<AssetListSortDirection>(DEFAULT_SORT_DIRECTION);

const isSyncingStateFromRoute = ref(false);

const hasLoadError = computed(() => loadErrorMessage.value.length > 0);
const isEmpty = computed(() => !loading.value && !hasLoadError.value && assets.value.length === 0);

const hasNonDefaultQueryState = computed(() => {
  return page.value !== DEFAULT_PAGE
    || pageSize.value !== DEFAULT_PAGE_SIZE
    || keyword.value.length > 0
    || contentTypeFilter.value !== 'all'
    || sortBy.value !== DEFAULT_SORT_BY
    || sortDirection.value !== DEFAULT_SORT_DIRECTION;
});

const showQueryStateHint = computed(() => hasNonDefaultQueryState.value);

const sortOptionValue = computed<SortOptionValue>({
  get() {
    return `${sortBy.value}:${sortDirection.value}` as SortOptionValue;
  },
  set(value) {
    const [nextSortBy, nextSortDirection] = value.split(':') as [AssetListSortBy, AssetListSortDirection];
    sortBy.value = nextSortBy;
    sortDirection.value = nextSortDirection;
  },
});

const contentTypeOptions = computed<SelectOption[]>(() => [
  { label: t('asset.list.filterTypeAll'), value: 'all' },
  { label: t('asset.list.filterTypeImage'), value: 'image' },
  { label: 'image/png', value: 'image/png' },
  { label: 'image/jpeg', value: 'image/jpeg' },
  { label: 'image/webp', value: 'image/webp' },
  { label: 'image/gif', value: 'image/gif' },
]);

const sortOptions = computed<SelectOption[]>(() => [
  { label: t('asset.list.sortCreatedAtDesc'), value: 'createdAt:desc' },
  { label: t('asset.list.sortCreatedAtAsc'), value: 'createdAt:asc' },
  { label: t('asset.list.sortSizeDesc'), value: 'size:desc' },
  { label: t('asset.list.sortSizeAsc'), value: 'size:asc' },
]);

function getQueryValue(query: LocationQuery, key: string): string | undefined {
  const value = query[key];

  if (Array.isArray(value)) {
    const firstValue = value[0];
    return typeof firstValue === 'string' ? firstValue : undefined;
  }

  if (typeof value === 'string') {
    return value;
  }

  return undefined;
}

function parsePositiveInt(rawValue: string | undefined, fallback: number): { value: number; invalid: boolean } {
  if (!rawValue) {
    return { value: fallback, invalid: false };
  }

  const parsed = Number.parseInt(rawValue, 10);
  if (!Number.isInteger(parsed) || parsed <= 0) {
    return { value: fallback, invalid: true };
  }

  return { value: parsed, invalid: false };
}

function parseContentType(rawValue: string | undefined): { value: ContentTypeFilterValue; invalid: boolean } {
  if (!rawValue) {
    return { value: 'all', invalid: false };
  }

  const allowedValues: ContentTypeFilterValue[] = ['all', 'image', 'image/png', 'image/jpeg', 'image/webp', 'image/gif'];

  if (rawValue === 'image/*') {
    return { value: 'image', invalid: false };
  }

  if (allowedValues.includes(rawValue as ContentTypeFilterValue)) {
    return { value: rawValue as ContentTypeFilterValue, invalid: false };
  }

  return { value: 'all', invalid: true };
}

function parseSortBy(rawValue: string | undefined): { value: AssetListSortBy; invalid: boolean } {
  if (!rawValue) {
    return { value: DEFAULT_SORT_BY, invalid: false };
  }

  if (rawValue === 'createdAtUtc') {
    // 兼容历史 URL 参数，统一映射到 createdAt。
    return { value: 'createdAt', invalid: false };
  }

  if (rawValue === 'createdAt' || rawValue === 'size') {
    return { value: rawValue, invalid: false };
  }

  return { value: DEFAULT_SORT_BY, invalid: true };
}

function parseSortDirection(rawValue: string | undefined): { value: AssetListSortDirection; invalid: boolean } {
  if (!rawValue) {
    return { value: DEFAULT_SORT_DIRECTION, invalid: false };
  }

  if (rawValue === 'asc' || rawValue === 'desc') {
    return { value: rawValue, invalid: false };
  }

  return { value: DEFAULT_SORT_DIRECTION, invalid: true };
}

function parseRouteState(query: LocationQuery): { state: AssetListRouteState; invalid: boolean; restored: boolean } {
  const parsedPage = parsePositiveInt(getQueryValue(query, 'page'), DEFAULT_PAGE);
  const parsedPageSize = parsePositiveInt(getQueryValue(query, 'pageSize'), DEFAULT_PAGE_SIZE);
  const parsedContentType = parseContentType(getQueryValue(query, 'contentType'));
  const parsedSortBy = parseSortBy(getQueryValue(query, 'sortBy'));
  const parsedSortDirection = parseSortDirection(getQueryValue(query, 'sortDirection'));

  const keywordFromQuery = getQueryValue(query, 'keyword')?.trim() ?? '';
  const hasQuery = ASSET_QUERY_KEYS.some((key) => query[key] !== undefined);

  return {
    state: {
      page: parsedPage.value,
      pageSize: parsedPageSize.value,
      keyword: keywordFromQuery,
      contentType: parsedContentType.value,
      sortBy: parsedSortBy.value,
      sortDirection: parsedSortDirection.value,
    },
    invalid: parsedPage.invalid || parsedPageSize.invalid || parsedContentType.invalid || parsedSortBy.invalid || parsedSortDirection.invalid,
    restored: hasQuery,
  };
}

function applyRouteState(state: AssetListRouteState): void {
  page.value = state.page;
  pageSize.value = state.pageSize;
  keyword.value = state.keyword;
  keywordDraft.value = state.keyword;
  contentTypeFilter.value = state.contentType;
  sortBy.value = state.sortBy;
  sortDirection.value = state.sortDirection;
}

function normalizeContentTypeForQuery(value: ContentTypeFilterValue): string | undefined {
  if (value === 'all') {
    return undefined;
  }

  if (value === 'image') {
    return 'image/*';
  }

  return value;
}

function buildRouteQueryFromState(state: AssetListRouteState): LocationQueryRaw {
  return {
    page: String(state.page),
    pageSize: String(state.pageSize),
    sortBy: state.sortBy,
    sortDirection: state.sortDirection,
    keyword: state.keyword || undefined,
    contentType: normalizeContentTypeForQuery(state.contentType),
  };
}

function normalizeQueryForCompare(query: LocationQuery | LocationQueryRaw): Record<string, string> {
  const normalized: Record<string, string> = {};

  Object.entries(query).forEach(([key, rawValue]) => {
    if (rawValue === undefined || rawValue === null) {
      return;
    }

    if (Array.isArray(rawValue)) {
      if (rawValue.length > 0) {
        normalized[key] = String(rawValue[0]);
      }

      return;
    }

    normalized[key] = String(rawValue);
  });

  return normalized;
}

function areQueriesEqual(left: LocationQuery | LocationQueryRaw, right: LocationQuery | LocationQueryRaw): boolean {
  const leftNormalized = normalizeQueryForCompare(left);
  const rightNormalized = normalizeQueryForCompare(right);

  const leftKeys = Object.keys(leftNormalized).sort();
  const rightKeys = Object.keys(rightNormalized).sort();

  if (leftKeys.length !== rightKeys.length) {
    return false;
  }

  return leftKeys.every((key, index) => key === rightKeys[index] && leftNormalized[key] === rightNormalized[key]);
}

async function syncRouteQuery(): Promise<void> {
  const nextState: AssetListRouteState = {
    page: page.value,
    pageSize: pageSize.value,
    keyword: keyword.value,
    contentType: contentTypeFilter.value,
    sortBy: sortBy.value,
    sortDirection: sortDirection.value,
  };

  const nextQuery = buildRouteQueryFromState(nextState);
  if (areQueriesEqual(route.query, nextQuery)) {
    return;
  }

  await router.replace({
    path: '/assets',
    query: nextQuery,
  });
}

const columns = computed<DataTableColumns<AssetListItemResponse>>(() => [
  {
    title: t('asset.list.columns.fileName'),
    key: 'originalFileName',
    ellipsis: {
      tooltip: true,
    },
    render: (row) => row.originalFileName || '-',
  },
  {
    title: t('asset.list.columns.contentType'),
    key: 'contentType',
    width: 160,
    render: (row) => row.contentType || '-',
  },
  {
    title: t('asset.list.columns.size'),
    key: 'size',
    width: 120,
    render: (row) => formatFileSize(row.size),
  },
  {
    title: t('asset.list.columns.dimension'),
    key: 'dimension',
    width: 120,
    render: (row) => `${row.width ?? '-'} x ${row.height ?? '-'}`,
  },
  {
    title: t('asset.list.columns.status'),
    key: 'status',
    width: 120,
    render: (row) => h(AssetStatusTag, { status: row.status }),
  },
  {
    title: t('asset.list.columns.createdAt'),
    key: 'createdAtUtc',
    width: 180,
    render: (row) => formatDateTime(row.createdAtUtc),
  },
  {
    title: t('asset.list.columns.actions'),
    key: 'actions',
    width: 170,
    render: (row) =>
      h(
        NSpace,
        { size: 8 },
        {
          default: () => [
            h(
              NButton,
              {
                size: 'small',
                quaternary: true,
                onClick: () =>
                  void router.push({
                    path: `/assets/${row.id}`,
                    query: route.query,
                  }),
              },
              { default: () => t('common.view') },
            ),
            h(
              NPopconfirm,
              {
                negativeText: t('common.cancel'),
                positiveText: t('common.delete'),
                onPositiveClick: () => handleDelete(row.id),
              },
              {
                trigger: () =>
                  h(
                    NButton,
                    {
                      size: 'small',
                      quaternary: true,
                      type: 'error',
                      loading: deletingId.value === row.id,
                    },
                    { default: () => t('common.delete') },
                  ),
                default: () => t('common.confirmDelete'),
              },
            ),
          ],
        },
      ),
  },
]);

function buildListAssetsInput(): ListAssetsInput {
  return {
    page: page.value,
    pageSize: pageSize.value,
    keyword: keyword.value || undefined,
    contentType: normalizeContentTypeForQuery(contentTypeFilter.value),
    sortBy: sortBy.value,
    sortDirection: sortDirection.value,
  };
}

async function fetchAssets(): Promise<void> {
  loading.value = true;
  loadErrorMessage.value = '';

  try {
    const paged = await listAssets(buildListAssetsInput());

    assets.value = paged.items;
    totalFromServer.value = paged.total;
    page.value = paged.page;
    pageSize.value = paged.pageSize;
  } catch (error) {
    loadErrorMessage.value = extractApiErrorMessage(error);
    message.error(`${t('asset.list.loadFailed')}: ${loadErrorMessage.value}`);
  } finally {
    loading.value = false;
  }
}

async function handleDelete(id: string): Promise<void> {
  deletingId.value = id;

  try {
    await deleteAssetById(id);
    message.success(t('common.deleteSuccess'));

    if (assets.value.length === 1 && page.value > 1) {
      page.value -= 1;
    }

    await fetchAssets();
  } catch (error) {
    message.error(`${t('common.deleteFailed')}: ${extractApiErrorMessage(error)}`);
  } finally {
    deletingId.value = null;
  }
}

function handlePageChange(nextPage: number): void {
  page.value = nextPage;
}

function handlePageSizeChange(nextPageSize: number): void {
  pageSize.value = nextPageSize;
  page.value = DEFAULT_PAGE;
}

function applyKeywordSearch(): void {
  keyword.value = keywordDraft.value.trim();
  page.value = DEFAULT_PAGE;
}

function handleContentTypeChange(nextValue: string): void {
  contentTypeFilter.value = nextValue as ContentTypeFilterValue;
  page.value = DEFAULT_PAGE;
}

function handleSortOptionChange(nextValue: string): void {
  sortOptionValue.value = nextValue as SortOptionValue;
  page.value = DEFAULT_PAGE;
}

function resetFilters(): void {
  keywordDraft.value = '';
  keyword.value = '';
  contentTypeFilter.value = 'all';
  sortBy.value = DEFAULT_SORT_BY;
  sortDirection.value = DEFAULT_SORT_DIRECTION;
  page.value = DEFAULT_PAGE;
}

async function handleRefresh(): Promise<void> {
  await fetchAssets();
}

function goToUpload(): void {
  void router.push({
    path: '/assets/upload',
    query: route.query,
  });
}

watch(
  () => route.query,
  (query) => {
    const parsed = parseRouteState(query);

    isSyncingStateFromRoute.value = true;
    applyRouteState(parsed.state);
    invalidQueryFallback.value = parsed.invalid;
    restoredFromQuery.value = isFirstRouteSync.value && parsed.restored;
    isFirstRouteSync.value = false;
    isSyncingStateFromRoute.value = false;

    if (parsed.invalid) {
      message.warning(t('asset.list.invalidQueryFallback'));
    }

    void fetchAssets();
  },
  { immediate: true },
);

watch(
  [page, pageSize, keyword, contentTypeFilter, sortBy, sortDirection],
  () => {
    if (isSyncingStateFromRoute.value) {
      return;
    }

    void syncRouteQuery();
  },
);
</script>

<template>
  <div>
    <page-header :title="t('asset.list.title')" :description="t('asset.list.description')">
      <template #actions>
        <n-button type="primary" @click="goToUpload">{{ t('asset.list.upload') }}</n-button>
      </template>
    </page-header>

    <n-card class="section-card">
      <div class="toolbar">
        <n-space wrap>
          <n-input
            v-model:value="keywordDraft"
            clearable
            :placeholder="t('asset.list.searchPlaceholder')"
            style="width: 260px"
            @keyup.enter="applyKeywordSearch"
          />

          <n-select
            :value="contentTypeFilter"
            :placeholder="t('asset.list.filterType')"
            :options="contentTypeOptions"
            style="width: 180px"
            @update:value="handleContentTypeChange"
          />

          <n-select
            :value="sortOptionValue"
            :placeholder="t('asset.list.sortBy')"
            :options="sortOptions"
            style="width: 210px"
            @update:value="handleSortOptionChange"
          />

          <n-button :loading="loading" type="primary" ghost @click="applyKeywordSearch">
            {{ t('asset.list.searchAction') }}
          </n-button>

          <n-button :loading="loading" @click="handleRefresh">{{ t('asset.list.refresh') }}</n-button>
          <n-button :disabled="loading" @click="resetFilters">{{ t('asset.list.resetFilters') }}</n-button>
        </n-space>
      </div>

      <n-space vertical :size="8" style="margin-bottom: 12px">
        <n-alert v-if="showQueryStateHint" type="info" :show-icon="false">
          {{ t('asset.list.queryStateHint') }}
        </n-alert>
        <n-alert v-if="restoredFromQuery" type="info" :show-icon="false">
          {{ t('asset.list.restoreState') }}
        </n-alert>
        <n-alert v-if="invalidQueryFallback" type="warning" :show-icon="false">
          {{ t('asset.list.invalidQueryFallback') }}
        </n-alert>
      </n-space>

      <n-result
        v-if="hasLoadError"
        status="error"
        :title="t('asset.list.loadFailed')"
        :description="loadErrorMessage || t('asset.list.loadFailedDescription')"
      >
        <template #footer>
          <n-button type="primary" @click="fetchAssets">{{ t('common.retry') }}</n-button>
        </template>
      </n-result>

      <n-empty
        v-else-if="isEmpty"
        :description="t('asset.list.emptyDescription')"
        :show-icon="true"
        style="padding: 48px 0"
      >
        <template #extra>
          <n-button type="primary" @click="goToUpload">{{ t('asset.list.upload') }}</n-button>
        </template>
      </n-empty>

      <n-data-table
        v-else
        :loading="loading"
        :columns="columns"
        :data="assets"
        :row-key="(row) => row.id"
        remote
      />

      <div class="pagination-wrapper">
        <div class="pagination-label">{{ t('asset.list.pagination') }}</div>
        <n-pagination
          :page="page"
          :page-size="pageSize"
          :item-count="totalFromServer"
          :page-sizes="PAGE_SIZE_OPTIONS"
          :page-slot="7"
          show-size-picker
          @update:page="handlePageChange"
          @update:page-size="handlePageSizeChange"
        />
      </div>
    </n-card>
  </div>
</template>

<style scoped>
.toolbar {
  margin-bottom: 16px;
}

.pagination-wrapper {
  margin-top: 16px;
  padding-top: 16px;
  border-top: 1px solid #e5e7eb;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
}

.pagination-label {
  font-size: 13px;
  color: #6b7280;
}

.section-card :deep(.n-card-header__main) {
  font-weight: 600;
}
</style>
