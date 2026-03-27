export interface StorageProviderCapabilitiesResponse {
  supportsPublicRead: boolean;
  supportsPrivateRead: boolean;
  supportsVisibilityToggle: boolean;
  supportsDelete: boolean;
  supportsDirectPublicUrl: boolean;
  requiresAccessProxy: boolean;
  recommendedForPrimaryStorage: boolean;
  isPlatformBacked: boolean;
  isExperimental: boolean;
  requiresTokenForPrivateRead: boolean;
}

export type StorageProviderType = 'local' | 's3-compatible';

export interface StorageProviderConfigurationSummaryResponse {
  providerName: string | null;
  rootPath: string | null;
  endpointHost: string | null;
  bucketOrContainer: string | null;
  region: string | null;
  publicBaseUrl: string | null;
  forcePathStyle: boolean | null;
  owner: string | null;
  repository: string | null;
  reference: string | null;
  releaseTagMode: string | null;
  fixedTag: string | null;
  pathPrefix: string | null;
  visibilityPolicy: string | null;
  basePath: string | null;
  assetPathPrefix: string | null;
  apiBaseUrl: string | null;
  rawBaseUrl: string | null;
}

export interface StorageProviderProfileResponse {
  id: string;
  name: string;
  displayName: string | null;
  providerType: string;
  isEnabled: boolean;
  isDefault: boolean;
  capabilities: StorageProviderCapabilitiesResponse;
  configurationSummary: StorageProviderConfigurationSummaryResponse;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface StorageRuntimeSummaryResponse {
  providerType: string;
  providerName: string;
  capabilities: StorageProviderCapabilitiesResponse;
  isConfigurationDriven: boolean;
  matchesDefaultProfileType: boolean | null;
}

export type StorageRuntimeAlignmentCode =
  | 'db_default_profile_missing'
  | 'db_default_profile_disabled'
  | 'runtime_matches_db_default_provider_type'
  | 'runtime_mismatches_db_default_provider_type'
  | string;

export interface StorageRuntimeAlignmentStatusResponse {
  runtimeSelectionSource: string;
  hasDefaultProfile: boolean;
  isDefaultProfileEnabled: boolean | null;
  providerTypeMatchesDefaultProfile: boolean | null;
  code: StorageRuntimeAlignmentCode;
  message: string;
}

export interface StorageProviderOverviewResponse {
  profiles: StorageProviderProfileResponse[];
  defaultProfile: StorageProviderProfileResponse | null;
  runtime: StorageRuntimeSummaryResponse;
  alignment: StorageRuntimeAlignmentStatusResponse;
}

export interface CreateStorageProviderProfileRequest {
  name: string;
  displayName?: string | null;
  providerType: StorageProviderType;
  isEnabled?: boolean;
  isDefault?: boolean;
  configuration: Record<string, unknown>;
  secretConfiguration?: Record<string, unknown>;
}

export interface UpdateStorageProviderProfileRequest {
  name?: string | null;
  displayName?: string | null;
  isEnabled?: boolean;
  configuration?: Record<string, unknown>;
  secretConfiguration?: Record<string, unknown>;
}

export interface DeleteStorageProviderProfileResponse {
  id: string;
  wasDefault: boolean;
  status: string;
  deletedAtUtc: string;
}

export type GitHubRepoBrowseType = 'all' | 'file' | 'dir';

export interface BrowseGitHubRepoProfileRequest {
  path?: string;
  recursive?: boolean;
  maxDepth?: number;
  type?: GitHubRepoBrowseType;
  keyword?: string;
  page?: number;
  pageSize?: number;
}

export interface GitHubRepoBrowseItemResponse {
  name: string;
  path: string;
  type: string;
  isDirectory: boolean;
  isFile: boolean;
  size: number | null;
  sha: string | null;
  publicUrl: string | null;
}

export interface GitHubRepoBrowseResponse {
  profileId: string;
  requestedPath: string;
  recursive: boolean;
  maxDepth: number;
  type: string;
  keyword: string | null;
  total: number;
  page: number;
  pageSize: number;
  hasMore: boolean;
  visibilityPolicy: string;
  usesControlledRead: boolean;
  items: GitHubRepoBrowseItemResponse[];
}

export interface UpsertGitHubRepoProfileRequest {
  path: string;
  contentBase64: string;
  commitMessage?: string;
  expectedSha?: string;
}

export interface GitHubRepoUpsertResponse {
  profileId: string;
  path: string;
  operation: string;
  size: number;
  sha: string;
  visibilityPolicy: string;
  usesControlledRead: boolean;
  publicUrl: string | null;
}
