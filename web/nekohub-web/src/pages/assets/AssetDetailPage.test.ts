import { defineComponent, h, type VNodeChild } from 'vue';
import { flushPromises, mount } from '@vue/test-utils';
import { createI18n } from 'vue-i18n';
import { createMemoryHistory, createRouter } from 'vue-router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import AssetDetailPage from './AssetDetailPage.vue';
import zhCN from '../../locales/zh-CN';

const message = {
  error: vi.fn(),
  success: vi.fn(),
  warning: vi.fn(),
};

const assetApiMocks = vi.hoisted(() => ({
  deleteAsset: vi.fn(),
  getAsset: vi.fn(),
  getAssetContentBlob: vi.fn(),
  patchAsset: vi.fn(),
  runAssetWorkflow: vi.fn(),
}));

const storageApiMocks = vi.hoisted(() => ({
  getStorageProviderOverview: vi.fn(),
}));

const workflowApiMocks = vi.hoisted(() => ({
  listWorkflowProfiles: vi.fn(),
}));

vi.mock('naive-ui', () => {
  const passthrough = (tag = 'div') => defineComponent({
    inheritAttrs: false,
    setup(_, { attrs, slots }) {
      return () => h(tag, attrs, [
        slots.header?.(),
        slots.default?.(),
        slots['header-extra']?.(),
        slots.footer?.(),
        slots.trigger?.(),
      ]);
    },
  });

  const NButton = defineComponent({
    props: {
      loading: Boolean,
      disabled: Boolean,
      text: Boolean,
      type: String,
      size: String,
      ghost: Boolean,
      quaternary: Boolean,
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

  const NInput = defineComponent({
    props: {
      value: String,
    },
    emits: ['update:value'],
    setup(props, { emit }) {
      return () => h('input', {
        value: props.value,
        onInput: (event: Event) => emit('update:value', (event.target as HTMLInputElement).value),
      });
    },
  });

  const NSelect = defineComponent({
    props: {
      value: {
        type: [String, Number, null],
        default: null,
      },
      options: {
        type: Array,
        default: () => [],
      },
    },
    emits: ['update:value'],
    setup(props, { emit }) {
      return () => h(
        'select',
        {
          value: props.value === null ? '' : String(props.value),
          onChange: (event: Event) => {
            const nextValue = (event.target as HTMLSelectElement).value;
            emit('update:value', nextValue || null);
          },
        },
        (props.options as Array<Record<string, unknown>>).map((option) => h('option', {
          value: String(option.value ?? ''),
        }, String(option.label ?? option.value ?? ''))),
      );
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
        (props.columns as Array<Record<string, unknown>>).map((column, columnIndex) => {
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

  const NDescriptions = defineComponent({
    setup(_, { slots }) {
      return () => h('dl', slots.default?.());
    },
  });

  const NDescriptionsItem = defineComponent({
    props: {
      label: String,
    },
    setup(props, { slots }) {
      return () => h('div', [
        h('dt', props.label),
        h('dd', slots.default?.()),
      ]);
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

  return {
    NAlert,
    NButton,
    NCard: passthrough(),
    NDataTable,
    NDescriptions,
    NDescriptionsItem,
    NEmpty: passthrough(),
    NForm: passthrough('form'),
    NFormItem: passthrough(),
    NImage: defineComponent({
      props: {
        src: String,
      },
      setup(props) {
        return () => h('img', { src: props.src });
      },
    }),
    NInput,
    NPopconfirm: passthrough(),
    NResult: passthrough(),
    NSelect,
    NSpace: passthrough(),
    NTag,
    useMessage: () => message,
  };
});

vi.mock('../../api/assets/assets.api', () => ({
  deleteAsset: assetApiMocks.deleteAsset,
  getAsset: assetApiMocks.getAsset,
  getAssetContentBlob: assetApiMocks.getAssetContentBlob,
  patchAsset: assetApiMocks.patchAsset,
  runAssetWorkflow: assetApiMocks.runAssetWorkflow,
}));

vi.mock('../../api/system/storage.api', () => ({
  getStorageProviderOverview: storageApiMocks.getStorageProviderOverview,
}));

vi.mock('../../api/system/workflows.api', () => ({
  listWorkflowProfiles: workflowApiMocks.listWorkflowProfiles,
}));

vi.mock('../../composables/useIsMobile', () => ({
  useIsMobile: () => ({
    isMobile: { value: false },
  }),
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

vi.mock('../../components/assets/structured-results/StructuredResultRenderer.vue', () => ({
  default: defineComponent({
    props: {
      result: {
        type: Object,
        required: true,
      },
    },
    setup(props) {
      return () => h('pre', JSON.stringify(props.result));
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
      { path: '/assets', component: { template: '<div />' } },
      { path: '/assets/:id', component: AssetDetailPage },
    ],
  });

  await router.push('/assets/asset-1');
  await router.isReady();

  return mount(AssetDetailPage, {
    global: {
      plugins: [router, createTestI18n()],
    },
  });
}

function createAsset(status: 'pending' | 'ready') {
  return {
    id: 'asset-1',
    type: 'image',
    status,
    isPublic: true,
    originalFileName: 'cat.png',
    storedFileName: 'cat.png',
    contentType: 'image/png',
    extension: '.png',
    size: 123,
    width: 100,
    height: 100,
    checksumSha256: null,
    storageProvider: 'local',
    storageProviderProfileId: null,
    storageKey: 'assets/cat.png',
    publicUrl: null,
    description: null,
    altText: null,
    createdAtUtc: '2026-04-10T00:00:00Z',
    updatedAtUtc: '2026-04-10T00:00:00Z',
    derivatives: [],
    structuredResults: [],
    latestExecutionSummary: null,
  };
}

describe('AssetDetailPage', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.clearAllMocks();

    storageApiMocks.getStorageProviderOverview.mockResolvedValue({
      runtime: {
        providerName: 'local',
        providerType: 'local',
      },
      profiles: [],
    });

    workflowApiMocks.listWorkflowProfiles.mockResolvedValue([
      {
        id: 'workflow-1',
        name: 'Caption Workflow',
        description: 'AI Auto Caption',
        isAutoRun: true,
        graphJson: '{"nodes":[],"edges":[]}',
        createdAtUtc: '2026-04-10T00:00:00Z',
        updatedAtUtc: '2026-04-10T00:00:00Z',
      },
    ]);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('shows the processing banner and stops polling after the asset is ready', async () => {
    assetApiMocks.getAsset
      .mockResolvedValueOnce(createAsset('pending'))
      .mockResolvedValueOnce(createAsset('ready'))
      .mockResolvedValue(createAsset('ready'));

    const wrapper = await mountPage();
    await flushPromises();

    expect(wrapper.text()).toContain('资产正在处理中');
    expect(wrapper.text()).toContain('该资产仍在后台处理中');
    expect(assetApiMocks.getAsset).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(5_000);
    await flushPromises();

    expect(assetApiMocks.getAsset).toHaveBeenCalledTimes(2);

    await vi.advanceTimersByTimeAsync(10_000);
    await flushPromises();

    expect(assetApiMocks.getAsset).toHaveBeenCalledTimes(2);
  });

  it('loads workflows and triggers a workflow run for the current asset', async () => {
    assetApiMocks.getAsset.mockResolvedValue(createAsset('ready'));
    assetApiMocks.runAssetWorkflow.mockResolvedValue({
      assetId: 'asset-1',
      workflowId: 'workflow-1',
      skillIds: ['ai-caption'],
    });

    const wrapper = await mountPage();
    await flushPromises();

    expect(workflowApiMocks.listWorkflowProfiles).toHaveBeenCalledTimes(1);
    expect(wrapper.text()).toContain('Caption Workflow');
    expect(wrapper.text()).toContain('AI Auto Caption');

    const runButton = wrapper.findAll('button').find((button) => button.text().includes('运行工作流'));
    expect(runButton).toBeTruthy();

    await runButton!.trigger('click');
    await flushPromises();

    expect(assetApiMocks.runAssetWorkflow).toHaveBeenCalledWith('asset-1', 'workflow-1');
    expect(message.success).toHaveBeenCalledWith('已触发工作流 Caption Workflow');
    expect(assetApiMocks.getAsset).toHaveBeenCalledTimes(2);
  });
});
