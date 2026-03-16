import type { ApiResponse } from '../../types/api';
import type { SystemBootstrapResponse } from '../../types/system';

export const DEFAULT_BOOTSTRAP_TIMEOUT_MS = 5000;

export class BootstrapRequestError extends Error
{
  constructor(message: string, options?: ErrorOptions) {
    super(message, options);
    this.name = new.target.name;
  }
}

export class BootstrapTimeoutError extends BootstrapRequestError {
  constructor(timeoutMs: number, options?: ErrorOptions) {
    super(`Bootstrap request timed out after ${timeoutMs}ms.`, options);
  }
}

export class BootstrapHttpError extends BootstrapRequestError {
  readonly status: number;

  constructor(status: number) {
    super(`Bootstrap request failed with status ${status}.`);
    this.status = status;
  }
}

export class BootstrapNetworkError extends BootstrapRequestError {}

function buildBootstrapUrl(apiBaseUrl: string): string {
  const normalizedBaseUrl = apiBaseUrl.trim().replace(/\/+$/, '');
  return normalizedBaseUrl
    ? `${normalizedBaseUrl}/api/v1/system/bootstrap`
    : '/api/v1/system/bootstrap';
}

export async function fetchSystemBootstrap(
  apiBaseUrl: string,
  timeoutMs = DEFAULT_BOOTSTRAP_TIMEOUT_MS,
): Promise<SystemBootstrapResponse> {
  const controller = new AbortController();
  const timeoutHandle = setTimeout(() => controller.abort(), timeoutMs);

  try {
    let response: Response;

    try {
      response = await fetch(buildBootstrapUrl(apiBaseUrl), {
        method: 'GET',
        headers: {
          Accept: 'application/json',
        },
        signal: controller.signal,
      });
    } catch (error) {
      if (error instanceof Error && error.name === 'AbortError') {
        throw new BootstrapTimeoutError(timeoutMs, { cause: error });
      }

      throw new BootstrapNetworkError('Bootstrap request could not reach the server.', { cause: error });
    }

    if (!response.ok) {
      throw new BootstrapHttpError(response.status);
    }

    const payload = await response.json() as ApiResponse<SystemBootstrapResponse>;
    return payload.data;
  } finally {
    clearTimeout(timeoutHandle);
  }
}
