<script setup lang="ts">
import { computed, nextTick, onMounted, reactive, ref, watch } from 'vue';
import {
  NAlert,
  NButton,
  NCard,
  NDrawer,
  NDrawerContent,
  NEmpty,
  NForm,
  NFormItem,
  NInput,
  NInputNumber,
  NPopconfirm,
  NSelect,
  NSlider,
  NSpace,
  NSwitch,
  NTag,
  useDialog,
  useMessage,
  type SelectOption,
} from 'naive-ui';
import {
  MarkerType,
  Position,
  VueFlow,
  useVueFlow,
  type Connection,
  type Edge,
  type FlowExportObject,
  type Node,
  type NodeMouseEvent,
  type XYPosition,
} from '@vue-flow/core';
import { Background } from '@vue-flow/background';
import { Controls } from '@vue-flow/controls';
import { useI18n } from 'vue-i18n';
import PageHeader from '../../components/common/PageHeader.vue';
import SkillNode from '../../components/workflow/SkillNode.vue';
import { extractApiErrorMessage } from '../../api/client/error';
import {
  createWorkflowProfile,
  deleteWorkflowProfile,
  listWorkflowProfiles,
  setWorkflowAutoRun,
  updateWorkflowProfile,
} from '../../api/system/workflows.api';
import { useAuthPermissions } from '../../composables/useAuthPermissions';
import { PERMISSIONS } from '../../constants/permissions';
import type { JsonObject, JsonValue } from '../../types/api';
import {
  WORKFLOW_FORMAT_CONVERT_TARGET_FORMATS,
  WORKFLOW_WATERMARK_POSITIONS,
  type WorkflowEdge,
  type WorkflowFormatConvertParameters,
  type WorkflowFormatConvertTargetFormat,
  type WorkflowGraphDocument,
  type WorkflowNode,
  type WorkflowProfileResponse,
  type WorkflowSkillNodeData,
  type WorkflowViewport,
  type WorkflowWatermarkParameters,
  type WorkflowWatermarkPosition,
} from '../../types/workflows';
import { WORKFLOW_SKILL_CATALOG, getWorkflowSkillCatalogItem } from './skill-catalog';
import '@vue-flow/core/dist/style.css';
import '@vue-flow/core/dist/theme-default.css';
import '@vue-flow/controls/dist/style.css';

interface WorkflowDraft {
  id: string | null;
  name: string;
  description: string;
  isAutoRun: boolean;
}

const WORKFLOW_DRAG_MIME = 'application/nekohub-workflow-skill';
const FLOW_ID = 'workflow-editor';
const FORMAT_CONVERT_SKILL_ID = 'format-convert';
const WATERMARK_SKILL_ID = 'watermark';

const { t } = useI18n();
const dialog = useDialog();
const message = useMessage();
const { can } = useAuthPermissions();

const {
  addEdges,
  addNodes,
  fitView,
  screenToFlowCoordinate,
  setEdges,
  setNodes,
  setViewport,
  toObject,
  updateNodeData,
} = useVueFlow({ id: FLOW_ID });

const loading = ref(false);
const saving = ref(false);
const deleting = ref(false);
const settingAutoRun = ref(false);
const loadErrorMessage = ref('');
const parseWarningMessage = ref('');
const workflows = ref<WorkflowProfileResponse[]>([]);
const selectedWorkflowId = ref<string | null>(null);
const selectedNodeId = ref<string | null>(null);
const isNodeDrawerOpen = ref(false);
const nodes = ref<WorkflowNode[]>([]);
const edges = ref<WorkflowEdge[]>([]);
const dragSkillId = ref<string | null>(null);
const canvasDragActive = ref(false);
const hydratingEditor = ref(false);
const isDirty = ref(false);

const draft = reactive<WorkflowDraft>({
  id: null,
  name: '',
  description: '',
  isAutoRun: false,
});

function findNodeById(nodeId: string | null): WorkflowNode | null {
  if (!nodeId) {
    return null;
  }

  const currentNodes = nodes.value as WorkflowNode[];

  for (let index = 0; index < currentNodes.length; index += 1) {
    const node = currentNodes[index];
    if (node && node.id === nodeId) {
      return node;
    }
  }

  return null;
}

const canManageWorkflows = computed(() => can(PERMISSIONS.settingsUpdate));
const currentWorkflow = computed<WorkflowProfileResponse | null>(() => (
  workflows.value.find((item) => item.id === draft.id) ?? null
));
const selectedNode = computed<WorkflowNode | null>(() => findNodeById(selectedNodeId.value));
const selectedNodeSkillId = computed<string | null>(() => (
  selectedNode.value
    ? resolveNodeSkillId(selectedNode.value)
    : null
));
const selectedNodeMeta = computed(() => (
  selectedNodeSkillId.value
    ? getWorkflowSkillCatalogItem(selectedNodeSkillId.value)
    : undefined
));
const selectedNodeTitle = computed(() => (
  selectedNodeMeta.value
    ? t(selectedNodeMeta.value.labelKey)
    : (selectedNodeSkillId.value ?? t('workflows.unknownSkill'))
));
const selectedNodeDescription = computed(() => (
  selectedNodeMeta.value
    ? t(selectedNodeMeta.value.descriptionKey)
    : t('workflows.unknownSkill')
));
const workflowOptions = computed<SelectOption[]>(() => workflows.value.map((item) => ({
  label: item.name,
  value: item.id,
})));
const targetFormatOptions = computed<SelectOption[]>(() => (
  WORKFLOW_FORMAT_CONVERT_TARGET_FORMATS.map((format) => ({
    label: format.toUpperCase(),
    value: format,
  }))
));
const watermarkPositionOptions = computed<SelectOption[]>(() => (
  WORKFLOW_WATERMARK_POSITIONS.map((position) => ({
    label: t(getWatermarkPositionLabelKey(position)),
    value: position,
  }))
));
const canSave = computed(() => (
  canManageWorkflows.value
  && draft.name.trim().length > 0
  && !saving.value
  && !deleting.value
));
const canQuickSetAutoRun = computed(() => (
  canManageWorkflows.value
  && Boolean(currentWorkflow.value)
  && !draft.isAutoRun
  && !isDirty.value
));

