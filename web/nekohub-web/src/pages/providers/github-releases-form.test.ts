import { describe, expect, it } from 'vitest';
import {
  buildGitHubReleasesConfiguration,
  buildGitHubReleasesSecretConfiguration,
  mapGitHubReleasesSummaryToForm,
  validateGitHubReleasesForm,
} from './github-releases-form';

describe('github-releases form helpers', () => {
  it('requires fixedTag when releaseTagMode is fixed', () => {
    const errorKey = validateGitHubReleasesForm({
      owner: 'nekohub',
      repo: 'assets',
      releaseTagMode: 'fixed',
      fixedTag: '',
      assetPathPrefix: '',
      visibilityPolicy: 'public-first',
      token: '',
    });

    expect(errorKey).toBe('settings.storage.validation.githubReleasesFixedTagRequired');
  });

  it('maps summary data and builds normalized configuration', () => {
    const formValues = mapGitHubReleasesSummaryToForm({
      providerName: null,
      rootPath: null,
      endpointHost: null,
      bucketOrContainer: null,
      region: null,
      publicBaseUrl: null,
      forcePathStyle: null,
      owner: 'nekohub',
      repository: 'assets',
      reference: null,
      releaseTagMode: 'fixed',
      fixedTag: 'v1.0.0',
      pathPrefix: 'images/public',
      visibilityPolicy: 'public-first',
      basePath: null,
      assetPathPrefix: 'images/public',
      apiBaseUrl: null,
      rawBaseUrl: null,
    });

    expect(formValues).toMatchObject({
      owner: 'nekohub',
      repo: 'assets',
      releaseTagMode: 'fixed',
      fixedTag: 'v1.0.0',
      assetPathPrefix: 'images/public',
      visibilityPolicy: 'public-first',
    });

    expect(buildGitHubReleasesConfiguration({
      ...formValues,
      assetPathPrefix: '/images/public/',
      token: 'ghp_release_token',
    })).toEqual({
      owner: 'nekohub',
      repo: 'assets',
      releaseTagMode: 'fixed',
      fixedTag: 'v1.0.0',
      assetPathPrefix: 'images/public',
      visibilityPolicy: 'public-first',
      allowDelete: false,
    });

    expect(buildGitHubReleasesSecretConfiguration({
      ...formValues,
      token: 'ghp_release_token',
    })).toEqual({
      token: 'ghp_release_token',
    });
  });
});
