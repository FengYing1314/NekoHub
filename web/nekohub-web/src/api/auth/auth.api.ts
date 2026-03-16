import { authHttpClient, httpClient } from '../client/http-client';
import { unwrapAxiosApiResponse } from '../client/response';
import type {
  AuthTokenResponse,
  AuthenticatedUser,
  LoginRequest,
  LogoutRequest,
  RefreshTokenRequest,
} from '../../types/auth';

const AUTH_BASE_PATH = '/api/v1/auth';

type AuthTokenResponseLike = Partial<AuthTokenResponse> & {
  accessToken?: string;
  refreshToken?: string;
  user?: AuthenticatedUser;
};

function normalizeAuthTokenResponse(payload: AuthTokenResponseLike): AuthTokenResponse {
  return {
    accessToken: payload.accessToken ?? '',
    refreshToken: payload.refreshToken ?? '',
    tokenType: payload.tokenType,
    expiresInSeconds: payload.expiresInSeconds,
    user: payload.user as AuthenticatedUser,
  };
}

export async function login(request: LoginRequest): Promise<AuthTokenResponse> {
  const response = await authHttpClient.post(`${AUTH_BASE_PATH}/login`, request);
  const payload = unwrapAxiosApiResponse<AuthTokenResponseLike>(response);
  return normalizeAuthTokenResponse(payload);
}

export async function refreshToken(request: RefreshTokenRequest): Promise<AuthTokenResponse> {
  const response = await authHttpClient.post(`${AUTH_BASE_PATH}/refresh`, request);
  const payload = unwrapAxiosApiResponse<AuthTokenResponseLike>(response);
  return normalizeAuthTokenResponse(payload);
}

export async function logout(request: LogoutRequest): Promise<void> {
  await httpClient.post(`${AUTH_BASE_PATH}/logout`, request);
}

export async function getCurrentUser(): Promise<AuthenticatedUser> {
  const response = await httpClient.get(`${AUTH_BASE_PATH}/me`);
  return unwrapAxiosApiResponse<AuthenticatedUser>(response);
}
