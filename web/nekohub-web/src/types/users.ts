import type { PermissionKey, UserRole } from './auth';

export interface UserListItemResponse {
  id: string;
  username: string;
  role: UserRole;
  isActive: boolean;
  permissions: PermissionKey[];
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface UserDetailResponse extends UserListItemResponse {
  lastLoginAtUtc: string | null;
}

export interface CreateUserRequest {
  username: string;
  password: string;
  role: UserRole;
  isActive?: boolean;
  permissions?: PermissionKey[];
}

export interface UpdateUserRequest {
  username?: string;
  role?: UserRole;
  isActive?: boolean;
}

export interface UpdateUserPermissionsRequest {
  permissions: PermissionKey[];
}

export interface ResetUserPasswordRequest {
  newPassword: string;
}
