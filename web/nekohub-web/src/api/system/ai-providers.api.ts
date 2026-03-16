import { httpClient } from '../client/http-client';
import { unwrapAxiosApiResponse } from '../client/response';
import type {
  AiProviderProfile,
  AiProviderProfileTestRequest,
  AiProviderProfileTestResponse,
  CreateAiProviderProfileRequest,
  DeleteAiProviderProfileResponse,
  UpdateAiProviderProfileRequest,
} from '../../types/ai-providers';

const AI_PROVIDERS_BASE_PATH = '/api/v1/system/ai/providers';
const AI_PROVIDER_TEST_TIMEOUT_MS = 90000;

export async function listAiProviderProfiles(): Promise<AiProviderProfile[]> {
  const response = await httpClient.get(AI_PROVIDERS_BASE_PATH);
  return unwrapAxiosApiResponse<AiProviderProfile[]>(response);
}

export async function createAiProviderProfile(
  request: CreateAiProviderProfileRequest,
): Promise<AiProviderProfile> {
  const response = await httpClient.post(AI_PROVIDERS_BASE_PATH, request);
  return unwrapAxiosApiResponse<AiProviderProfile>(response);
}

export async function updateAiProviderProfile(
  profileId: string,
  request: UpdateAiProviderProfileRequest,
): Promise<AiProviderProfile> {
  const response = await httpClient.patch(`${AI_PROVIDERS_BASE_PATH}/${profileId}`, request);
  return unwrapAxiosApiResponse<AiProviderProfile>(response);
}

export async function testAiProviderProfile(
  request: AiProviderProfileTestRequest,
): Promise<AiProviderProfileTestResponse> {
  const response = await httpClient.post(`${AI_PROVIDERS_BASE_PATH}/test`, request, {
    timeout: AI_PROVIDER_TEST_TIMEOUT_MS,
  });
  return unwrapAxiosApiResponse<AiProviderProfileTestResponse>(response);
}

export async function deleteAiProviderProfile(profileId: string): Promise<DeleteAiProviderProfileResponse> {
  const response = await httpClient.delete(`${AI_PROVIDERS_BASE_PATH}/${profileId}`);
  return unwrapAxiosApiResponse<DeleteAiProviderProfileResponse>(response);
}
