import { defineComponent, h, nextTick } from 'vue';
import { flushPromises, mount } from '@vue/test-utils';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { WorkflowNode } from '../../types/workflows';
import WorkflowEditorPage from './WorkflowEditorPage.vue';

const permissions = vi.hoisted(() => ({ can: vi.fn(() => true) }));
const flow = vi.hoisted(() => ({
  nodes: [] as WorkflowNode[],
  emitNodes: undefined as ((nodes: WorkflowNode[]) => void) | undefined,
  addNodes: vi.fn(),
  addSelectedNodes: vi.fn(),
  removeSelectedElements: vi.fn(),
  setNodes: vi.fn(),
  fitView: vi.fn(),
  screenToFlowCoordinate: vi.fn(),
}));

vi.mock('../../composables/useAuthPermissions', () => ({ useAuthPermissions: () => permissions }));
vi.mock('../../api/system/workflows.api', () => ({
  listWorkflowProfiles: vi.fn().mockResolvedValue([]),
  createWorkflowProfile: vi.fn(),
  updateWorkflowProfile: vi.fn(),
  deleteWorkflowProfile: vi.fn(),
  setWorkflowAutoRun: vi.fn(),
}));
vi.mock('vue-i18n', () => ({ useI18n: () => ({ t: (key: string) => key }) }));
vi.mock('@vue-flow/background', () => ({ Background: defineComponent({ render: () => null }) }));
vi.mock('@vue-flow/controls', () => ({ Controls: defineComponent({ render: () => null }) }));
vi.mock('@vue-flow/node-toolbar', () => ({ NodeToolbar: defineComponent({ render: () => null }) }));
vi.mock('@vue-flow/core', () => ({
  Position: { Left: 'left', Right: 'right', Top: 'top' },
  MarkerType: { ArrowClosed: 'arrowclosed' },
  Handle: defineComponent({ render: () => null }),
  VueFlow: defineComponent({
    props: ['nodes', 'edges'],
    emits: ['update:nodes', 'update:edges'],
    setup(_, { emit, slots }) {
      flow.emitNodes = (nodes) => emit('update:nodes', nodes);
      return () => h('div', slots.default?.());
    },
  }),
  useVueFlow: () => ({
    addNodes: flow.addNodes,
    addSelectedNodes: flow.addSelectedNodes,
    removeSelectedElements: flow.removeSelectedElements,
    findNode: (id: string) => flow.nodes.find((node) => node.id === id),
    setNodes: flow.setNodes,
    fitView: flow.fitView,
    screenToFlowCoordinate: flow.screenToFlowCoordinate,
    addEdges: vi.fn(),
    setEdges: vi.fn(),
    setViewport: vi.fn(),
    updateNodeData: vi.fn(),
    toObject: () => ({ nodes: flow.nodes, edges: [], viewport: { x: 0, y: 0, zoom: 1 } }),
  }),
}));
vi.mock('naive-ui', () => {
  const container = defineComponent({
    setup: (_, { slots }) => () => h('div', [slots.default?.(), slots['header-extra']?.()]),
  });
  return {
    NAlert: container, NButton: container, NCard: container, NDrawerContent: container,
    NEmpty: container, NForm: container, NFormItem: container, NInput: container,
    NInputNumber: container, NPopconfirm: container, NSelect: container, NSlider: container,
    NSpace: container, NSwitch: container, NTag: container,
    NDrawer: defineComponent({
      props: ['show', 'width'],
      setup: (props, { slots }) => () => h('aside', {
        'data-testid': 'node-drawer', 'data-show': props.show, style: { width: props.width },
      }, slots.default?.()),
    }),
    useDialog: () => ({ warning: vi.fn() }),
    useMessage: () => ({ warning: vi.fn(), success: vi.fn(), error: vi.fn() }),
  };
});

async function mountEditor() {
  const wrapper = mount(WorkflowEditorPage, { global: { stubs: { PageHeader: true, SkillNode: true } } });
  await flushPromises();
  return wrapper;
}

