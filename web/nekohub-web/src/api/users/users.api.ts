import { httpClient } from '../client/http-client';
import { unwrapAxiosApiResponse } from '../client/response';
import type {
  CreateUserRequest,
  ResetUserPasswordRequest,
  UpdateUserPermissionsRequest,
  UpdateUserRequest,
  UserDetailResponse,
  UserListItemResponse,
} from '../../types/users';

const USERS_BASE_PATH = '/api/v1/users';

export async function listUsers(): Promise<UserListItemResponse[]> {
  const response = await httpClient.get(USERS_BASE_PATH);
  return unwrapAxiosApiResponse<UserListItemResponse[]>(response);
}

export async function createUser(request: CreateUserRequest): Promise<UserDetailResponse> {
  const response = await httpClient.post(USERS_BASE_PATH, request);
  return unwrapAxiosApiResponse<UserDetailResponse>(response);
}

export async function updateUser(userId: string, request: UpdateUserRequest): Promise<UserDetailResponse> {
  const response = await httpClient.patch(`${USERS_BASE_PATH}/${userId}`, request);
  return unwrapAxiosApiResponse<UserDetailResponse>(response);
}

export async function updateUserPermissions(
  userId: string,
  request: UpdateUserPermissionsRequest,
): Promise<UserDetailResponse> {
  const response = await httpClient.patch(`${USERS_BASE_PATH}/${userId}/permissions`, request);
  return unwrapAxiosApiResponse<UserDetailResponse>(response);
}

export async function resetUserPassword(userId: string, request: ResetUserPasswordRequest): Promise<void> {
  await httpClient.post(`${USERS_BASE_PATH}/${userId}/reset-password`, request);
}
