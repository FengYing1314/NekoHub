import type { JsonObject } from './api';

export type AssetStatus = 'pending' | 'ready' | 'deleted' | 'failed';
export type AssetDisplayStatus = AssetStatus | 'unknown';
export type AssetStatusLike = AssetStatus | string | number | null | undefined;

const ASSET_STATUS_NUMBER_MAP: Record<number, AssetStatus> = {
  1: 'pending',
  2: 'ready',
  3: 'deleted',
  4: 'failed',
};

const ASSET_STATUS_SET = new Set<AssetStatus>(['pending', 'ready', 'deleted', 'failed']);

export function normalizeAssetStatus(status: AssetStatusLike): AssetDisplayStatus {
  if (typeof status === 'number') {
    return ASSET_STATUS_NUMBER_MAP[status] ?? 'unknown';
  }

  if (typeof status !== 'string') {
    return 'unknown';
  }

  const normalized = status.trim().toLowerCase();
  if (normalized === 'processing') {
    return 'pending';
  }

  return ASSET_STATUS_SET.has(normalized as AssetStatus)
    ? (normalized as AssetStatus)
    : 'unknown';
}

export interface AssetListItemResponse {
  id: string;
  type: string;
  status: AssetStatus;
  isPublic: boolean;
  originalFileName: string | null;
  contentType: string;
  size: number;
  width: number | null;
  height: number | null;
  storageProvider: string;
  publicUrl: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface AssetPagedResponse {
  items: AssetListItemResponse[];
  page: number;
  pageSize: number;
  total: number;
}

export interface AssetDerivativeSummaryResponse {
  kind: string;
  contentType: string;
  extension: string;
  size: number;
  width: number | null;
  height: number | null;
  publicUrl: string | null;
  createdAtUtc: string;
}

export interface AssetStructuredResultSummaryResponse {
  kind: string;
  payloadJson: string;
  createdAtUtc: string;
}

export interface BasicCaptionStructuredResultPayload {
  caption?: string;
  generator?: string;
}

export interface AssetLatestExecutionStepSummaryResponse {
  stepName: string;
  succeeded: boolean;
  errorMessage: string | null;
  startedAtUtc: string;
  completedAtUtc: string;
}

export interface AssetLatestExecutionSummaryResponse {
  executionId: string;
  skillName: string;
  triggerSource: string;
  startedAtUtc: string;
  completedAtUtc: string;
  succeeded: boolean;
  steps: AssetLatestExecutionStepSummaryResponse[];
}

export interface AssetResponse {
  id: string;
  type: string;
  status: AssetStatus;
  isPublic: boolean;
  originalFileName: string | null;
  storedFileName: string | null;
  contentType: string;
  extension: string;
  size: number;
  width: number | null;
  height: number | null;
  checksumSha256: string | null;
  storageProvider: string;
  storageKey: string;
  publicUrl: string | null;
  description: string | null;
  altText: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  derivatives: AssetDerivativeSummaryResponse[];
  structuredResults: AssetStructuredResultSummaryResponse[];
  latestExecutionSummary: AssetLatestExecutionSummaryResponse | null;
}

export interface DeleteAssetResponse {
  id: string;
  status: string;
  deletedAtUtc: string;
}

export type AssetListOrderBy = 'createdAt' | 'size';
export type AssetListOrderDirection = 'asc' | 'desc';

export type AssetPatchFieldValue = string | null | undefined;

export interface AssetListQueryParams {
  page: number;
  pageSize: number;
  query?: string;
  contentType?: string;
  status?: AssetStatus;
  orderBy?: AssetListOrderBy;
  orderDirection?: AssetListOrderDirection;
}

export type ListAssetsInput = AssetListQueryParams;

export interface PatchAssetInput {
  description?: AssetPatchFieldValue;
  altText?: AssetPatchFieldValue;
  originalFileName?: AssetPatchFieldValue;
  isPublic?: boolean;
}

export interface UploadAssetInput {
  file: File;
  description?: string;
  altText?: string;
  isPublic?: boolean;
}

export type BatchDeleteAssetsInput = string[];

export interface BatchDeleteAssetsResponse {
  requestedCount: number;
  deletedCount: number;
  notFoundIds: string[];
}

export interface AssetContentTypeBreakdownResponse {
  contentType: string;
  count: number;
  totalBytes: number;
}

export interface AssetSkillUsageSummaryResponse {
  skillName: string;
  runCount: number;
}

export interface AssetSkillSummaryResponse {
  skillName: string;
  description: string;
  steps: string[];
}

export interface ListSkillsResponse {
  skills: AssetSkillSummaryResponse[];
}

export interface AssetUsageStatsResponse {
  totalAssets: number;
  totalBytes: number;
  totalDerivatives: number;
  contentTypeBreakdown: AssetContentTypeBreakdownResponse[];
  mostActiveSkill: AssetSkillUsageSummaryResponse | null;
}

export interface RunAssetSkillStepResponse {
  name: string;
  succeeded: boolean;
  errorMessage: string | null;
}

export interface RunAssetSkillAssetResponse {
  id: string;
  type: string;
  status: AssetStatus;
  isPublic: boolean;
  originalFileName: string | null;
  contentType: string;
  extension: string;
  size: number;
  width: number | null;
  height: number | null;
  checksumSha256: string | null;
  publicUrl: string | null;
  description: string | null;
  altText: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  derivatives: AssetDerivativeSummaryResponse[];
  structuredResults: AssetStructuredResultSummaryResponse[];
  latestExecutionSummary: AssetLatestExecutionSummaryResponse | null;
}

export interface RunAssetSkillResponse {
  succeeded: boolean;
  skillName: string;
  steps: RunAssetSkillStepResponse[];
  asset: RunAssetSkillAssetResponse;
}

export interface RunAssetSkillInput {
  assetId: string;
  skillName: string;
  parameters?: JsonObject;
}

export interface ParsedStructuredResult {
  kind: string;
  createdAtUtc: string;
  rawPayloadJson: string;
  parsedPayload: unknown | null;
}