const formatConvertTargetFormat = computed<WorkflowFormatConvertTargetFormat>({
  get() {
    const parameters = getSelectedNodeParameters();
    const rawValue = parameters?.TargetFormat;

    return isWorkflowFormatConvertTargetFormat(rawValue) ? rawValue : 'webp';
  },
  set(value) {
    updateSelectedNodeParameters({
      TargetFormat: value,
    });
  },
});

const formatConvertKeepOriginal = computed<boolean>({
  get() {
    const parameters = getSelectedNodeParameters();

    return typeof parameters?.KeepOriginal === 'boolean' ? parameters.KeepOriginal : false;
  },
  set(value) {
    updateSelectedNodeParameters({
      KeepOriginal: value,
    });
  },
});

const watermarkText = computed<string>({
  get() {
    const parameters = getSelectedNodeParameters();

    return typeof parameters?.Text === 'string' ? parameters.Text : 'NekoHub';
  },
  set(value) {
    updateSelectedNodeParameters({
      Text: value,
    });
  },
});

const watermarkPosition = computed<WorkflowWatermarkPosition>({
  get() {
    const parameters = getSelectedNodeParameters();
    const rawValue = parameters?.Position;

    return isWorkflowWatermarkPosition(rawValue) ? rawValue : 'BottomRight';
  },
  set(value) {
    updateSelectedNodeParameters({
      Position: value,
    });
  },
});

const watermarkOpacity = computed<number>(() => {
  const parameters = getSelectedNodeParameters();
  const rawValue = parameters?.Opacity;

  return typeof rawValue === 'number' && Number.isFinite(rawValue)
    ? clampOpacity(rawValue)
    : 0.5;
});

const watermarkFontSize = computed<number>(() => {
  const parameters = getSelectedNodeParameters();
  const rawValue = parameters?.FontSize;

  return typeof rawValue === 'number' && Number.isFinite(rawValue)
    ? clampFontSize(rawValue)
    : 36;
});

const defaultEdgeOptions = {
  type: 'smoothstep',
  markerEnd: MarkerType.ArrowClosed,
  animated: true,
} satisfies Partial<WorkflowEdge>;

function createDraftDefaults(): WorkflowDraft {
  return {
    id: null,
    name: t('workflows.defaultName'),
    description: '',
    isAutoRun: false,
  };
}

function createEmptyGraph(): WorkflowGraphDocument {
  return {
    nodes: [],
    edges: [],
    viewport: {
      x: 0,
      y: 0,
      zoom: 1,
    },
  };
}

function createElementId(prefix: string): string {
  if (typeof window !== 'undefined' && typeof window.crypto?.randomUUID === 'function') {
    return `${prefix}-${window.crypto.randomUUID()}`;
  }

  return `${prefix}-${Date.now()}-${Math.round(Math.random() * 1_000_000)}`;
}

function isJsonObject(value: unknown): value is JsonObject {
  return Boolean(value) && typeof value === 'object' && !Array.isArray(value);
}

function isJsonValue(value: unknown): value is JsonValue {
  if (
    value === null
    || typeof value === 'string'
    || typeof value === 'number'
    || typeof value === 'boolean'
  ) {
    return true;
  }

  if (Array.isArray(value)) {
    return value.every((item) => isJsonValue(item));
  }

  if (isJsonObject(value)) {
    return Object.values(value).every((item) => isJsonValue(item));
  }

  return false;
}

function cloneJsonValue<T extends JsonValue>(value: T): T {
  if (Array.isArray(value)) {
    return value.map((item) => cloneJsonValue(item)) as T;
  }

  if (isJsonObject(value)) {
    const clone: JsonObject = {};
    for (const [key, item] of Object.entries(value)) {
      clone[key] = cloneJsonValue(item);
    }

    return clone as T;
  }

  return value;
}

function cloneJsonObject(value: JsonObject): JsonObject {
  return cloneJsonValue(value);
}

function isWorkflowFormatConvertTargetFormat(value: unknown): value is WorkflowFormatConvertTargetFormat {
  return typeof value === 'string'
    && WORKFLOW_FORMAT_CONVERT_TARGET_FORMATS.some((item) => item === value);
}

function isWorkflowWatermarkPosition(value: unknown): value is WorkflowWatermarkPosition {
  return typeof value === 'string'
    && WORKFLOW_WATERMARK_POSITIONS.some((item) => item === value);
}

function normalizeFormatConvertTargetFormat(value: unknown): WorkflowFormatConvertTargetFormat {
  if (isWorkflowFormatConvertTargetFormat(value)) {
    return value;
  }

  if (typeof value === 'string') {
    const normalized = value.trim().toLowerCase();
    if (isWorkflowFormatConvertTargetFormat(normalized)) {
      return normalized;
    }
  }

  return 'webp';
}

function normalizeWatermarkPosition(value: unknown): WorkflowWatermarkPosition {
  if (isWorkflowWatermarkPosition(value)) {
    return value;
  }

  if (typeof value === 'string') {
    const normalized = value.trim().toLowerCase();

    switch (normalized) {
      case 'bottomright':
        return 'BottomRight';
      case 'bottomleft':
        return 'BottomLeft';
      case 'center':
        return 'Center';
      case 'topright':
        return 'TopRight';
      case 'topleft':
        return 'TopLeft';
      default:
        break;
    }
  }

  return 'BottomRight';
}

function coerceBoolean(value: unknown, fallback: boolean): boolean {
  if (typeof value === 'boolean') {
    return value;
  }

  if (typeof value === 'number') {
    if (value === 1) {
      return true;
    }

    if (value === 0) {
      return false;
    }
  }

  if (typeof value === 'string') {
    const normalized = value.trim().toLowerCase();
    if (normalized === 'true') {
      return true;
    }

    if (normalized === 'false') {
      return false;
    }
  }

  return fallback;
}

function coerceNumber(value: unknown, fallback: number): number {
  if (typeof value === 'number' && Number.isFinite(value)) {
    return value;
  }

  if (typeof value === 'string' && value.trim().length > 0) {
    const parsed = Number(value);
    if (Number.isFinite(parsed)) {
      return parsed;
    }
  }

  return fallback;
}

