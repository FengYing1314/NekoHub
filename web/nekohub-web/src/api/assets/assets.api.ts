import { httpClient } from '../client/http-client';
import { unwrapAxiosApiResponse } from '../client/response';
import type { JsonObject } from '../../types/api';
import type {
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

const ASSETS_BASE_PATH = '/api/v1/assets';
const MCP_BASE_PATH = '/mcp';

interface RawAssetPagedResponse {
  items?: AssetListItemResponse[];
  page?: number;
  pageSize?: number;
  total?: number;
}

interface McpToolCallSuccess<T> {
  jsonrpc?: string;
  id?: unknown;
  result?: {
    structuredContent?: T | McpToolErrorPayload;
    isError?: boolean;
  };
}

interface McpToolCallFailure {
  jsonrpc?: string;
  id?: unknown;
  error?: {
    code?: number;
    message?: string;
    data?: unknown;
  };
}

interface McpToolErrorPayload {
  error?: {
    code?: string;
    message?: string;
  };
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

function extractMcpToolErrorMessage(payload: unknown): string | null {
  if (!payload || typeof payload !== 'object') {
    return null;
  }

  const structuredError = (payload as McpToolErrorPayload).error;
  return typeof structuredError?.message === 'string' ? structuredError.message : null;
}

async function callMcpTool<T>(name: string, argumentsPayload: Record<string, unknown>): Promise<T> {
  const response = await httpClient.post<McpToolCallSuccess<T> | McpToolCallFailure>(MCP_BASE_PATH, {
    jsonrpc: '2.0',
    id: `web-${Date.now()}`,
    method: 'tools/call',
    params: {
      name,
      arguments: argumentsPayload,
    },
  });

  const payload = response.data;
  if ('error' in payload && payload.error) {
    throw new Error(payload.error.message ?? `MCP tool '${name}' failed.`);
  }

  if (!('result' in payload) || !payload.result) {
    throw new Error(`MCP tool '${name}' returned an empty result.`);
  }

  const toolResult = payload.result;
  if (!toolResult) {
    throw new Error(`MCP tool '${name}' returned an empty result.`);
  }

  const toolErrorMessage = extractMcpToolErrorMessage(toolResult.structuredContent);
  if (toolResult.isError || toolErrorMessage) {
    throw new Error(toolErrorMessage ?? `MCP tool '${name}' returned an error.`);
  }

  if (toolResult.structuredContent === undefined) {
    throw new Error(`MCP tool '${name}' returned no structured content.`);
  }

  return toolResult.structuredContent as T;
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

  const response = await httpClient.post(ASSETS_BASE_PATH, formData, {
    headers: {
      'Content-Type': 'multipart/form-data',
    },
  });

  return unwrapAxiosApiResponse<AssetResponse>(response);
}

export async function deleteAsset(id: string): Promise<DeleteAssetResponse> {
  const response = await httpClient.delete(`${ASSETS_BASE_PATH}/${id}`);
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

export async function listSkills(): Promise<AssetSkillSummaryResponse[]> {
  const result = await callMcpTool<ListSkillsResponse>('list_skills', {});
  return Array.isArray(result.skills) ? result.skills : [];
}

export async function runAssetSkill(
  assetId: string,
  skillName: string,
  parameters?: JsonObject,
): Promise<RunAssetSkillResponse> {
  const argumentsPayload: Record<string, unknown> = {
    assetId,
    skillName,
  };

  if (parameters !== undefined) {
    argumentsPayload.parameters = parameters;
  }

  return callMcpTool<RunAssetSkillResponse>('run_asset_skill', argumentsPayload);
}

export const getAssetById = getAsset;
export const deleteAssetById = deleteAsset;

export function buildAssetContentUrl(apiBaseUrl: string, id: string): string {
  const normalizedBaseUrl = apiBaseUrl.trim().replace(/\/+$/, '');
  return `${normalizedBaseUrl}${ASSETS_BASE_PATH}/${id}/content`;
}
