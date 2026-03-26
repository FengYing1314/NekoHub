import { httpClient } from '../client/http-client';
import { unwrapAxiosApiResponse } from '../client/response';
import type {
  AssetListItemResponse,
  AssetPagedResponse,
  AssetResponse,
  DeleteAssetResponse,
  ListAssetsInput,
  UploadAssetInput,
} from '../../types/assets';

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

export async function listAssets(input: ListAssetsInput): Promise<AssetPagedResponse> {
  const response = await httpClient.get(`/api/v1/assets`, {
    params: {
      page: input.page,
      pageSize: input.pageSize,
      keyword: input.keyword,
      contentType: input.contentType,
      sortBy: input.sortBy,
      sortDirection: input.sortDirection,
    },
  });

  const rawPaged = unwrapAxiosApiResponse<RawAssetPagedResponse>(response);
  return normalizeAssetPagedResponse(rawPaged, input);
}

export async function getAssetById(id: string): Promise<AssetResponse> {
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

  const response = await httpClient.post(ASSETS_BASE_PATH, formData, {
    headers: {
      'Content-Type': 'multipart/form-data',
    },
  });

  return unwrapAxiosApiResponse<AssetResponse>(response);
}

export async function deleteAssetById(id: string): Promise<DeleteAssetResponse> {
  const response = await httpClient.delete(`${ASSETS_BASE_PATH}/${id}`);
  return unwrapAxiosApiResponse<DeleteAssetResponse>(response);
}

export function buildAssetContentUrl(apiBaseUrl: string, id: string): string {
  const normalizedBaseUrl = apiBaseUrl.trim().replace(/\/+$/, '');
  return `${normalizedBaseUrl}${ASSETS_BASE_PATH}/${id}/content`;
}