function coerceInteger(value: unknown, fallback: number): number {
  return Math.round(coerceNumber(value, fallback));
}

function coerceString(value: unknown, fallback: string): string {
  return typeof value === 'string' ? value : fallback;
}

function createDefaultSkillParameters(skillId: string): JsonObject | undefined {
  switch (skillId) {
    case FORMAT_CONVERT_SKILL_ID:
      return {
        TargetFormat: 'webp',
        KeepOriginal: false,
      } satisfies WorkflowFormatConvertParameters;
    case WATERMARK_SKILL_ID:
      return {
        Text: 'NekoHub',
        Opacity: 0.5,
        FontSize: 36,
        Position: 'BottomRight',
      } satisfies WorkflowWatermarkParameters;
    default:
      return undefined;
  }
}

function sanitizeFormatConvertParameters(rawParameters: JsonObject | undefined): WorkflowFormatConvertParameters {
  return {
    TargetFormat: normalizeFormatConvertTargetFormat(rawParameters?.TargetFormat),
    KeepOriginal: coerceBoolean(rawParameters?.KeepOriginal, false),
  };
}

function sanitizeWatermarkParameters(rawParameters: JsonObject | undefined): WorkflowWatermarkParameters {
  return {
    Text: coerceString(rawParameters?.Text, 'NekoHub'),
    Opacity: clampOpacity(coerceNumber(rawParameters?.Opacity, 0.5)),
    FontSize: clampFontSize(coerceInteger(rawParameters?.FontSize, 36)),
    Position: normalizeWatermarkPosition(rawParameters?.Position),
  };
}

function collectLegacyParameters(rawData: JsonObject): JsonObject | undefined {
  const parameters: JsonObject = {};

  for (const [key, value] of Object.entries(rawData)) {
    if (key === 'skillId' || key === 'parameters') {
      continue;
    }

    if (isJsonValue(value)) {
      parameters[key] = cloneJsonValue(value);
    }
  }

  return Object.keys(parameters).length > 0 ? parameters : undefined;
}

function normalizeSkillParameters(skillId: string, rawData: unknown): JsonObject | undefined {
  const defaults = createDefaultSkillParameters(skillId);
  const dataObject = isJsonObject(rawData) ? rawData : undefined;
  const explicitParameters = dataObject && isJsonObject(dataObject.parameters)
    ? cloneJsonObject(dataObject.parameters)
    : undefined;
  const legacyParameters = dataObject ? collectLegacyParameters(dataObject) : undefined;
  const merged: JsonObject = {};

  if (defaults) {
    Object.assign(merged, defaults);
  }

  if (legacyParameters) {
    Object.assign(merged, legacyParameters);
  }

  if (explicitParameters) {
    Object.assign(merged, explicitParameters);
  }

  if (skillId === FORMAT_CONVERT_SKILL_ID) {
    return sanitizeFormatConvertParameters(merged);
  }

  if (skillId === WATERMARK_SKILL_ID) {
    return sanitizeWatermarkParameters(merged);
  }

  return Object.keys(merged).length > 0 ? merged : undefined;
}

function resolveNodeSkillId(node: Node): string {
  const rawData = node.data as Partial<WorkflowSkillNodeData> | undefined;
  const dataSkillId = typeof rawData?.skillId === 'string' ? rawData.skillId.trim() : '';
  if (dataSkillId) {
    return dataSkillId;
  }

  const nodeType = typeof node.type === 'string' ? node.type.trim() : '';
  if (nodeType && nodeType !== 'skill') {
    return nodeType;
  }

  return 'unknown-skill';
}

function normalizeNodeData(node: Node): WorkflowSkillNodeData {
  const skillId = resolveNodeSkillId(node);
  const parameters = normalizeSkillParameters(skillId, node.data);

  return parameters
    ? {
      skillId,
      parameters,
    }
    : {
      skillId,
    };
}

function buildWorkflowNodeData(skillId: string, parameters?: JsonObject): WorkflowSkillNodeData {
  const normalizedParameters = normalizeSkillParameters(
    skillId,
    parameters
      ? {
        skillId,
        parameters,
      }
      : {
        skillId,
      },
  );

  return normalizedParameters
    ? {
      skillId,
      parameters: normalizedParameters,
    }
    : {
      skillId,
    };
}

function areWorkflowNodeDataEqual(
  left: Partial<WorkflowSkillNodeData> | undefined,
  right: WorkflowSkillNodeData,
): boolean {
  const leftSkillId = typeof left?.skillId === 'string' ? left.skillId : '';
  const leftParameters = isJsonObject(left?.parameters) ? left.parameters : undefined;

  return leftSkillId === right.skillId
    && JSON.stringify(leftParameters ?? null) === JSON.stringify(right.parameters ?? null);
}

function syncNodeData(node: WorkflowNode): WorkflowSkillNodeData {
  const nextData = normalizeNodeData(node);
  const currentData = node.data as Partial<WorkflowSkillNodeData> | undefined;

  if (!areWorkflowNodeDataEqual(currentData, nextData)) {
    // 节点被选中或导入旧图后，统一把 data 规整成当前前端约定，减少后续保存时的噪音差异。
    updateNodeData<WorkflowSkillNodeData>(node.id, nextData, { replace: true });
  }

  return nextData;
}

function normalizeNodes(rawNodes: Node[] | undefined): WorkflowNode[] {
  if (!Array.isArray(rawNodes)) {
    return [];
  }

  return rawNodes
    .filter((node): node is Node => Boolean(node && typeof node.id === 'string'))
    .map((node, index) => ({
      ...node,
      id: node.id,
      type: 'skill',
      position: node.position ?? {
        x: 80 + index * 220,
        y: 160,
      },
      sourcePosition: node.sourcePosition ?? Position.Right,
      targetPosition: node.targetPosition ?? Position.Left,
      data: normalizeNodeData(node),
    }));
}

function normalizeEdges(rawEdges: Edge[] | undefined): WorkflowEdge[] {
  if (!Array.isArray(rawEdges)) {
    return [];
  }

  return rawEdges
    .filter((edge): edge is Edge => Boolean(
      edge
      && typeof edge.source === 'string'
      && typeof edge.target === 'string',
    ))
    .map((edge) => ({
      ...edge,
      id: edge.id || createElementId('edge'),
      type: edge.type ?? 'smoothstep',
      animated: edge.animated ?? true,
      markerEnd: edge.markerEnd ?? MarkerType.ArrowClosed,
    }));
}

