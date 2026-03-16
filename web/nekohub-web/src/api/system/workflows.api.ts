import { httpClient } from '../client/http-client';
import { unwrapAxiosApiResponse } from '../client/response';
import type {
  CreateWorkflowProfileRequest,
  UpdateWorkflowProfileRequest,
  WorkflowProfileResponse,
} from '../../types/workflows';

const SYSTEM_WORKFLOWS_BASE_PATH = '/api/v1/system/workflows';

export async function listWorkflowProfiles(): Promise<WorkflowProfileResponse[]> {
  const response = await httpClient.get(SYSTEM_WORKFLOWS_BASE_PATH);
  return unwrapAxiosApiResponse<WorkflowProfileResponse[]>(response);
}

export async function getWorkflowProfile(id: string): Promise<WorkflowProfileResponse> {
  const response = await httpClient.get(`${SYSTEM_WORKFLOWS_BASE_PATH}/${id}`);
  return unwrapAxiosApiResponse<WorkflowProfileResponse>(response);
}

export async function createWorkflowProfile(
  request: CreateWorkflowProfileRequest,
): Promise<WorkflowProfileResponse> {
  const response = await httpClient.post(SYSTEM_WORKFLOWS_BASE_PATH, request);
  return unwrapAxiosApiResponse<WorkflowProfileResponse>(response);
}

export async function updateWorkflowProfile(
  id: string,
  request: UpdateWorkflowProfileRequest,
): Promise<WorkflowProfileResponse> {
  const response = await httpClient.put(`${SYSTEM_WORKFLOWS_BASE_PATH}/${id}`, request);
  return unwrapAxiosApiResponse<WorkflowProfileResponse>(response);
}

export async function deleteWorkflowProfile(id: string): Promise<void> {
  await httpClient.delete(`${SYSTEM_WORKFLOWS_BASE_PATH}/${id}`);
}

export async function setWorkflowAutoRun(id: string): Promise<WorkflowProfileResponse> {
  const response = await httpClient.patch(`${SYSTEM_WORKFLOWS_BASE_PATH}/${id}/autorun`, null);
  return unwrapAxiosApiResponse<WorkflowProfileResponse>(response);
}
