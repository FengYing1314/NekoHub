import { describe, expect, it, vi } from 'vitest';
import { uploadAsset } from './assets.api';
import { httpClient } from '../client/http-client';

vi.mock('../client/http-client', () => ({
  httpClient: {
    post: vi.fn(),
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