function parseViewport(value: unknown): WorkflowViewport | undefined {
  if (!value || typeof value !== 'object') {
    return undefined;
  }

  const viewport = value as Partial<WorkflowViewport>;
  if (
    typeof viewport.x !== 'number'
    || typeof viewport.y !== 'number'
    || typeof viewport.zoom !== 'number'
  ) {
    return undefined;
  }

  return {
    x: viewport.x,
    y: viewport.y,
    zoom: viewport.zoom,
  };
}

function parseWorkflowGraph(graphJson: string): WorkflowGraphDocument {
  const parsed = JSON.parse(graphJson) as Partial<WorkflowGraphDocument>;

  return {
    nodes: normalizeNodes(parsed.nodes),
    edges: normalizeEdges(parsed.edges),
    viewport: parseViewport(parsed.viewport),
  };
}

function compareNodesForExecution(left: WorkflowNode, right: WorkflowNode): number {
  if (left.position.x !== right.position.x) {
    return left.position.x - right.position.x;
  }

  if (left.position.y !== right.position.y) {
    return left.position.y - right.position.y;
  }

  return left.id.localeCompare(right.id);
}

function buildGraphJson(): string {
  const flowObject = toObject() as FlowExportObject;

  const graph: WorkflowGraphDocument = {
    // 节点按画布位置排序后再序列化，尽量让同一张图在未改结构时生成稳定 JSON。
    nodes: normalizeNodes(flowObject.nodes).sort(compareNodesForExecution),
    edges: normalizeEdges(flowObject.edges),
    viewport: parseViewport(flowObject.viewport),
  };

  return JSON.stringify(graph);
}

function getSelectedNodeParameters(): JsonObject | null {
  const node = selectedNode.value;
  if (!node) {
    return null;
  }

  return normalizeNodeData(node).parameters ?? null;
}

function updateSelectedNodeParameters(nextPartial: JsonObject): void {
  const node = selectedNode.value;
  if (!node) {
    return;
  }

  updateNodeData<WorkflowSkillNodeData>(node.id, (currentNode) => {
    const skillId = resolveNodeSkillId(currentNode);
    const currentParameters = normalizeSkillParameters(skillId, currentNode.data);

    return buildWorkflowNodeData(skillId, {
      ...(currentParameters ?? {}),
      ...nextPartial,
    });
  }, { replace: true });
}

function clampOpacity(value: number): number {
  return Math.max(0, Math.min(1, Number(value.toFixed(2))));
}

function clampFontSize(value: number): number {
  return Math.max(8, Math.min(512, Math.round(value)));
}

function getWatermarkPositionLabelKey(position: WorkflowWatermarkPosition): string {
  switch (position) {
    case 'BottomRight':
      return 'workflows.drawer.positions.bottomRight';
    case 'BottomLeft':
      return 'workflows.drawer.positions.bottomLeft';
    case 'Center':
      return 'workflows.drawer.positions.center';
    case 'TopRight':
      return 'workflows.drawer.positions.topRight';
    case 'TopLeft':
      return 'workflows.drawer.positions.topLeft';
    default:
      return 'workflows.drawer.positions.bottomRight';
  }
}

async function applyEditorState(
  nextDraft: WorkflowDraft,
  graph: WorkflowGraphDocument,
): Promise<void> {
  // hydratingEditor 用来屏蔽程序化回填触发的 dirty 标记，只把真实用户修改算作未保存变更。
  hydratingEditor.value = true;
  parseWarningMessage.value = '';
  selectedNodeId.value = null;
  isNodeDrawerOpen.value = false;

  draft.id = nextDraft.id;
  draft.name = nextDraft.name;
  draft.description = nextDraft.description;
  draft.isAutoRun = nextDraft.isAutoRun;
  selectedWorkflowId.value = nextDraft.id;

  setNodes(graph.nodes);
  setEdges(graph.edges);

  await nextTick();

  if (graph.viewport) {
    await setViewport(graph.viewport);
  } else if (graph.nodes.length > 0) {
    await fitView({ padding: 0.16 });
  } else {
    await setViewport({
      x: 0,
      y: 0,
      zoom: 1,
    });
  }

  hydratingEditor.value = false;
  isDirty.value = false;
}

async function openDraftProfile(profile: WorkflowProfileResponse): Promise<void> {
  let graph = createEmptyGraph();
  parseWarningMessage.value = '';

  try {
    graph = parseWorkflowGraph(profile.graphJson);
  } catch {
    parseWarningMessage.value = t('workflows.messages.parseGraphFallback');
  }

  await applyEditorState(
    {
      id: profile.id,
      name: profile.name,
      description: profile.description ?? '',
      isAutoRun: profile.isAutoRun,
    },
    graph,
  );
}

async function openBlankDraft(): Promise<void> {
  await applyEditorState(createDraftDefaults(), createEmptyGraph());
}

function confirmDiscardChanges(): Promise<boolean> {
  if (!isDirty.value) {
    return Promise.resolve(true);
  }

  return new Promise((resolve) => {
    const dialogReactive = dialog.warning({
      title: t('workflows.title'),
      content: t('workflows.confirmDiscardChanges'),
      positiveText: t('common.yes'),
      negativeText: t('common.no'),
      maskClosable: false,
      onPositiveClick: () => {
        resolve(true);
        dialogReactive.destroy();
      },
      onNegativeClick: () => {
        resolve(false);
        dialogReactive.destroy();
      },
      onClose: () => {
        resolve(false);
      },
    });
  });
}

async function loadWorkflowList(preferredId?: string | null): Promise<void> {
  loading.value = true;
  loadErrorMessage.value = '';

  try {
    const response = await listWorkflowProfiles();
    workflows.value = response;

    const nextId = preferredId ?? selectedWorkflowId.value ?? response[0]?.id ?? null;
    const matched = nextId ? response.find((item) => item.id === nextId) ?? null : null;

    if (matched) {
      await openDraftProfile(matched);
      return;
    }

    await openBlankDraft();
  } catch (error) {
    loadErrorMessage.value = extractApiErrorMessage(error);
    workflows.value = [];
    await openBlankDraft();
  } finally {
    loading.value = false;
  }
}

