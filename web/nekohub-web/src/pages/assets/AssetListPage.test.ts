import { defineComponent, h, type VNodeChild } from 'vue';
import { flushPromises, mount } from '@vue/test-utils';
import { createI18n } from 'vue-i18n';
import { createMemoryHistory, createRouter } from 'vue-router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import AssetListPage from './AssetListPage.vue';
import zhCN from '../../locales/zh-CN';

const message = {
  error: vi.fn(),
  success: vi.fn(),
  warning: vi.fn(),
};

const dialog = {
  warning: vi.fn(),
};

const assetApiMocks = vi.hoisted(() => ({
  listAssets: vi.fn(),
  getUsageStats: vi.fn(),
  deleteAsset: vi.fn(),
  batchDeleteAssets: vi.fn(),
}));

const storageApiMocks = vi.hoisted(() => ({
  getStorageProviderOverview: vi.fn(),
}));

vi.mock('naive-ui', () => {
  const passthrough = (tag = 'div') => defineComponent({
    inheritAttrs: false,
    setup(_, { attrs, slots }) {
      return () => h(tag, attrs, slots.default?.());
    },
  });

  const NButton = defineComponent({
    props: {
      loading: Boolean,
      disabled: Boolean,
      text: Boolean,
      type: String,
      size: String,
      quaternary: Boolean,
      ghost: Boolean,
    },
    emits: ['click'],
    setup(props, { attrs, emit, slots }) {
      return () => h('button', {
        ...attrs,
        disabled: props.disabled,
        onClick: () => emit('click'),
      }, slots.default?.());
    },
  });

  const NAlert = defineComponent({
    inheritAttrs: false,
    setup(_, { attrs, slots }) {
      return () => h('div', attrs, [
        slots.header?.(),
        slots.default?.(),
      ]);
    },
  });

  const NInput = defineComponent({
    props: {
      value: String,
    },
    emits: ['update:value', 'keyup.enter'],
    setup(props, { emit }) {
      return () => h('input', {
        value: props.value,
        onInput: (event: Event) => emit('update:value', (event.target as HTMLInputElement).value),
        onKeyup: (event: KeyboardEvent) => {
          if (event.key === 'Enter') {
            emit('keyup.enter');
          }
        },
      });
    },
  });

  const NSelect = defineComponent({
    props: {
      value: [String, Number],
      options: {
        type: Array,
        default: () => [],
      },
    },
    emits: ['update:value'],
    setup(props, { emit }) {
      return () => h('select', {
        value: props.value,
        onChange: (event: Event) => emit('update:value', (event.target as HTMLSelectElement).value),
      }, (props.options as Array<{ label: string; value: string }>).map((option) => (
        h('option', { value: option.value }, option.label)
      )));
    },
  });

  const NPagination = defineComponent({
    props: {
      page: Number,
      pageSize: Number,
    },
    emits: ['update:page', 'update:page-size'],
    setup(props) {
      return () => h('div', `${props.page}-${props.pageSize}`);
    },
  });

  const NTag = defineComponent({
    inheritAttrs: false,
    setup(_, { attrs, slots }) {
      return () => h('span', attrs, slots.default?.());
    },
  });

  const NDataTable = defineComponent({
    props: {
      columns: {
        type: Array,
        default: () => [],
      },
      data: {
        type: Array,
        default: () => [],
      },
    },
    setup(props) {
      function normalizeChildren(content: unknown): VNodeChild[] {
        if (content === null || content === undefined) {
          return [];
        }

        return Array.isArray(content) ? content as VNodeChild[] : [content as VNodeChild];
      }

      return () => h('div', { class: 'table-stub' }, (props.data as Array<Record<string, unknown>>).flatMap((row, rowIndex) => (
        (props.columns as Array<Record<string, unknown>>)
          .filter((column) => column.type !== 'selection')
          .map((column, columnIndex) => {
            const render = column.render as ((row: Record<string, unknown>, index: number) => unknown) | undefined;
            const key = String(column.key ?? `${rowIndex}-${columnIndex}`);

            return h(
              'div',
              { class: 'table-cell', 'data-key': key },
              normalizeChildren(render ? render(row, rowIndex) : String(row[key] ?? '')),
            );
          })
      )));
    },
  });

  return {
    NAlert,
    NButton,
    NCard: passthrough(),
    NDataTable,
    NEmpty: passthrough(),
    NGrid: passthrough(),
    NGridItem: passthrough(),
    NInput,
    NPagination,
    NResult: passthrough(),
    NSelect,
    NSkeleton: passthrough('span'),
    NSpace: passthrough(),
    NTag,
    useDialog: () => dialog,
    useMessage: () => message,
  };
});

