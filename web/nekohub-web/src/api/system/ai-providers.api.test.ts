import { describe, expect, it, vi } from 'vitest';
import { testAiProviderProfile } from './ai-providers.api';
import { httpClient } from '../client/http-client';

vi.mock('../client/http-client', () => ({
  httpClient: {
    post: vi.fn(),
  },
}));

describe('testAiProviderProfile', () => {
  it('uses an extended timeout for long-running AI provider compatibility checks', async () => {
    vi.mocked(httpClient.post).mockResolvedValue({
      data: {
        data: {
          succeeded: true,
          caption: 'ok',
          resolvedModelName: 'gpt-5.4',
          resolvedApiBaseUrl: 'https://api.example.com/v1',
          errorMessage: null,
        },
      },
    } as never);

    await testAiProviderProfile({
      apiBaseUrl: 'https://api.example.com/v1',
      apiKey: 'sk-test',
      modelName: 'gpt-5.4',
    });

    expect(httpClient.post).toHaveBeenCalledWith(
      '/api/v1/system/ai/providers/test',
      {
        apiBaseUrl: 'https://api.example.com/v1',
        apiKey: 'sk-test',
        modelName: 'gpt-5.4',
      },
      expect.objectContaining({
        timeout: 90000,
      }),
    );
  });
});