async function handleRefresh(): Promise<void> {
  if (!(await confirmDiscardChanges())) {
    return;
  }

  await loadWorkflowList(selectedWorkflowId.value);
}

async function handleCreateDraft(): Promise<void> {
  if (!(await confirmDiscardChanges())) {
    return;
  }

  await openBlankDraft();
}

async function handleWorkflowSelection(value: string | null): Promise<void> {
  if (value === selectedWorkflowId.value) {
    return;
  }

  if (!(await confirmDiscardChanges())) {
    return;
  }

  const profile = workflows.value.find((item) => item.id === value);
  if (!profile) {
    await openBlankDraft();
    return;
  }

  await openDraftProfile(profile);
}

function buildSkillNode(skillId: string, position: XYPosition): WorkflowNode {
  return {
    id: createElementId('node'),
    type: 'skill',
    position: {
      x: position.x - 132,
      y: position.y - 44,
    },
    sourcePosition: Position.Right,
    targetPosition: Position.Left,
    data: buildWorkflowNodeData(skillId, createDefaultSkillParameters(skillId)),
  };
}

function handleSkillDragStart(event: DragEvent, skillId: string): void {
  if (!canManageWorkflows.value) {
    event.preventDefault();
    return;
  }

  dragSkillId.value = skillId;

  if (event.dataTransfer) {
    event.dataTransfer.effectAllowed = 'copy';
    event.dataTransfer.setData(WORKFLOW_DRAG_MIME, skillId);
  }
}

function handleSkillDragEnd(): void {
  dragSkillId.value = null;
  canvasDragActive.value = false;
}

function handleCanvasDragOver(event: DragEvent): void {
  if (!canManageWorkflows.value) {
    return;
  }

  event.preventDefault();
  canvasDragActive.value = true;

  if (event.dataTransfer) {
    event.dataTransfer.dropEffect = 'copy';
  }
}

function handleCanvasDragLeave(event: DragEvent): void {
  if (event.currentTarget === event.target) {
    canvasDragActive.value = false;
  }
}

function handleCanvasDrop(event: DragEvent): void {
  if (!canManageWorkflows.value) {
    return;
  }

  event.preventDefault();
  canvasDragActive.value = false;

  const skillId = event.dataTransfer?.getData(WORKFLOW_DRAG_MIME)?.trim() || dragSkillId.value;
  dragSkillId.value = null;
  if (!skillId) {
    return;
  }

  const flowPosition = screenToFlowCoordinate({
    x: event.clientX,
    y: event.clientY,
  });

  addNodes(buildSkillNode(skillId, flowPosition));
}

function handleNodeClick(event: NodeMouseEvent): void {
  const clickedNode = findNodeById(event.node.id);
  if (!clickedNode) {
    return;
  }

  selectedNodeId.value = clickedNode.id;
  syncNodeData(clickedNode);
  isNodeDrawerOpen.value = true;
}

function handlePaneClick(): void {
  isNodeDrawerOpen.value = false;
  selectedNodeId.value = null;
}

function handleDrawerVisibilityChange(show: boolean): void {
  isNodeDrawerOpen.value = show;

  if (!show) {
    selectedNodeId.value = null;
  }
}

function handleConnect(connection: Connection): void {
  addEdges({
    ...connection,
    id: createElementId('edge'),
    type: 'smoothstep',
    animated: true,
    markerEnd: MarkerType.ArrowClosed,
  });
}

function handleWatermarkOpacityChange(value: number | null): void {
  updateSelectedNodeParameters({
    Opacity: clampOpacity(typeof value === 'number' ? value : 0.5),
  });
}

function handleWatermarkFontSizeChange(value: number | null): void {
  updateSelectedNodeParameters({
    FontSize: clampFontSize(typeof value === 'number' ? value : 36),
  });
}

async function handleSave(): Promise<void> {
  if (!canManageWorkflows.value) {
    message.warning(t('auth.permissionDenied'));
    return;
  }

  saving.value = true;
  const isCreate = !draft.id;

  try {
    const saved = isCreate
      ? await createWorkflowProfile({
        name: draft.name.trim(),
        description: draft.description.trim() || null,
        isAutoRun: draft.isAutoRun,
        graphJson: buildGraphJson(),
      })
      : await updateWorkflowProfile(draft.id!, {
        name: draft.name.trim(),
        description: draft.description.trim() || null,
        isAutoRun: draft.isAutoRun,
        graphJson: buildGraphJson(),
      });

    message.success(t(isCreate ? 'workflows.messages.createSuccess' : 'workflows.messages.updateSuccess'));
    await loadWorkflowList(saved.id);
  } catch (error) {
    message.error(`${t('workflows.messages.saveFailed')}: ${extractApiErrorMessage(error)}`);
  } finally {
    saving.value = false;
  }
}

async function handleDelete(): Promise<void> {
  if (!canManageWorkflows.value) {
    message.warning(t('auth.permissionDenied'));
    return;
  }

  if (!draft.id) {
    return;
  }

  deleting.value = true;

  try {
    await deleteWorkflowProfile(draft.id);
    message.success(t('workflows.messages.deleteSuccess'));
    await loadWorkflowList(null);
  } catch (error) {
    message.error(`${t('workflows.messages.deleteFailed')}: ${extractApiErrorMessage(error)}`);
  } finally {
    deleting.value = false;
  }
}

async function handleSetAutoRun(): Promise<void> {
  if (!canManageWorkflows.value) {
    message.warning(t('auth.permissionDenied'));
    return;
  }

  if (!draft.id) {
    return;
  }

  settingAutoRun.value = true;

  try {
    const updated = await setWorkflowAutoRun(draft.id);
    message.success(t('workflows.messages.setAutoRunSuccess'));
    await loadWorkflowList(updated.id);
  } catch (error) {
    message.error(`${t('workflows.messages.setAutoRunFailed')}: ${extractApiErrorMessage(error)}`);
  } finally {
    settingAutoRun.value = false;
  }
}

watch(
  () => [draft.name, draft.description, draft.isAutoRun],
  () => {
    if (!hydratingEditor.value) {
      isDirty.value = true;
    }
  },
);

