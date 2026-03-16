export type UserRole = 'superAdmin' | 'admin' | 'user' | string;

export type PermissionKey =
  | 'assets.read'
  | 'assets.create'
  | 'assets.update'
  | 'assets.delete'
  | 'providers.read'
  | 'providers.create'
  | 'providers.update'
  | 'providers.delete'
  | 'aiProviders.read'
  | 'aiProviders.create'
  | 'aiProviders.update'
  | 'aiProviders.delete'
  | 'settings.read'
  | 'settings.update'
  | 'users.read'
  | 'users.create'
  | 'users.update'
  | 'users.disable'
  | 'users.managePermissions'
  | string;

export interface AuthenticatedUser {
  id: string;
  username: string;
  role: UserRole;
  isActive: boolean;
  permissions: PermissionKey[];
}

export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
  tokenType?: string;
  expiresInSeconds?: number;
}

export interface LoginRequest {
  username: string;
  password: string;
}

export interface RefreshTokenRequest {
  refreshToken: string;
}

export interface LogoutRequest {
  refreshToken: string;
}

export interface AuthTokenResponse extends AuthTokens {
  user: AuthenticatedUser;
}
