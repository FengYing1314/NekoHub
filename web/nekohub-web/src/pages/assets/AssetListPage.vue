<script setup lang="ts">
import { computed, h, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import type { DataTableColumns, DataTableRowKey, SelectOption } from 'naive-ui';
import {
  NAlert,
  NButton,
  NCard,
  NDataTable,
  NEmpty,
  NGrid,
  NGridItem,
  NInput,
  NPagination,
  NResult,
  NSelect,
  NSkeleton,
  NSpace,
  NTag,
  useDialog,
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
import AssetVisibilityTag from '../../components/assets/AssetVisibilityTag.vue';
import { batchDeleteAssets, deleteAsset, getUsageStats, listAssets } from '../../api/assets/assets.api';
import { extractApiErrorMessage } from '../../api/client/error';
import { getStorageProviderOverview } from '../../api/system/storage.api';
import type {
  DeleteAssetInput,
  AssetListItemResponse,
  AssetListOrderBy,
  AssetListOrderDirection,
  AssetStatus,
  AssetUsageStatsResponse,
  ListAssetsInput,
} from '../../types/assets';
import { isAssetPending } from '../../types/assets';
import type { StorageProviderOverviewResponse } from '../../types/storage';
import { formatDateTime, formatFileSize } from '../../utils/format';
import { useAuthPermissions } from '../../composables/useAuthPermissions';
import { PERMISSIONS } from '../../constants/permissions';

const DEFAULT_PAGE = 1;
const DEFAULT_PAGE_SIZE = 20;
const DEFAULT_ORDER_BY: AssetListOrderBy = 'createdAt';
const DEFAULT_ORDER_DIRECTION: AssetListOrderDirection = 'desc';
const PAGE_SIZE_OPTIONS = [10, 20, 50];
const LIST_TABLE_SCROLL_X = 1120;
const PROCESSING_POLL_INTERVAL_MS = 5_000;
const ASSET_QUERY_KEYS = [
  'page',
  'pageSize',
  'query',
  'keyword',
  'contentType',
  'status',
  'orderBy',
  'orderDirection',
  'sortBy',
  'sortDirection',
] as const;

type SortOptionValue = 'createdAt:desc' | 'createdAt:asc' | 'size:desc' | 'size:asc';
type ContentTypeFilterValue = 'all' | 'image' | 'image/png' | 'image/jpeg' | 'image/webp' | 'image/gif';
type StatusFilterValue = 'all' | AssetStatus;

interface AssetListRouteState {
  page: number;
  pageSize: number;
  query: string;
  contentType: ContentTypeFilterValue;
  status: StatusFilterValue;
  orderBy: AssetListOrderBy;
  orderDirection: AssetListOrderDirection;
}

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const dialog = useDialog();
const message = useMessage();
const { can } = useAuthPermissions();

const loading = ref(false);
const backgroundRefreshing = ref(false);
const deletingId = ref<string | null>(null);
const batchDeleting = ref(false);
const loadErrorMessage = ref('');
const statsLoading = ref(false);
const statsLoadErrorMessage = ref('');
const invalidQueryFallback = ref(false);
const restoredFromQuery = ref(false);
const isFirstRouteSync = ref(true);

const assets = ref<AssetListItemResponse[]>([]);
const usageStats = ref<AssetUsageStatsResponse | null>(null);
const storageOverview = ref<StorageProviderOverviewResponse | null>(null);
const totalFromServer = ref(0);
const checkedRowKeys = ref<DataTableRowKey[]>([]);

const page = ref(DEFAULT_PAGE);
const pageSize = ref(DEFAULT_PAGE_SIZE);
const queryText = ref('');
const queryDraft = ref('');
const contentTypeFilter = ref<ContentTypeFilterValue>('all');
const statusFilter = ref<StatusFilterValue>('all');
const orderBy = ref<AssetListOrderBy>(DEFAULT_ORDER_BY);
const orderDirection = ref<AssetListOrderDirection>(DEFAULT_ORDER_DIRECTION);

const isSyncingStateFromRoute = ref(false);

const hasLoadError = computed(() => loadErrorMessage.value.length > 0);
const isEmpty = computed(() => !loading.value && !hasLoadError.value && assets.value.length === 0);
const hasStatsError = computed(() => statsLoadErrorMessage.value.length > 0);
const selectedCount = computed(() => checkedRowKeys.value.length);
const hasSelection = computed(() => selectedCount.value > 0);
const canCreateAssets = computed(() => can(PERMISSIONS.assetsCreate));
const canDeleteAssets = computed(() => can(PERMISSIONS.assetsDelete));
const selectedAssetIds = computed(() => checkedRowKeys.value.map((key) => String(key)));
const topContentType = computed(() => usageStats.value?.contentTypeBreakdown[0] ?? null);
const processingAssetCount = computed(() => assets.value.filter(isAssetProcessing).length);
const hasProcessingAssets = computed(() => processingAssetCount.value > 0);

let assetPollingTimerId: number | null = null;

const usageSummaryItems = computed(() => {
  const stats = usageStats.value;
  if (!stats) {
    return [];
  }

  return [
    {
      key: 'totalAssets',
      label: t('asset.list.stats.totalAssets'),
      value: formatCount(stats.totalAssets),
      meta: '',
    },
    {
      key: 'totalBytes',
      label: t('asset.list.stats.totalBytes'),
      value: formatFileSize(stats.totalBytes),
      meta: '',
    },
    {
      key: 'totalDerivatives',
      label: t('asset.list.stats.totalDerivatives'),
      value: formatCount(stats.totalDerivatives),
      meta: '',
    },
    {
      key: 'topContentType',
      label: t('asset.list.stats.topContentType'),
      value: topContentType.value?.contentType ?? t('common.noData'),
      meta: topContentType.value
        ? t('asset.list.stats.topContentTypeMeta', {
          count: formatCount(topContentType.value.count),
          size: formatFileSize(topContentType.value.totalBytes),
        })
        : t('asset.list.stats.topContentTypeEmpty'),
    },
    {
      key: 'mostActiveSkill',
      label: t('asset.list.stats.mostActiveSkill'),
      value: stats.mostActiveSkill?.skillName ?? t('common.noData'),
      meta: stats.mostActiveSkill
        ? t('asset.list.stats.activeSkillMeta', {
          count: formatCount(stats.mostActiveSkill.runCount),
        })
        : t('asset.list.stats.activeSkillEmpty'),
    },
  ];
});

const hasNonDefaultQueryState = computed(() => {
  return page.value !== DEFAULT_PAGE
    || pageSize.value !== DEFAULT_PAGE_SIZE
    || queryText.value.length > 0
    || contentTypeFilter.value !== 'all'
    || statusFilter.value !== 'all'
    || orderBy.value !== DEFAULT_ORDER_BY
    || orderDirection.value !== DEFAULT_ORDER_DIRECTION;
});

const showQueryStateHint = computed(() => hasNonDefaultQueryState.value);

const sortOptionValue = computed<SortOptionValue>({
  get() {
    return `${orderBy.value}:${orderDirection.value}` as SortOptionValue;
  },
  set(value) {
    const [nextOrderBy, nextOrderDirection] = value.split(':') as [AssetListOrderBy, AssetListOrderDirection];
    orderBy.value = nextOrderBy;
    orderDirection.value = nextOrderDirection;
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
const statusOptions = computed<SelectOption[]>(() => [
  { label: t('asset.list.filterStatusAll'), value: 'all' },
  { label: t('asset.list.filterStatusPending'), value: 'pending' },
  { label: t('asset.list.filterStatusReady'), value: 'ready' },
  { label: t('asset.list.filterStatusFailed'), value: 'failed' },
  { label: t('asset.list.filterStatusDeleted'), value: 'deleted' },
]);

const countFormatter = new Intl.NumberFormat('zh-CN');

function formatCount(value: number): string {
  return countFormatter.format(value);
}

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

function parseStatus(rawValue: string | undefined): { value: StatusFilterValue; invalid: boolean } {
  if (!rawValue) {
    return { value: 'all', invalid: false };
  }

  const normalized = rawValue.trim().toLowerCase();
  if (normalized === 'all') {
    return { value: 'all', invalid: false };
  }

  if (normalized === 'processing') {
    return { value: 'pending', invalid: false };
  }

  if (normalized === 'pending' || normalized === 'ready' || normalized === 'deleted' || normalized === 'failed') {
    return { value: normalized as AssetStatus, invalid: false };
  }

  return { value: 'all', invalid: true };
}

function parseOrderBy(rawValue: string | undefined): { value: AssetListOrderBy; invalid: boolean } {
  if (!rawValue) {
    return { value: DEFAULT_ORDER_BY, invalid: false };
  }

  if (rawValue === 'createdAtUtc') {
    // 兼容历史 URL 参数，统一映射到 createdAt。
    return { value: 'createdAt', invalid: false };
  }

  if (rawValue === 'createdAt' || rawValue === 'size') {
    return { value: rawValue, invalid: false };
  }

  return { value: DEFAULT_ORDER_BY, invalid: true };
}

function parseOrderDirection(rawValue: string | undefined): { value: AssetListOrderDirection; invalid: boolean } {
  if (!rawValue) {
    return { value: DEFAULT_ORDER_DIRECTION, invalid: false };
  }

  if (rawValue === 'asc' || rawValue === 'desc') {
    return { value: rawValue, invalid: false };
  }

  return { value: DEFAULT_ORDER_DIRECTION, invalid: true };
}

function parseRouteState(query: LocationQuery): { state: AssetListRouteState; invalid: boolean; restored: boolean } {
  // 这里同时兼容 query/keyword、orderBy/sortBy 等历史参数，保证分享链接和旧地址仍可恢复列表状态。
  const parsedPage = parsePositiveInt(getQueryValue(query, 'page'), DEFAULT_PAGE);
  const parsedPageSize = parsePositiveInt(getQueryValue(query, 'pageSize'), DEFAULT_PAGE_SIZE);
  const parsedContentType = parseContentType(getQueryValue(query, 'contentType'));
  const parsedStatus = parseStatus(getQueryValue(query, 'status'));
  const parsedOrderBy = parseOrderBy(getQueryValue(query, 'orderBy') ?? getQueryValue(query, 'sortBy'));
  const parsedOrderDirection = parseOrderDirection(
    getQueryValue(query, 'orderDirection') ?? getQueryValue(query, 'sortDirection'),
  );

  const queryFromRoute = getQueryValue(query, 'query')?.trim()
    ?? getQueryValue(query, 'keyword')?.trim()
    ?? '';
  const hasQuery = ASSET_QUERY_KEYS.some((key) => query[key] !== undefined);

  return {
    state: {
      page: parsedPage.value,
      pageSize: parsedPageSize.value,
      query: queryFromRoute,
      contentType: parsedContentType.value,
      status: parsedStatus.value,
      orderBy: parsedOrderBy.value,
      orderDirection: parsedOrderDirection.value,
    },
    invalid: parsedPage.invalid
      || parsedPageSize.invalid
      || parsedContentType.invalid
      || parsedStatus.invalid
      || parsedOrderBy.invalid
      || parsedOrderDirection.invalid,
    restored: hasQuery,
  };
}

function applyRouteState(state: AssetListRouteState): void {
  page.value = state.page;
  pageSize.value = state.pageSize;
  queryText.value = state.query;
  queryDraft.value = state.query;
  contentTypeFilter.value = state.contentType;
  statusFilter.value = state.status;
  orderBy.value = state.orderBy;
  orderDirection.value = state.orderDirection;
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
    orderBy: state.orderBy,
    orderDirection: state.orderDirection,
    query: state.query || undefined,
    contentType: normalizeContentTypeForQuery(state.contentType),
    status: state.status === 'all' ? undefined : state.status,
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
    query: queryText.value,
    contentType: contentTypeFilter.value,
    status: statusFilter.value,
    orderBy: orderBy.value,
    orderDirection: orderDirection.value,
  };

  const nextQuery = buildRouteQueryFromState(nextState);
  if (areQueriesEqual(route.query, nextQuery)) {
    return;
  }

  // 列表筛选变更只替换当前历史项，避免每次敲关键字或翻页都污染浏览器回退栈。
  await router.replace({
    path: '/assets',
    query: nextQuery,
  });
}

function isAssetProcessing(asset: Pick<AssetListItemResponse, 'status'>): boolean {
  return isAssetPending(asset.status);
}

function renderProcessingState(): ReturnType<typeof h> {
  return h(
    NTag,
    {
      size: 'small',
      type: 'warning',
      bordered: false,
      round: true,
    },
    {
      default: () => t('asset.list.processing.inline'),
    },
  );
}

const columns = computed<DataTableColumns<AssetListItemResponse>>(() => [
  {
    type: 'selection',
    width: 48,
  },
  {
    title: t('asset.list.columns.fileName'),
    key: 'originalFileName',
    ellipsis: {
      tooltip: true,
    },
    render: (row) => h(
      NSpace,
      {
        vertical: true,
        size: 4,
      },
      {
        default: () => [
          h('span', row.originalFileName || '-'),
          isAssetProcessing(row) ? renderProcessingState() : null,
        ],
      },
    ),
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
    title: t('asset.list.columns.visibility'),
    key: 'isPublic',
    width: 110,
    render: (row) => h(AssetVisibilityTag, { isPublic: row.isPublic }),
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
              NButton,
              {
                size: 'small',
                quaternary: true,
                type: 'error',
                loading: deletingId.value === row.id,
                disabled: batchDeleting.value || !canDeleteAssets.value,
                onClick: () => handleDeleteAction(row),
              },
              { default: () => t('common.delete') },
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
    query: queryText.value || undefined,
    contentType: normalizeContentTypeForQuery(contentTypeFilter.value),
    status: statusFilter.value === 'all' ? undefined : statusFilter.value,
    orderBy: orderBy.value,
    orderDirection: orderDirection.value,
  };
}

async function fetchAssets(options: { silent?: boolean } = {}): Promise<void> {
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
    const paged = await listAssets(buildListAssetsInput());

    assets.value = paged.items;
    totalFromServer.value = paged.total;
    page.value = paged.page;
    pageSize.value = paged.pageSize;
    loadErrorMessage.value = '';
    syncSelectionWithCurrentPage();
  } catch (error) {
    if (!silent) {
      loadErrorMessage.value = extractApiErrorMessage(error);
      message.error(`${t('asset.list.loadFailed')}: ${loadErrorMessage.value}`);
    }
  } finally {
    if (silent) {
      backgroundRefreshing.value = false;
    } else {
      loading.value = false;
    }
  }
}

async function fetchUsageStats(): Promise<void> {
  statsLoading.value = true;
  statsLoadErrorMessage.value = '';

  try {
    usageStats.value = await getUsageStats();
  } catch (error) {
    statsLoadErrorMessage.value = extractApiErrorMessage(error);
  } finally {
    statsLoading.value = false;
  }
}

async function fetchStorageOverview(): Promise<void> {
  try {
    storageOverview.value = await getStorageProviderOverview();
  } catch {
    storageOverview.value = null;
  }
}

function syncSelectionWithCurrentPage(): void {
  const visibleIds = new Set(assets.value.map((asset) => asset.id));
  checkedRowKeys.value = checkedRowKeys.value.filter((key) => visibleIds.has(String(key)));
}

function clearSelection(): void {
  checkedRowKeys.value = [];
}

function getPageAfterDelete(deletedCount: number): number {
  const nextTotal = Math.max(totalFromServer.value - deletedCount, 0);
  const maxPage = Math.max(DEFAULT_PAGE, Math.ceil(nextTotal / pageSize.value));

  return Math.min(page.value, maxPage);
}

async function refreshAfterDelete(deletedCount: number): Promise<void> {
  const nextPage = getPageAfterDelete(deletedCount);

  if (page.value !== nextPage) {
    page.value = nextPage;
  }

  await Promise.all([fetchAssets(), fetchUsageStats()]);
}

function isGitHubAsset(asset: Pick<AssetListItemResponse, 'storageProvider' | 'storageProviderProfileId'>): boolean {
  const overview = storageOverview.value;
  if (!overview) {
    return false;
  }

  if (asset.storageProviderProfileId) {
    const matchedProfile = overview.profiles.find((profile) => profile.id === asset.storageProviderProfileId);
    return matchedProfile?.providerType === 'github-repo';
  }

  return asset.storageProvider === overview.runtime.providerName
    && overview.runtime.providerType === 'github-repo';
}

async function handleDelete(id: string, input?: DeleteAssetInput): Promise<void> {
  deletingId.value = id;

  try {
    await deleteAsset(id, input);
    message.success(t('common.deleteSuccess'));
    await refreshAfterDelete(1);
  } catch (error) {
    message.error(`${t('common.deleteFailed')}: ${extractApiErrorMessage(error)}`);
  } finally {
    deletingId.value = null;
  }
}

function handleDeleteAction(row: AssetListItemResponse): void {
  if (!canDeleteAssets.value) {
    message.warning(t('auth.permissionDenied'));
    return;
  }

  if (isGitHubAsset(row)) {
    const commitMessageState = ref('');

    dialog.warning({
      title: t('asset.list.githubDeleteDialog.title'),
      positiveText: t('common.delete'),
      negativeText: t('common.cancel'),
      content: () => h(
        NSpace,
        { vertical: true, size: 12 },
        {
          default: () => [
            h('div', t('asset.list.githubDeleteDialog.description', {
              name: row.originalFileName || row.id,
            })),
            h(
              NInput,
              {
                value: commitMessageState.value,
                type: 'textarea',
                autosize: {
                  minRows: 2,
                  maxRows: 4,
                },
                placeholder: t('asset.list.githubDeleteDialog.commitMessagePlaceholder'),
                'onUpdate:value': (value: string) => {
                  commitMessageState.value = value;
                },
              },
            ),
            h('div', { class: 'dialog-hint-text' }, t('asset.list.githubDeleteDialog.commitMessageHint')),
          ],
        },
      ),
      onPositiveClick: () => handleDelete(row.id, {
        commitMessage: commitMessageState.value,
      }),
    });

    return;
  }

  dialog.warning({
    title: t('common.delete'),
    content: t('common.confirmDelete'),
    positiveText: t('common.delete'),
    negativeText: t('common.cancel'),
    onPositiveClick: () => handleDelete(row.id),
  });
}

function handleCheckedRowKeysChange(nextKeys: DataTableRowKey[]): void {
  checkedRowKeys.value = nextKeys;
}

async function executeBatchDelete(assetIds: string[]): Promise<void> {
  if (assetIds.length === 0 || batchDeleting.value) {
    return;
  }

  batchDeleting.value = true;

  try {
    const result = await batchDeleteAssets(assetIds);
    clearSelection();

    if (result.notFoundIds.length > 0) {
      message.warning(
        t('asset.list.batch.deletePartialSuccess', {
          deletedCount: result.deletedCount,
          notFoundCount: result.notFoundIds.length,
        }),
      );
    } else {
      message.success(
        t('asset.list.batch.deleteSuccess', {
          count: result.deletedCount,
        }),
      );
    }

    await refreshAfterDelete(result.deletedCount);
  } catch (error) {
    message.error(`${t('asset.list.batch.deleteFailed')}: ${extractApiErrorMessage(error)}`);
  } finally {
    batchDeleting.value = false;
  }
}

function handleBatchDelete(): void {
  if (!canDeleteAssets.value) {
    message.warning(t('auth.permissionDenied'));
    return;
  }

  const assetIds = [...selectedAssetIds.value];
  if (assetIds.length === 0 || batchDeleting.value) {
    return;
  }

  dialog.warning({
    title: t('asset.list.batch.deleteAction'),
    content: t('asset.list.batch.confirmDelete', { count: assetIds.length }),
    positiveText: t('asset.list.batch.deleteAction'),
    negativeText: t('common.cancel'),
    onPositiveClick: () => executeBatchDelete(assetIds),
  });
}

function handlePageChange(nextPage: number): void {
  page.value = nextPage;
}

function handlePageSizeChange(nextPageSize: number): void {
  pageSize.value = nextPageSize;
  page.value = DEFAULT_PAGE;
}

function applyQuerySearch(): void {
  queryText.value = queryDraft.value.trim();
  page.value = DEFAULT_PAGE;
}

function handleContentTypeChange(nextValue: string): void {
  contentTypeFilter.value = nextValue as ContentTypeFilterValue;
  page.value = DEFAULT_PAGE;
}

function handleStatusChange(nextValue: string): void {
  statusFilter.value = nextValue as StatusFilterValue;
  page.value = DEFAULT_PAGE;
}

function handleSortOptionChange(nextValue: string): void {
  sortOptionValue.value = nextValue as SortOptionValue;
  page.value = DEFAULT_PAGE;
}

function resetFilters(): void {
  queryDraft.value = '';
  queryText.value = '';
  contentTypeFilter.value = 'all';
  statusFilter.value = 'all';
  orderBy.value = DEFAULT_ORDER_BY;
  orderDirection.value = DEFAULT_ORDER_DIRECTION;
  page.value = DEFAULT_PAGE;
}

async function handleRefresh(): Promise<void> {
  await Promise.all([fetchAssets(), fetchUsageStats()]);
}

async function handleListReload(): Promise<void> {
  await fetchAssets();
}

function startAssetPolling(): void {
  if (assetPollingTimerId !== null) {
    return;
  }

  assetPollingTimerId = window.setInterval(() => {
    void fetchAssets({ silent: true });
  }, PROCESSING_POLL_INTERVAL_MS);
}

function stopAssetPolling(): void {
  if (assetPollingTimerId === null) {
    return;
  }

  window.clearInterval(assetPollingTimerId);
  assetPollingTimerId = null;
}

function goToUpload(): void {
  if (!canCreateAssets.value) {
    message.warning(t('auth.permissionDenied'));
    return;
  }

  void router.push({
    path: '/assets/upload',
    query: route.query,
  });
}

onMounted(() => {
  void fetchUsageStats();
  void fetchStorageOverview();
});

onBeforeUnmount(() => {
  stopAssetPolling();
});

watch(
  () => route.query,
  (query) => {
    const parsed = parseRouteState(query);

    // 路由到状态、状态到路由是双向同步的，这个哨兵用来避免 watch 之间互相触发成循环。
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
  [page, pageSize, queryText, contentTypeFilter, statusFilter, orderBy, orderDirection],
  () => {
    if (isSyncingStateFromRoute.value) {
      return;
    }

    void syncRouteQuery();
  },
);

watch(
  hasProcessingAssets,
  (nextValue) => {
    if (nextValue) {
      startAssetPolling();
      return;
    }

    stopAssetPolling();
  },
  { immediate: true },
);
</script>

<template>
  <div>
    <page-header :title="t('asset.list.title')" :description="t('asset.list.description')">
      <template #actions>
        <n-button v-if="canCreateAssets" type="primary" @click="goToUpload">{{ t('asset.list.upload') }}</n-button>
      </template>
    </page-header>

    <n-card class="section-card stats-card" :title="t('asset.list.stats.title')">
      <template #header-extra>
        <n-button text :loading="statsLoading" @click="fetchUsageStats">
          {{ t('common.refresh') }}
        </n-button>
      </template>

      <n-space vertical :size="12">
        <n-alert v-if="hasStatsError" type="warning" :show-icon="false">
          <div class="stats-alert">
            <span>{{ t('asset.list.stats.loadFailed') }}: {{ statsLoadErrorMessage }}</span>
            <n-button text size="small" :loading="statsLoading" @click="fetchUsageStats">
              {{ t('common.retry') }}
            </n-button>
          </div>
        </n-alert>

        <n-grid
          v-if="statsLoading && !usageStats"
          cols="1 s:2 m:3 xl:5"
          responsive="screen"
          :x-gap="12"
          :y-gap="12"
        >
          <n-grid-item v-for="index in 5" :key="index">
            <div class="stats-tile">
              <n-skeleton text style="width: 56px; margin-bottom: 10px" />
              <n-skeleton text :repeat="2" />
            </div>
          </n-grid-item>
        </n-grid>

        <n-grid
          v-else-if="usageStats"
          cols="1 s:2 m:3 xl:5"
          responsive="screen"
          :x-gap="12"
          :y-gap="12"
        >
          <n-grid-item v-for="item in usageSummaryItems" :key="item.key">
            <div class="stats-tile">
              <div class="stats-label">{{ item.label }}</div>
              <div class="stats-value">{{ item.value }}</div>
              <div v-if="item.meta" class="stats-meta">{{ item.meta }}</div>
            </div>
          </n-grid-item>
        </n-grid>
      </n-space>
    </n-card>

    <n-card class="section-card">
      <div class="toolbar">
        <n-space wrap class="toolbar-space">
          <n-input
            v-model:value="queryDraft"
            clearable
            :placeholder="t('asset.list.searchPlaceholder')"
            class="toolbar-control toolbar-control--search"
            @keyup.enter="applyQuerySearch"
          />

          <n-select
            :value="contentTypeFilter"
            :placeholder="t('asset.list.filterType')"
            :options="contentTypeOptions"
            class="toolbar-control"
            @update:value="handleContentTypeChange"
          />

          <n-select
            :value="statusFilter"
            :placeholder="t('asset.list.filterStatus')"
            :options="statusOptions"
            class="toolbar-control"
            @update:value="handleStatusChange"
          />

          <n-select
            :value="sortOptionValue"
            :placeholder="t('asset.list.sortBy')"
            :options="sortOptions"
            class="toolbar-control toolbar-control--sort"
            @update:value="handleSortOptionChange"
          />

          <n-button class="toolbar-button" :loading="loading" type="primary" ghost @click="applyQuerySearch">
            {{ t('asset.list.searchAction') }}
          </n-button>

          <n-button class="toolbar-button" :loading="loading" @click="handleRefresh">
            {{ t('asset.list.refresh') }}
          </n-button>
          <n-button class="toolbar-button" :disabled="loading" @click="resetFilters">
            {{ t('asset.list.resetFilters') }}
          </n-button>
        </n-space>
      </div>

      <n-space vertical :size="8" style="margin-bottom: 12px">
        <n-alert v-if="hasProcessingAssets" type="info" :show-icon="false" data-testid="asset-list-processing-banner">
          <div class="processing-alert">
            <span>{{ t('asset.list.processing.notice', { count: processingAssetCount }) }}</span>
            <n-space :size="8" align="center">
              <span class="processing-alert-meta">{{ t('asset.list.processing.autoRefresh') }}</span>
              <n-button text size="small" :loading="backgroundRefreshing" @click="handleRefresh">
                {{ t('common.refresh') }}
              </n-button>
            </n-space>
          </div>
        </n-alert>
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

      <div v-if="hasSelection && canDeleteAssets" class="batch-action-bar">
        <div class="batch-action-summary">
          {{ t('asset.list.batch.selectedCount', { count: selectedCount }) }}
        </div>
        <n-space :size="8">
          <n-button quaternary :disabled="batchDeleting" @click="clearSelection">
            {{ t('asset.list.batch.clearSelection') }}
          </n-button>
          <n-button type="error" :loading="batchDeleting" @click="handleBatchDelete">
            {{ t('asset.list.batch.deleteAction') }}
          </n-button>
        </n-space>
      </div>

      <n-result
        v-if="hasLoadError"
        status="error"
        :title="t('asset.list.loadFailed')"
        :description="loadErrorMessage || t('asset.list.loadFailedDescription')"
      >
        <template #footer>
          <n-button type="primary" @click="handleListReload">{{ t('common.retry') }}</n-button>
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

      <div v-else class="table-wrapper">
        <n-data-table
          :loading="loading"
          :columns="columns"
          :data="assets"
          :row-key="(row) => row.id"
          :checked-row-keys="checkedRowKeys"
          :scroll-x="LIST_TABLE_SCROLL_X"
          remote
          @update:checked-row-keys="handleCheckedRowKeysChange"
        />
      </div>

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
.stats-card {
  margin-bottom: 16px;
}

.stats-alert {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
}

.stats-tile {
  padding: 16px;
  border: 1px solid #e5e7eb;
  border-radius: 12px;
  background: linear-gradient(180deg, #ffffff 0%, #f8fafc 100%);
  min-height: 112px;
}

.stats-label {
  font-size: 12px;
  color: #6b7280;
  margin-bottom: 8px;
}

.stats-value {
  font-size: 22px;
  font-weight: 700;
  color: #111827;
  line-height: 1.2;
  word-break: break-word;
}

.stats-meta {
  margin-top: 8px;
  font-size: 12px;
  color: #6b7280;
  line-height: 1.5;
}

.toolbar {
  margin-bottom: 16px;
}

.processing-alert {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
}

.processing-alert-meta {
  font-size: 12px;
  color: #6b7280;
}

.toolbar-space {
  width: 100%;
}

.toolbar-control {
  width: 180px;
}

.toolbar-control--search {
  width: 260px;
}

.toolbar-control--sort {
  width: 210px;
}

.batch-action-bar {
  margin-bottom: 16px;
  padding: 12px 16px;
  border: 1px solid #f3d6a0;
  border-radius: 12px;
  background: #fff8eb;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
}

.batch-action-summary {
  font-size: 14px;
  font-weight: 600;
  color: #8a5a00;
}

:global(.dialog-hint-text) {
  font-size: 12px;
  color: #6b7280;
  line-height: 1.6;
}

.table-wrapper {
  overflow-x: auto;
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

@media (max-width: 768px) {
  .stats-tile {
    min-height: unset;
  }

  .stats-value {
    font-size: 20px;
  }

  .toolbar :deep(.n-space-item) {
    width: 100%;
  }

  .toolbar :deep(.n-space-item > *) {
    width: 100%;
  }

  .batch-action-bar {
    padding: 12px;
  }

  .pagination-wrapper {
    align-items: flex-start;
  }
}
</style>