watch(
  nodes,
  () => {
    if (!hydratingEditor.value) {
      isDirty.value = true;
    }
  },
  { deep: true },
);

watch(
  edges,
  () => {
    if (!hydratingEditor.value) {
      isDirty.value = true;
    }
  },
  { deep: true },
);

watch(
  selectedNode,
  (node) => {
    if (node) {
      // 右侧抽屉依赖的是规范化后的节点参数，因此每次选中时都先同步一次 node.data。
      syncNodeData(node);
      return;
    }

    if (selectedNodeId.value) {
      selectedNodeId.value = null;
    }

    isNodeDrawerOpen.value = false;
  },
);

onMounted(() => {
  void loadWorkflowList();
});
</script>

<template>
  <div class="workflow-editor-page">
    <page-header :title="t('workflows.title')" :description="t('workflows.description')">
      <template #actions>
        <n-space wrap>
          <n-select
            :value="selectedWorkflowId"
            class="workflow-selector"
            clearable
            :options="workflowOptions"
            :placeholder="t('workflows.selectPlaceholder')"
            :disabled="loading"
            @update:value="handleWorkflowSelection"
          />
          <n-button :disabled="!canManageWorkflows" @click="handleCreateDraft">
            {{ t('workflows.actions.newWorkflow') }}
          </n-button>
          <n-button :loading="loading" @click="handleRefresh">
            {{ t('common.refresh') }}
          </n-button>
          <n-popconfirm
            v-if="draft.id"
            :negative-text="t('common.cancel')"
            :positive-text="t('common.delete')"
            @positive-click="handleDelete"
          >
            <template #trigger>
              <n-button :disabled="!canManageWorkflows" :loading="deleting" type="error" ghost>
                {{ t('common.delete') }}
              </n-button>
            </template>
            {{ t('workflows.confirmDelete', { name: draft.name }) }}
          </n-popconfirm>
          <n-button
            type="primary"
            :disabled="!canSave"
            :loading="saving"
            @click="handleSave"
          >
            {{ t('common.save') }}
          </n-button>
        </n-space>
      </template>
    </page-header>

    <n-space vertical :size="16">
      <n-alert v-if="!canManageWorkflows" type="info">
        {{ t('workflows.readOnlyNotice') }}
      </n-alert>

      <n-alert v-if="loadErrorMessage" type="warning">
        {{ t('workflows.messages.loadFailed') }}: {{ loadErrorMessage }}
      </n-alert>

      <n-alert v-if="parseWarningMessage" type="warning" :show-icon="false">
        {{ parseWarningMessage }}
      </n-alert>

      <div class="workflow-editor-layout">
        <aside class="workflow-sidebar">
          <n-card :title="t('workflows.profileTitle')" class="workflow-panel">
            <template #header-extra>
              <n-space align="center" :size="8">
                <n-tag size="small" round :bordered="false" :type="draft.id ? 'info' : 'warning'">
                  {{ draft.id ? t('workflows.status.saved') : t('workflows.status.draft') }}
                </n-tag>
                <n-tag
                  v-if="draft.isAutoRun"
                  size="small"
                  round
                  :bordered="false"
                  type="success"
                >
                  {{ t('workflows.status.autoRun') }}
                </n-tag>
              </n-space>
            </template>

            <n-space vertical :size="14">
              <n-form label-placement="top">
                <n-form-item :label="t('workflows.fields.name')">
                  <n-input
                    v-model:value="draft.name"
                    maxlength="100"
                    show-count
                    :disabled="!canManageWorkflows"
                    :placeholder="t('workflows.placeholders.name')"
                  />
                </n-form-item>

                <n-form-item :label="t('workflows.fields.description')">
                  <n-input
                    v-model:value="draft.description"
                    type="textarea"
                    maxlength="500"
                    show-count
                    :autosize="{ minRows: 3, maxRows: 6 }"
                    :disabled="!canManageWorkflows"
                    :placeholder="t('workflows.placeholders.description')"
                  />
                </n-form-item>
              </n-form>

              <div class="workflow-toggle-row">
                <div class="workflow-toggle-copy">
                  <div class="workflow-toggle-copy__title">{{ t('workflows.fields.autoRun') }}</div>
                  <div class="workflow-toggle-copy__hint">{{ t('workflows.autoRunHint') }}</div>
                </div>

                <n-switch v-model:value="draft.isAutoRun" :disabled="!canManageWorkflows" />
              </div>

              <n-button
                secondary
                type="success"
                :loading="settingAutoRun"
                :disabled="!canQuickSetAutoRun"
                @click="handleSetAutoRun"
              >
                {{ t('workflows.actions.setAutoRun') }}
              </n-button>

              <div class="workflow-hint">
                {{ t('workflows.executionHint') }}
              </div>
            </n-space>
          </n-card>

          <n-card :title="t('workflows.paletteTitle')" class="workflow-panel">
            <div class="workflow-hint">
              {{ t('workflows.paletteHint') }}
            </div>

            <div class="skill-palette">
              <button
                v-for="skill in WORKFLOW_SKILL_CATALOG"
                :key="skill.skillId"
                type="button"
                class="skill-palette__item"
                :class="{ 'skill-palette__item--disabled': !skill.enabled || !canManageWorkflows }"
                :draggable="skill.enabled && canManageWorkflows"
                :style="{ '--skill-accent': skill.accentColor }"
                @dragstart="handleSkillDragStart($event, skill.skillId)"
                @dragend="handleSkillDragEnd"
              >
                <div class="skill-palette__icon">{{ skill.shortCode }}</div>
                <div class="skill-palette__content">
                  <div class="skill-palette__title-row">
                    <div class="skill-palette__title">{{ t(skill.labelKey) }}</div>
                    <n-tag
                      size="small"
                      round
                      :bordered="false"
                      :type="skill.enabled ? 'success' : 'warning'"
                    >
                      {{
                        skill.enabled
                          ? t('workflows.skillAvailability.ready')
                          : t('workflows.skillAvailability.comingSoon')
                      }}
                    </n-tag>
                  </div>

                  <div class="skill-palette__description">{{ t(skill.descriptionKey) }}</div>
                </div>
              </button>
            </div>
          </n-card>
        </aside>

        <section class="workflow-canvas-panel">
          <n-card :title="t('workflows.canvasTitle')" class="workflow-panel workflow-panel--canvas">
            <template #header-extra>
              <n-space align="center" :size="8">
                <n-tag
                  v-if="isDirty"
                  size="small"
                  round
                  :bordered="false"
                  type="warning"
                >
                  {{ t('workflows.status.unsaved') }}
                </n-tag>
                <span class="workflow-canvas-hint">{{ t('workflows.canvasHint') }}</span>
              </n-space>
            </template>

            <div
              class="workflow-canvas-shell"
              :class="{ 'workflow-canvas-shell--dragging': canvasDragActive }"
              @drop="handleCanvasDrop"
              @dragover="handleCanvasDragOver"
              @dragleave="handleCanvasDragLeave"
            >
              <VueFlow
                :id="FLOW_ID"
                v-model:nodes="nodes"
                v-model:edges="edges"
                class="workflow-flow"
                :default-edge-options="defaultEdgeOptions"
                :delete-key-code="canManageWorkflows ? 'Delete' : null"
                :elements-selectable="true"
                :fit-view-on-init="false"
                :nodes-connectable="canManageWorkflows"
                :nodes-draggable="canManageWorkflows"
                :snap-to-grid="true"
                :snap-grid="[24, 24]"
                @connect="handleConnect"
                @node-click="handleNodeClick"
                @pane-click="handlePaneClick"
              >
                <Background variant="dots" :gap="22" :size="1.4" color="#cbd5e1" />
                <Controls />

                <template #node-skill="nodeProps">
                  <SkillNode v-bind="nodeProps" :read-only="!canManageWorkflows" />
                </template>
              </VueFlow>

              <div v-if="nodes.length === 0" class="workflow-canvas-placeholder">
                <n-empty :description="t('workflows.emptyCanvasDescription')">
                  <template #extra>
                    <div class="workflow-hint">
                      {{ t('workflows.emptyCanvasHint') }}
                    </div>
                  </template>
                </n-empty>
              </div>
            </div>
          </n-card>
        </section>
      </div>
    </n-space>

    <n-drawer
      :show="isNodeDrawerOpen"
      placement="right"
      :width="380"
      resizable
      @update:show="handleDrawerVisibilityChange"
    >
      <n-drawer-content
        closable
        :title="t('workflows.drawer.title', { name: selectedNodeTitle })"
      >
        <n-space v-if="selectedNode" vertical :size="16">
          <div class="workflow-node-drawer__hero">
            <div class="workflow-node-drawer__title">{{ selectedNodeTitle }}</div>
            <div class="workflow-node-drawer__description">
              {{ selectedNodeDescription }}
            </div>
            <div class="workflow-node-drawer__meta">
              <span class="workflow-node-drawer__meta-label">{{ t('workflows.drawer.skillIdLabel') }}</span>
              <code class="workflow-node-drawer__meta-value">{{ selectedNodeSkillId }}</code>
            </div>
          </div>

          <n-form label-placement="top">
            <template v-if="selectedNodeSkillId === FORMAT_CONVERT_SKILL_ID">
              <n-form-item :label="t('workflows.drawer.fields.targetFormat')">
                <n-select
                  v-model:value="formatConvertTargetFormat"
                  :options="targetFormatOptions"
                  :disabled="!canManageWorkflows"
                />
              </n-form-item>

              <n-form-item :label="t('workflows.drawer.fields.keepOriginal')">
                <div class="workflow-node-drawer__switch-row">
                  <div class="workflow-node-drawer__field-copy">
                    <div class="workflow-node-drawer__field-title">
                      {{ t('workflows.drawer.fields.keepOriginal') }}
                    </div>
                    <div class="workflow-node-drawer__field-hint">
                      {{ t('workflows.drawer.hints.keepOriginal') }}
                    </div>
                  </div>

                  <n-switch
                    v-model:value="formatConvertKeepOriginal"
                    :disabled="!canManageWorkflows"
                  />
                </div>
              </n-form-item>
            </template>

            <template v-else-if="selectedNodeSkillId === WATERMARK_SKILL_ID">
              <n-form-item :label="t('workflows.drawer.fields.text')">
                <n-input
                  v-model:value="watermarkText"
                  :disabled="!canManageWorkflows"
                  :placeholder="t('workflows.drawer.placeholders.watermarkText')"
                />
              </n-form-item>

              <n-form-item :label="t('workflows.drawer.fields.opacity')">
                <div class="workflow-node-drawer__range-row">
                  <n-slider
                    :value="watermarkOpacity"
                    :step="0.05"
                    :min="0"
                    :max="1"
                    :disabled="!canManageWorkflows"
                    @update:value="handleWatermarkOpacityChange"
                  />
                  <n-input-number
                    :value="watermarkOpacity"
                    :step="0.05"
                    :min="0"
                    :max="1"
                    :precision="2"
                    :disabled="!canManageWorkflows"
                    @update:value="handleWatermarkOpacityChange"
                  />
                </div>
              </n-form-item>

              <n-form-item :label="t('workflows.drawer.fields.fontSize')">
                <n-input-number
                  :value="watermarkFontSize"
                  :step="1"
                  :min="8"
                  :max="512"
                  :disabled="!canManageWorkflows"
                  @update:value="handleWatermarkFontSizeChange"
                />
              </n-form-item>

              <n-form-item :label="t('workflows.drawer.fields.position')">
                <n-select
                  v-model:value="watermarkPosition"
                  :options="watermarkPositionOptions"
                  :disabled="!canManageWorkflows"
                />
              </n-form-item>
            </template>

            <n-alert v-else type="info" :show-icon="false">
              {{ t('workflows.drawer.noConfig') }}
            </n-alert>
          </n-form>
        </n-space>

        <n-empty v-else :description="t('workflows.drawer.empty')" />
      </n-drawer-content>
    </n-drawer>
  </div>