describe('workflow skill palette', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    permissions.can.mockReturnValue(true);
    flow.nodes = [];
    flow.emitNodes = undefined;
    flow.setNodes.mockImplementation((nodes: WorkflowNode[]) => {
      flow.nodes = nodes;
      flow.emitNodes?.(nodes);
    });
    flow.addNodes.mockImplementation((node: WorkflowNode) => {
      flow.nodes = [...flow.nodes, node];
      flow.emitNodes?.(flow.nodes);
    });
    flow.fitView.mockResolvedValue(undefined);
    flow.screenToFlowCoordinate.mockImplementation((position) => position);
  });

  it('adds a configured node with a native keyboard click and selects its drawer', async () => {
    const wrapper = await mountEditor();
    const button = wrapper.findAll('.skill-palette__item')
      .find((item) => item.text().includes('workflows.skills.formatConvert.label'))!;
    expect(button.element).toBeInstanceOf(HTMLButtonElement);
    expect(button.attributes('type')).toBe('button');

    // JSDOM 不合成默认键盘点击；detail=0 对应原生按钮键盘激活产生的点击。
    button.element.dispatchEvent(new MouseEvent('click', { bubbles: true, detail: 0 }));
    await nextTick();
    await flushPromises();

    expect(flow.addNodes).toHaveBeenCalledOnce();
    expect(flow.nodes[0]).toMatchObject({
      position: { x: 80, y: 160 },
      data: { skillId: 'format-convert', parameters: { TargetFormat: 'webp', KeepOriginal: false } },
    });
    expect(wrapper.get('[data-testid="node-drawer"]').attributes('data-show')).toBe('true');
    expect(wrapper.get('[data-testid="node-drawer"]').find('[label="workflows.drawer.fields.targetFormat"]').exists()).toBe(true);
    expect(flow.fitView).toHaveBeenCalledOnce();
    expect(flow.removeSelectedElements).toHaveBeenCalledOnce();
    expect(flow.addSelectedNodes).toHaveBeenCalledWith([flow.nodes[0]]);
    wrapper.unmount();
  });

  it('appends clicks after the last node without overlapping or changing execution order', async () => {
    const wrapper = await mountEditor();
    const buttons = wrapper.findAll('.skill-palette__item');
    await buttons[0]!.trigger('click');
    await flushPromises();
    await buttons[1]!.trigger('click');
    await flushPromises();

    expect(flow.nodes.map((node) => node.data?.skillId)).toEqual(['thumbnail', 'ai-caption']);
    expect(flow.nodes.map((node) => node.position)).toEqual([{ x: 80, y: 160 }, { x: 400, y: 160 }]);
    expect(flow.removeSelectedElements).toHaveBeenCalledTimes(2);
    expect(flow.addSelectedNodes).toHaveBeenLastCalledWith([flow.nodes[1]]);
    expect(wrapper.get('[data-testid="node-drawer"]').text()).toContain('workflows.skills.aiCaption.label');
    wrapper.unmount();
  });

  it('keeps drag-and-drop coordinates and adds only one selected node', async () => {
    const wrapper = await mountEditor();
    const button = wrapper.findAll('.skill-palette__item')[0]!;
    const dataTransfer = { setData: vi.fn(), getData: vi.fn(() => 'thumbnail'), effectAllowed: '', dropEffect: '' };
    expect(button.attributes('draggable')).toBe('true');
    await button.trigger('dragstart', { dataTransfer });
    await wrapper.get('.workflow-canvas-shell').trigger('drop', { dataTransfer, clientX: 480, clientY: 240 });
    await button.trigger('dragend');
    await flushPromises();

    expect(dataTransfer.setData).toHaveBeenCalledWith('application/nekohub-workflow-skill', 'thumbnail');
    expect(flow.screenToFlowCoordinate).toHaveBeenCalledWith({ x: 480, y: 240 });
    expect(flow.addNodes).toHaveBeenCalledOnce();
    expect(flow.nodes[0]).toMatchObject({ position: { x: 348, y: 196 } });
    expect(flow.addSelectedNodes).toHaveBeenCalledWith([flow.nodes[0]]);
    wrapper.unmount();
  });

  it('disables native palette buttons and rejects drops without write permission', async () => {
    permissions.can.mockReturnValue(false);
    const wrapper = await mountEditor();
    const buttons = wrapper.findAll('.skill-palette__item');
    expect(buttons.every((button) => (button.element as HTMLButtonElement).disabled)).toBe(true);
    expect(buttons.every((button) => button.attributes('draggable') === 'false')).toBe(true);
    buttons[0]!.element.dispatchEvent(new MouseEvent('click', { bubbles: true, detail: 0 }));
    await nextTick();
    await wrapper.get('.workflow-canvas-shell').trigger('drop', {
      dataTransfer: { getData: () => 'thumbnail' }, clientX: 480, clientY: 240,
    });
    await flushPromises();

    expect(flow.addNodes).not.toHaveBeenCalled();
    expect(wrapper.get('[data-testid="node-drawer"]').attributes('data-show')).toBe('false');
    wrapper.unmount();
  });
});
