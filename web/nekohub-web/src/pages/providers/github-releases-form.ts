import type { StorageProviderConfigurationSummaryResponse } from '../../types/storage';

export type GitHubReleasesReleaseTagMode = 'latest' | 'fixed';
export type GitHubReleasesVisibilityPolicy = 'public-only' | 'public-first';

export interface GitHubReleasesFormValues {
  owner: string;
  repo: string;
  releaseTagMode: GitHubReleasesReleaseTagMode;
  fixedTag: string;
  assetPathPrefix: string;
  visibilityPolicy: GitHubReleasesVisibilityPolicy;
  token: string;
}

export function createDefaultGitHubReleasesFormValues(): GitHubReleasesFormValues {
  return {
    owner: '',
    repo: '',
    releaseTagMode: 'latest',
    fixedTag: '',
    assetPathPrefix: '',
    visibilityPolicy: 'public-first',
    token: '',
  };
}

export function mapGitHubReleasesSummaryToForm(
  summary: StorageProviderConfigurationSummaryResponse,
): GitHubReleasesFormValues {
  return {
    owner: summary.owner ?? '',
    repo: summary.repository ?? '',
    releaseTagMode: summary.releaseTagMode === 'fixed' ? 'fixed' : 'latest',
    fixedTag: summary.fixedTag ?? '',
    assetPathPrefix: summary.assetPathPrefix ?? summary.pathPrefix ?? '',
    visibilityPolicy: summary.visibilityPolicy === 'public-only' ? 'public-only' : 'public-first',
    token: '',
  };
}

export function validateGitHubReleasesForm(values: GitHubReleasesFormValues): string | null {
  const owner = values.owner.trim();
  const repo = values.repo.trim();
  const fixedTag = values.fixedTag.trim();
  const assetPathPrefix = values.assetPathPrefix.trim();
  const token = values.token.trim();

  if (!owner) {
    return 'settings.storage.validation.githubOwnerRequired';
  }

  if (!isValidGitHubSegment(owner)) {
    return 'settings.storage.validation.githubOwnerInvalid';
  }

  if (!repo) {
    return 'settings.storage.validation.githubRepoRequired';
  }

  if (!isValidGitHubSegment(repo)) {
    return 'settings.storage.validation.githubRepoInvalid';
  }

  if (values.releaseTagMode !== 'latest' && values.releaseTagMode !== 'fixed') {
    return 'settings.storage.validation.githubReleasesReleaseTagModeInvalid';
  }

  if (values.releaseTagMode === 'fixed' && !fixedTag) {
    return 'settings.storage.validation.githubReleasesFixedTagRequired';
  }

  if (assetPathPrefix && !isValidGitHubPathPrefix(assetPathPrefix)) {
    return 'settings.storage.validation.githubReleasesAssetPathPrefixInvalid';
  }

  if (token && token.length < 8) {
    return 'settings.storage.validation.githubTokenInvalid';
  }

  return null;
}

export function buildGitHubReleasesConfiguration(values: GitHubReleasesFormValues): Record<string, unknown> {
  const configuration: Record<string, unknown> = {
    owner: values.owner.trim(),
    repo: values.repo.trim(),
    releaseTagMode: values.releaseTagMode,
    visibilityPolicy: values.visibilityPolicy,
    allowDelete: false,
  };

  const fixedTag = values.fixedTag.trim();
  if (values.releaseTagMode === 'fixed' && fixedTag) {
    configuration.fixedTag = fixedTag;
  }

  const assetPathPrefix = normalizeOptionalPathPrefix(values.assetPathPrefix);
  if (assetPathPrefix) {
    configuration.assetPathPrefix = assetPathPrefix;
  }

  return configuration;
}

export function buildGitHubReleasesSecretConfiguration(
  values: GitHubReleasesFormValues,
): Record<string, unknown> | null {
  const token = values.token.trim();
  return token
    ? { token }
    : null;
}

function normalizeOptionalPathPrefix(value: string): string | null {
  const normalized = value.trim().replaceAll('\\', '/').replace(/^\/+|\/+$/g, '');
  return normalized ? normalized : null;
}

function isValidGitHubSegment(value: string): boolean {
  return /^[A-Za-z0-9._-]+$/.test(value);
}

function isValidGitHubPathPrefix(value: string): boolean {
  const normalized = value.trim().replaceAll('\\', '/').replace(/^\/+|\/+$/g, '');
  if (!normalized) {
    return true;
  }

  if (normalized.includes('//')) {
    return false;
  }

  return normalized.split('/').every((segment) => segment !== '.' && segment !== '..' && segment.length > 0);
}
