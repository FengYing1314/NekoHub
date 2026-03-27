import { httpClient } from '../client/http-client';
import { unwrapAxiosApiResponse } from '../client/response';
import type {
  BrowseGitHubRepoProfileRequest,
  CreateStorageProviderProfileRequest,
  DeleteStorageProviderProfileResponse,
  GitHubRepoBrowseResponse,
  GitHubRepoUpsertResponse,
  StorageProviderOverviewResponse,
  StorageProviderProfileResponse,
  UpsertGitHubRepoProfileRequest,
  UpdateStorageProviderProfileRequest,
} from '../../types/storage';

const SYSTEM_STORAGE_BASE_PATH = '/api/v1/system/storage';

export async function getStorageProviderOverview(): Promise<StorageProviderOverviewResponse> {
  const response = await httpClient.get(`${SYSTEM_STORAGE_BASE_PATH}/providers`);
  return unwrapAxiosApiResponse<StorageProviderOverviewResponse>(response);
}

export async function createStorageProviderProfile(
  request: CreateStorageProviderProfileRequest,
): Promise<StorageProviderProfileResponse> {
  const response = await httpClient.post(`${SYSTEM_STORAGE_BASE_PATH}/providers`, request);
  return unwrapAxiosApiResponse<StorageProviderProfileResponse>(response);
}

export async function updateStorageProviderProfile(
  profileId: string,
  request: UpdateStorageProviderProfileRequest,
): Promise<StorageProviderProfileResponse> {
  const response = await httpClient.patch(`${SYSTEM_STORAGE_BASE_PATH}/providers/${profileId}`, request);
  return unwrapAxiosApiResponse<StorageProviderProfileResponse>(response);
}

export async function deleteStorageProviderProfile(profileId: string): Promise<DeleteStorageProviderProfileResponse> {
  const response = await httpClient.delete(`${SYSTEM_STORAGE_BASE_PATH}/providers/${profileId}`);
  return unwrapAxiosApiResponse<DeleteStorageProviderProfileResponse>(response);
}

export async function setDefaultStorageProviderProfile(profileId: string): Promise<StorageProviderProfileResponse> {
  const response = await httpClient.post(`${SYSTEM_STORAGE_BASE_PATH}/providers/${profileId}/set-default`, null);
  return unwrapAxiosApiResponse<StorageProviderProfileResponse>(response);
}

export async function browseGitHubRepoProfile(
  profileId: string,
  request: BrowseGitHubRepoProfileRequest,
): Promise<GitHubRepoBrowseResponse> {
  const response = await httpClient.get(`${SYSTEM_STORAGE_BASE_PATH}/providers/${profileId}/github-repo/browse`, {
    params: request,
  });
  return unwrapAxiosApiResponse<GitHubRepoBrowseResponse>(response);
}

export async function upsertGitHubRepoProfile(
  profileId: string,
  request: UpsertGitHubRepoProfileRequest,
): Promise<GitHubRepoUpsertResponse> {
  const response = await httpClient.post(`${SYSTEM_STORAGE_BASE_PATH}/providers/${profileId}/github-repo/upsert`, request);
  return unwrapAxiosApiResponse<GitHubRepoUpsertResponse>(response);
}
