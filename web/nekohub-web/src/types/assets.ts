export interface AssetListItemResponse {
  id: string;
  type: string;
  status: string;
  originalFileName: string;
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
  status: string;
  originalFileName: string;
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

export type AssetListSortBy = 'createdAt' | 'size';
export type AssetListSortDirection = 'asc' | 'desc';

export interface AssetListQueryParams {
  page: number;
  pageSize: number;
  keyword?: string;
  contentType?: string;
  sortBy?: AssetListSortBy;
  sortDirection?: AssetListSortDirection;
}

export type ListAssetsInput = AssetListQueryParams;

export interface UploadAssetInput {
  file: File;
  description?: string;
  altText?: string;
}

export interface ParsedStructuredResult {
  kind: string;
  createdAtUtc: string;
  rawPayloadJson: string;
  parsedPayload: unknown | null;
}