</template>

<style scoped>
.workflow-editor-layout {
  display: grid;
  grid-template-columns: 320px minmax(0, 1fr);
  gap: 16px;
  align-items: start;
}

.workflow-sidebar {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.workflow-panel {
  border-radius: 14px;
  border: 1px solid var(--app-border);
  background: linear-gradient(180deg, var(--app-surface-strong), var(--app-surface));
  box-shadow: var(--app-shadow-soft);
}

.workflow-panel :deep(.n-card-header) {
  gap: 10px;
  flex-wrap: wrap;
}

.workflow-panel :deep(.n-card__content) {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.workflow-panel--canvas :deep(.n-card__content) {
  padding-top: 12px;
}

.workflow-selector {
  min-width: 260px;
}

.workflow-toggle-row {
  display: flex;
  gap: 16px;
  justify-content: space-between;
  align-items: flex-start;
}

.workflow-toggle-copy {
  min-width: 0;
}

.workflow-toggle-copy__title {
  font-size: 14px;
  font-weight: 700;
  color: #0f172a;
}

.workflow-toggle-copy__hint,
.workflow-hint,
.workflow-canvas-hint {
  font-size: 12px;
  line-height: 1.6;
  color: #64748b;
}

.skill-palette {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.skill-palette__item {
  width: 100%;
  border: 1px solid color-mix(in srgb, var(--skill-accent) 18%, #dbe4f0);
  border-radius: 14px;
  background:
    radial-gradient(circle at top right, color-mix(in srgb, var(--skill-accent) 10%, transparent), transparent 58%),
    linear-gradient(180deg, rgba(255, 255, 255, 0.98), rgba(248, 250, 252, 0.96));
  padding: 14px;
  display: flex;
  gap: 12px;
  align-items: flex-start;
  text-align: left;
  cursor: grab;
  transition: transform 0.2s ease, box-shadow 0.2s ease, border-color 0.2s ease;
}

.skill-palette__item:hover {
  transform: translateY(-1px);
  box-shadow: 0 10px 24px rgba(15, 23, 42, 0.07);
}

.skill-palette__item:active {
  cursor: grabbing;
}

.skill-palette__item--disabled {
  opacity: 0.72;
  cursor: not-allowed;
  box-shadow: none;
}

.skill-palette__icon {
  width: 42px;
  height: 42px;
  border-radius: 14px;
  background: color-mix(in srgb, var(--skill-accent) 15%, #eff6ff);
  color: var(--skill-accent);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 13px;
  font-weight: 700;
  letter-spacing: 0.08em;
  flex-shrink: 0;
}

.skill-palette__content {
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.skill-palette__title-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}

.skill-palette__title {
  font-size: 14px;
  font-weight: 700;
  color: #0f172a;
}

.skill-palette__description {
  font-size: 12px;
  line-height: 1.6;
  color: #475569;
}

.workflow-canvas-panel {
  min-width: 0;
}

.workflow-canvas-shell {
  position: relative;
  min-height: 700px;
  border-radius: 16px;
  overflow: hidden;
  background:
    radial-gradient(circle at top, rgba(59, 130, 246, 0.05), transparent 32%),
    linear-gradient(180deg, rgba(248, 250, 252, 0.94), rgba(244, 247, 251, 0.98));
  border: 1px solid var(--app-border);
}

.workflow-canvas-shell--dragging {
  border-color: #3b82f6;
  box-shadow: inset 0 0 0 1px rgba(59, 130, 246, 0.25);
}

.workflow-flow {
  min-height: 700px;
  background: transparent;
}

.workflow-flow :deep(.vue-flow__pane) {
  cursor: default;
}

.workflow-flow :deep(.vue-flow__edge-path) {
  stroke: #3b82f6;
  stroke-width: 2;
}

.workflow-flow :deep(.vue-flow__edge.selected .vue-flow__edge-path) {
  stroke: #0f172a;
}

.workflow-flow :deep(.vue-flow__controls) {
  box-shadow: 0 10px 22px rgba(15, 23, 42, 0.1);
  border-radius: 12px;
  overflow: hidden;
}

.workflow-canvas-placeholder {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  pointer-events: none;
  padding: 24px;
}

.workflow-node-drawer__hero {
  padding: 16px;
  border-radius: 14px;
  background:
    radial-gradient(circle at top right, rgba(59, 130, 246, 0.1), transparent 52%),
    linear-gradient(180deg, rgba(248, 251, 255, 0.96), rgba(245, 247, 251, 0.96));
  border: 1px solid rgba(191, 219, 254, 0.72);
}

.workflow-node-drawer__title {
  font-size: 18px;
  font-weight: 700;
  color: #0f172a;
}

.workflow-node-drawer__description {
  margin-top: 8px;
  font-size: 13px;
  line-height: 1.7;
  color: #475569;
}

.workflow-node-drawer__meta {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  margin-top: 12px;
  font-size: 12px;
  color: #64748b;
}

.workflow-node-drawer__meta-label {
  font-weight: 600;
}

.workflow-node-drawer__meta-value {
  padding: 3px 8px;
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.9);
  border: 1px solid rgba(191, 219, 254, 0.9);
  color: #334155;
  font-size: 12px;
}

.workflow-node-drawer__switch-row {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
}

.workflow-node-drawer__field-copy {
  min-width: 0;
}

.workflow-node-drawer__field-title {
  font-size: 13px;
  font-weight: 700;
  color: #0f172a;
}

.workflow-node-drawer__field-hint {
  margin-top: 4px;
  font-size: 12px;
  line-height: 1.6;
  color: #64748b;
}

.workflow-node-drawer__range-row {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 120px;
  gap: 12px;
  align-items: center;
}

@media (max-width: 1080px) {
  .workflow-editor-layout {
    grid-template-columns: 1fr;
  }

  .workflow-canvas-shell,
  .workflow-flow {
    min-height: 620px;
  }
}

@media (max-width: 768px) {
  .workflow-selector {
    width: 100%;
    min-width: 0;
  }

  .workflow-toggle-row,
  .workflow-node-drawer__switch-row {
    flex-direction: column;
  }

  .workflow-node-drawer__range-row {
    grid-template-columns: 1fr;
  }

  .workflow-panel :deep(.n-card__content) {
    padding-left: 14px;
    padding-right: 14px;
  }

  .workflow-canvas-shell,
  .workflow-flow {
    min-height: 520px;
    border-radius: 14px;
  }
}
</style>
