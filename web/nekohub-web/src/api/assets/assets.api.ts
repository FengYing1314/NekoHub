import { httpClient } from '../client/http-client';
import { unwrapAxiosApiResponse } from '../client/response';
import type { JsonObject } from '../../types/api';
import type {
  DeleteAssetInput,
  AssetListItemResponse,
  AssetPagedResponse,
  AssetResponse,
  AssetSkillSummaryResponse,
  AssetUsageStatsResponse,
  BatchDeleteAssetsInput,
  BatchDeleteAssetsResponse,
  DeleteAssetResponse,
  ListSkillsResponse,
  ListAssetsInput,
  PatchAssetInput,
  RunAssetSkillResponse,
  UploadAssetInput,
} from '../../types/assets';
import type { RunAssetWorkflowResponse } from '../../types/workflows';

const ASSETS_BASE_PATH = '/api/v1/assets';

interface RawAssetPagedResponse {
  items?: AssetListItemResponse[];
  page?: number;
  pageSize?: number;
  total?: number;
}

function normalizeAssetPagedResponse(raw: RawAssetPagedResponse, fallback: ListAssetsInput): AssetPagedResponse {
  return {
    items: Array.isArray(raw.items) ? raw.items : [],
    page: typeof raw.page === 'number' ? raw.page : fallback.page,
    pageSize: typeof raw.pageSize === 'number' ? raw.pageSize : fallback.pageSize,
    total: typeof raw.total === 'number' ? raw.total : 0,
  };
}

function buildPatchAssetPayload(payload: PatchAssetInput): Record<string, string | null | boolean> {
  const requestBody: Record<string, string | null | boolean> = {};

  if (payload.description !== undefined) {
    requestBody.description = payload.description;
  }

  if (payload.altText !== undefined) {
    requestBody.altText = payload.altText;
  }

  if (payload.originalFileName !== undefined) {
    requestBody.originalFileName = payload.originalFileName;
  }

  if (payload.isPublic !== undefined) {
    requestBody.isPublic = payload.isPublic;
  }

  return requestBody;
}

export async function listAssets(input: ListAssetsInput): Promise<AssetPagedResponse> {
  const response = await httpClient.get(ASSETS_BASE_PATH, {
    params: {
      page: input.page,
      pageSize: input.pageSize,
      query: input.query,
      contentType: input.contentType,
      status: input.status,
      orderBy: input.orderBy,
      orderDirection: input.orderDirection,
    },
  });

  const rawPaged = unwrapAxiosApiResponse<RawAssetPagedResponse>(response);
  return normalizeAssetPagedResponse(rawPaged, input);
}

export async function getAsset(id: string): Promise<AssetResponse> {
  const response = await httpClient.get(`${ASSETS_BASE_PATH}/${id}`);
  return unwrapAxiosApiResponse<AssetResponse>(response);
}

export async function uploadAsset(input: UploadAssetInput): Promise<AssetResponse> {
  const formData = new FormData();
  formData.append('file', input.file);

  const description = input.description?.trim();
  if (description) {
    formData.append('description', description);
  }

  const altText = input.altText?.trim();
  if (altText) {
    formData.append('altText', altText);
  }

  if (input.isPublic !== undefined) {
    formData.append('isPublic', String(input.isPublic));
  }

  if (input.storageProviderProfileId) {
    formData.append('storageProviderProfileId', input.storageProviderProfileId);
  }

  if (input.runEnrichment !== undefined) {
    formData.append('runEnrichment', String(input.runEnrichment));
  }

  const commitMessage = input.commitMessage?.trim();
  if (commitMessage) {
    formData.append('commitMessage', commitMessage);
  }

  const response = await httpClient.post(ASSETS_BASE_PATH, formData, {
    headers: {
      'Content-Type': 'multipart/form-data',
    },
  });

  return unwrapAxiosApiResponse<AssetResponse>(response);
}

export async function deleteAsset(id: string, input?: DeleteAssetInput): Promise<DeleteAssetResponse> {
  const commitMessage = input?.commitMessage?.trim();
  const response = await httpClient.delete(`${ASSETS_BASE_PATH}/${id}`, {
    data: commitMessage
      ? {
        commitMessage,
      }
      : undefined,
  });

  return unwrapAxiosApiResponse<DeleteAssetResponse>(response);
}

export async function patchAsset(id: string, payload: PatchAssetInput): Promise<AssetResponse> {
  const response = await httpClient.patch(`${ASSETS_BASE_PATH}/${id}`, buildPatchAssetPayload(payload));
  return unwrapAxiosApiResponse<AssetResponse>(response);
}

export async function batchDeleteAssets(assetIds: BatchDeleteAssetsInput): Promise<BatchDeleteAssetsResponse> {
  const response = await httpClient.post(`${ASSETS_BASE_PATH}/batch-delete`, assetIds);
  return unwrapAxiosApiResponse<BatchDeleteAssetsResponse>(response);
}

export async function getUsageStats(): Promise<AssetUsageStatsResponse> {
  const response = await httpClient.get(`${ASSETS_BASE_PATH}/usage-stats`);
  return unwrapAxiosApiResponse<AssetUsageStatsResponse>(response);
}

export async function getAssetContentBlob(id: string): Promise<Blob> {
  const response = await httpClient.get(`${ASSETS_BASE_PATH}/${id}/content`, {
    responseType: 'blob',
  });
  return response.data as Blob;
}

export async function listSkills(): Promise<AssetSkillSummaryResponse[]> {
  const response = await httpClient.get(`${ASSETS_BASE_PATH}/skills`);
  const result = unwrapAxiosApiResponse<ListSkillsResponse | AssetSkillSummaryResponse[]>(response);
  if (Array.isArray(result)) {
    return result;
  }

  return Array.isArray(result.skills) ? result.skills : [];
}

export async function runAssetSkill(
  assetId: string,
  skillName: string,
  parameters?: JsonObject,
): Promise<RunAssetSkillResponse> {
  const response = await httpClient.post(`${ASSETS_BASE_PATH}/${assetId}/skills/${encodeURIComponent(skillName)}/run`, {
    parameters,
  });
  return unwrapAxiosApiResponse<RunAssetSkillResponse>(response);
}

export async function runAssetWorkflow(
  assetId: string,
  workflowId: string,
): Promise<RunAssetWorkflowResponse> {
  const response = await httpClient.post(`${ASSETS_BASE_PATH}/${assetId}/workflows/${workflowId}/run`, null);
  return unwrapAxiosApiResponse<RunAssetWorkflowResponse>(response);
}

export const getAssetById = getAsset;
export const deleteAssetById = deleteAsset;

export function buildAssetContentUrl(apiBaseUrl: string, id: string): string {
  const normalizedBaseUrl = apiBaseUrl.trim().replace(/\/+$/, '');
  return `${normalizedBaseUrl}${ASSETS_BASE_PATH}/${id}/content`;
}