vi.mock('../../api/assets/assets.api', () => ({
  listAssets: assetApiMocks.listAssets,
  getUsageStats: assetApiMocks.getUsageStats,
  deleteAsset: assetApiMocks.deleteAsset,
  batchDeleteAssets: assetApiMocks.batchDeleteAssets,
}));

vi.mock('../../api/system/storage.api', () => ({
  getStorageProviderOverview: storageApiMocks.getStorageProviderOverview,
}));

vi.mock('../../composables/useAuthPermissions', () => ({
  useAuthPermissions: () => ({
    can: () => true,
  }),
}));

vi.mock('../../components/common/PageHeader.vue', () => ({
  default: defineComponent({
    props: {
      title: String,
      description: String,
    },
    setup(props, { slots }) {
      return () => h('section', [
        h('h1', props.title),
        h('p', props.description),
        slots.actions?.(),
      ]);
    },
  }),
}));

vi.mock('../../components/assets/AssetStatusTag.vue', () => ({
  default: defineComponent({
    props: {
      status: String,
    },
    setup(props) {
      return () => h('span', props.status);
    },
  }),
}));

vi.mock('../../components/assets/AssetVisibilityTag.vue', () => ({
  default: defineComponent({
    props: {
      isPublic: Boolean,
    },
    setup(props) {
      return () => h('span', props.isPublic ? 'public' : 'private');
    },
  }),
}));

function createTestI18n() {
  return createI18n({
    legacy: false,
    locale: 'zh-CN',
    messages: {
      'zh-CN': zhCN,
    },
  });
}

async function mountPage() {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/assets', component: AssetListPage },
      { path: '/assets/:id', component: { template: '<div />' } },
      { path: '/assets/upload', component: { template: '<div />' } },
    ],
  });

  await router.push('/assets');
  await router.isReady();

  return mount(AssetListPage, {
    global: {
      plugins: [router, createTestI18n()],
    },
  });
}

describe('AssetListPage', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.clearAllMocks();

    assetApiMocks.getUsageStats.mockResolvedValue({
      totalAssets: 1,
      totalBytes: 1024,
      totalDerivatives: 0,
      contentTypeBreakdown: [],
      mostActiveSkill: null,
    });
    storageApiMocks.getStorageProviderOverview.mockResolvedValue({
      runtime: {
        providerName: 'local',
        providerType: 'local',
      },
      profiles: [],
    });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('shows a processing indicator and stops polling once pending assets are ready', async () => {
    assetApiMocks.listAssets
      .mockResolvedValueOnce({
        items: [{
          id: 'asset-1',
          type: 'image',
          status: 'pending',
          isPublic: true,
          originalFileName: 'cat.png',
          contentType: 'image/png',
          size: 123,
          width: 100,
          height: 100,
          storageProvider: 'local',
          storageProviderProfileId: null,
          publicUrl: null,
          createdAtUtc: '2026-04-10T00:00:00Z',
          updatedAtUtc: '2026-04-10T00:00:00Z',
        }],
        page: 1,
        pageSize: 20,
        total: 1,
      })
      .mockResolvedValueOnce({
        items: [{
          id: 'asset-1',
          type: 'image',
          status: 'ready',
          isPublic: true,
          originalFileName: 'cat.png',
          contentType: 'image/png',
          size: 123,
          width: 100,
          height: 100,
          storageProvider: 'local',
          storageProviderProfileId: null,
          publicUrl: null,
          createdAtUtc: '2026-04-10T00:00:00Z',
          updatedAtUtc: '2026-04-10T00:00:05Z',
        }],
        page: 1,
        pageSize: 20,
        total: 1,
      })
      .mockResolvedValue({
        items: [],
        page: 1,
        pageSize: 20,
        total: 0,
      });

    const wrapper = await mountPage();
    await flushPromises();

    expect(wrapper.text()).toContain('后台处理中');
    expect(wrapper.text()).toContain('当前有 1 个资产正在后台处理中');
    expect(assetApiMocks.listAssets).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(5_000);
    await flushPromises();

    expect(assetApiMocks.listAssets).toHaveBeenCalledTimes(2);

    await vi.advanceTimersByTimeAsync(10_000);
    await flushPromises();

    expect(assetApiMocks.listAssets).toHaveBeenCalledTimes(2);
  });
});
