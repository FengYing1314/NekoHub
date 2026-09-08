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
  getAssetDerivativeContentBlob: vi.fn(),
  patchAsset: vi.fn(),
  runAssetWorkflow: vi.fn(),
  listAssetWorkflows: vi.fn(),
  getAssetJobs: vi.fn(),
  retryAssetJob: vi.fn(),
}));

const storageApiMocks = vi.hoisted(() => ({
  getStorageProviderOverview: vi.fn(),
}));

const permissions = vi.hoisted(() => ({ can: vi.fn(() => true) }));

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
    NPopconfirm: defineComponent({
      emits: ['positive-click'],
      setup: (_, { slots, emit }) => () => h('div', [slots.trigger?.(), slots.default?.(),
        h('button', { 'data-confirm': true, onClick: () => emit('positive-click') }, '确认')]),
    }),
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
  getAssetDerivativeContentBlob: assetApiMocks.getAssetDerivativeContentBlob,
  patchAsset: assetApiMocks.patchAsset,
  runAssetWorkflow: assetApiMocks.runAssetWorkflow,
  listAssetWorkflows: assetApiMocks.listAssetWorkflows,
  getAssetJobs: assetApiMocks.getAssetJobs,
  retryAssetJob: assetApiMocks.retryAssetJob,
}));

vi.mock('../../api/system/storage.api', () => ({
  getStorageProviderOverview: storageApiMocks.getStorageProviderOverview,
}));


vi.mock('../../composables/useIsMobile', () => ({
  useIsMobile: () => ({
    isMobile: { value: false },
  }),
}));

vi.mock('../../composables/useAuthPermissions', () => ({
  useAuthPermissions: () => ({
    can: permissions.can,
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
    permissions.can.mockImplementation(() => true);
    assetApiMocks.getAssetJobs.mockResolvedValue([]);

    storageApiMocks.getStorageProviderOverview.mockResolvedValue({
      runtime: {
        providerName: 'local',
        providerType: 'local',
      },
      profiles: [],
    });

    assetApiMocks.listAssetWorkflows.mockResolvedValue([
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

    expect(assetApiMocks.listAssetWorkflows).toHaveBeenCalledTimes(1);
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
  it('allows asset operators to run workflows without system settings or provider permissions', async () => {
    permissions.can.mockImplementation((...args: unknown[]) => ['assets.read', 'assets.update'].includes(String(args[0])));
    assetApiMocks.getAsset.mockResolvedValue(createAsset('ready'));
    const wrapper = await mountPage();
    await flushPromises();
    expect(assetApiMocks.listAssetWorkflows).toHaveBeenCalledOnce();
    expect(storageApiMocks.getStorageProviderOverview).not.toHaveBeenCalled();
    const runButton = wrapper.findAll('button').find((button) => button.text().includes('运行工作流'));
    expect(runButton?.attributes('disabled')).toBeUndefined();
  });

  it('loads failed jobs and retries them with asset update permission', async () => {
    assetApiMocks.getAsset.mockResolvedValue(createAsset('ready'));
    const job = { id: 'job-1', assetId: 'asset-1', status: 'failed', attempts: 1,
      createdAtUtc: '2026-04-10T00:00:00Z', updatedAtUtc: '2026-04-10T00:00:00Z', errorMessage: 'Provider offline' };
    assetApiMocks.getAssetJobs.mockResolvedValueOnce([job]).mockResolvedValue([{ ...job, status: 'pending', errorMessage: null }]);
    const wrapper = await mountPage();
    await flushPromises();
    expect(wrapper.get('[data-testid="asset-job"]').text()).toContain('Provider offline');
    await wrapper.get('[data-testid="asset-job"] button').trigger('click');
    expect(assetApiMocks.retryAssetJob).not.toHaveBeenCalled();
    expect(wrapper.text()).toContain('请先核对当前图片');
    await wrapper.get('[data-testid="asset-job"] [data-confirm]').trigger('click');
    await flushPromises();
    expect(assetApiMocks.retryAssetJob).toHaveBeenCalledWith('asset-1', 'job-1');
    expect(wrapper.get('[data-testid="asset-job"]').text()).toContain('等待处理');
  });

  it('downloads private content without treating noopener as a blocked popup', async () => {
    assetApiMocks.getAsset.mockResolvedValue({ ...createAsset('ready'), isPublic: false });
    assetApiMocks.getAssetContentBlob.mockResolvedValue(new Blob(['image'], { type: 'image/png' }));
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
    const originalCreate = URL.createObjectURL;
    const originalRevoke = URL.revokeObjectURL;
    URL.createObjectURL = vi.fn(() => 'blob:asset');
    URL.revokeObjectURL = vi.fn();
    const wrapper = await mountPage();
    await flushPromises();
    const download = wrapper.findAll('button').find((button) => button.text().includes('下载原文件'))!;
    await download.trigger('click');
    await flushPromises();
    expect(click).toHaveBeenCalledOnce();
    expect(message.warning).not.toHaveBeenCalled();
    wrapper.unmount();
    click.mockRestore();
    URL.createObjectURL = originalCreate;
    URL.revokeObjectURL = originalRevoke;
  });

  it('downloads a retained private original through the authenticated derivative endpoint', async () => {
    const derivative = { kind: 'original_snapshot', contentType: 'image/png', extension: '.png', size: 5,
      width: 1, height: 1, publicUrl: null, createdAtUtc: '2026-04-10T00:00:00Z' };
    assetApiMocks.getAsset.mockResolvedValue({ ...createAsset('ready'), isPublic: false, derivatives: [derivative] });
    assetApiMocks.getAssetDerivativeContentBlob.mockResolvedValue(new Blob(['image']));
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
    const originalCreate = URL.createObjectURL;
    const originalRevoke = URL.revokeObjectURL;
    URL.createObjectURL = vi.fn(() => 'blob:original');
    URL.revokeObjectURL = vi.fn();
    const wrapper = await mountPage();
    await flushPromises();
    await wrapper.findAll('button').find((button) => button.text().includes('下载保留原图'))!.trigger('click');
    await flushPromises();
    expect(assetApiMocks.getAssetDerivativeContentBlob).toHaveBeenCalledWith('asset-1', 'original_snapshot');
    expect(click).toHaveBeenCalledOnce();
    expect((click.mock.instances[0] as HTMLAnchorElement).download).toBe('original_snapshot.png');
    wrapper.unmount();
    click.mockRestore();
    URL.createObjectURL = originalCreate;
    URL.revokeObjectURL = originalRevoke;
  });

});
