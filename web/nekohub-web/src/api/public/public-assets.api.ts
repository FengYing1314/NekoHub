import type { ApiResponse } from '../../types/api';
import { runtimeConfig } from '../../config/runtime';
import type {
  ListPublicAssetsInput,
  PublicAssetPagedResponse,
  PublicAssetResponse,
} from '../../types/public-assets';

const PUBLIC_ASSETS_BASE_PATH = '/api/v1/public/assets';

function buildPublicApiUrl(path: string, query?: Record<string, string | number | undefined>): string {
  const normalizedBaseUrl = runtimeConfig.apiBaseUrl.trim().replace(/\/+$/, '');
  const searchParams = new URLSearchParams();

  if (query) {
    Object.entries(query).forEach(([key, value]) => {
      if (value !== undefined && value !== null && value !== '') {
        searchParams.set(key, String(value));
      }
    });
  }

  const queryString = searchParams.toString();
  const requestPath = queryString ? `${path}?${queryString}` : path;

  return normalizedBaseUrl
    ? `${normalizedBaseUrl}${requestPath}`
    : requestPath;
}

async function fetchPublicApi<T>(path: string, query?: Record<string, string | number | undefined>): Promise<T> {
  const response = await fetch(buildPublicApiUrl(path, query), {
    method: 'GET',
    headers: {
      Accept: 'application/json',
    },
  });

  if (!response.ok) {
    throw new Error(`公开资源请求失败，状态码：${response.status}`);
  }

  const payload = await response.json() as ApiResponse<T>;
  return payload.data;
}

export async function listPublicAssets(input: ListPublicAssetsInput): Promise<PublicAssetPagedResponse> {
  return fetchPublicApi<PublicAssetPagedResponse>(PUBLIC_ASSETS_BASE_PATH, {
    page: input.page,
    pageSize: input.pageSize,
    query: input.query,
    contentType: input.contentType,
  });
}

export async function getPublicAsset(id: string): Promise<PublicAssetResponse> {
  return fetchPublicApi<PublicAssetResponse>(`${PUBLIC_ASSETS_BASE_PATH}/${id}`);
}
