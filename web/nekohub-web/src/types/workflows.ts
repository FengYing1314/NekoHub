import type { Edge, Node } from '@vue-flow/core';
import type { JsonObject } from './api';

export interface WorkflowProfileResponse {
  id: string;
  name: string;
  description: string | null;
  isAutoRun: boolean;
  graphJson: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CreateWorkflowProfileRequest {
  name: string;
  description?: string | null;
  isAutoRun?: boolean;
  graphJson: string;
}

export type UpdateWorkflowProfileRequest = CreateWorkflowProfileRequest;

export interface RunAssetWorkflowResponse {
  assetId: string;
  workflowId: string;
  skillIds: string[];
}

export type WorkflowNodeType = 'skill';

export const WORKFLOW_FORMAT_CONVERT_TARGET_FORMATS = [
  'webp',
  'jpeg',
  'png',
  'gif',
  'bmp',
  'tga',
  'tiff',
] as const;

export type WorkflowFormatConvertTargetFormat = (typeof WORKFLOW_FORMAT_CONVERT_TARGET_FORMATS)[number];

export const WORKFLOW_WATERMARK_POSITIONS = [
  'BottomRight',
  'BottomLeft',
  'Center',
  'TopRight',
  'TopLeft',
] as const;

export type WorkflowWatermarkPosition = (typeof WORKFLOW_WATERMARK_POSITIONS)[number];

export interface WorkflowSkillNodeData {
  skillId: string;
  parameters?: JsonObject;
}

export interface WorkflowFormatConvertParameters extends JsonObject {
  TargetFormat: WorkflowFormatConvertTargetFormat;
  KeepOriginal: boolean;
}

export interface WorkflowWatermarkParameters extends JsonObject {
  Text: string;
  Opacity: number;
  FontSize: number;
  Position: WorkflowWatermarkPosition;
}

export type WorkflowNode = Node<WorkflowSkillNodeData>;
export type WorkflowEdge = Edge;

export interface WorkflowViewport {
  x: number;
  y: number;
  zoom: number;
}

export interface WorkflowGraphDocument {
  nodes: WorkflowNode[];
  edges: WorkflowEdge[];
  viewport?: WorkflowViewport;
}
