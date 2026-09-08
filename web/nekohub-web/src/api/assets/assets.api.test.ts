import { describe, expect, it, vi } from 'vitest';
import { uploadAsset, listAssetStorageTargets, listAssetWorkflows, getAssetJobs, retryAssetJob, getAssetDerivativeContentBlob } from './assets.api';
import { httpClient } from '../client/http-client';

vi.mock('../client/http-client', () => ({
  httpClient: {
    post: vi.fn(),
    get: vi.fn(),
  },
}));

describe('uploadAsset', () => {
  it('serializes storageProviderProfileId and runEnrichment into multipart form data', async () => {
    vi.mocked(httpClient.post).mockResolvedValue({
      data: {
        data: {
          id: 'asset-1',
        },
      },
    } as never);

    const file = new File(['hello'], 'cat.png', { type: 'image/png' });

    await uploadAsset({
      file,
      description: '  sample  ',
      altText: '  alt text  ',
      isPublic: false,
      storageProviderProfileId: 'profile-1',
      runEnrichment: false,
      commitMessage: '  upload commit  ',
    });

    const [, formData] = vi.mocked(httpClient.post).mock.calls[0];
    const entries = Array.from((formData as FormData).entries());

    expect(entries).toEqual(expect.arrayContaining([
      ['description', 'sample'],
      ['altText', 'alt text'],
      ['isPublic', 'false'],
      ['storageProviderProfileId', 'profile-1'],
      ['runEnrichment', 'false'],
      ['commitMessage', 'upload commit'],
    ]));
  });
});

describe('asset operation endpoints', () => {
  it('loads workflow and storage options through asset-scoped endpoints', async () => {
    vi.mocked(httpClient.get).mockResolvedValue({ data: { data: [] } });
    await listAssetWorkflows();
    await listAssetStorageTargets();
    expect(httpClient.get).toHaveBeenCalledWith('/api/v1/assets/workflows');
    expect(httpClient.get).toHaveBeenCalledWith('/api/v1/assets/storage-targets');
  });

  it('loads and retries only the selected asset job', async () => {
    vi.mocked(httpClient.get).mockResolvedValue({ data: { data: [] } });
    await getAssetJobs('asset-1');
    await retryAssetJob('asset-1', 'job-1');
    expect(httpClient.get).toHaveBeenCalledWith('/api/v1/assets/asset-1/jobs');
    expect(httpClient.post).toHaveBeenCalledWith('/api/v1/assets/asset-1/jobs/job-1/retry');
  });
});

it('downloads retained originals as authenticated blobs', async () => {
  const blob = new Blob(['image']);
  vi.mocked(httpClient.get).mockResolvedValue({ data: blob });
  expect(await getAssetDerivativeContentBlob('asset-1', 'original_snapshot')).toBe(blob);
  expect(httpClient.get).toHaveBeenCalledWith('/api/v1/assets/asset-1/derivatives/original_snapshot/content', { responseType: 'blob' });
});
