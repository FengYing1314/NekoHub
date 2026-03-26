const DEFAULT_MAX_UPLOAD_SIZE_BYTES = 10 * 1024 * 1024;
const DEFAULT_API_BASE_URL = 'http://localhost:5121';

function parsePositiveInt(rawValue: string | undefined, fallback: number): number {
  if (!rawValue) {
    return fallback;
  }

  const parsed = Number.parseInt(rawValue, 10);
  if (!Number.isInteger(parsed) || parsed <= 0) {
    return fallback;
  }

  return parsed;
}

function normalizeApiBaseUrl(rawValue: string | undefined): string {
  if (!rawValue) {
    return DEFAULT_API_BASE_URL;
  }

  const trimmed = rawValue.trim();
  if (!trimmed) {
    return DEFAULT_API_BASE_URL;
  }

  if (trimmed === '/') {
    return '/';
  }

  return trimmed.replace(/\/+$/, '');
}

export const runtimeConfig = {
  apiBaseUrl: normalizeApiBaseUrl(import.meta.env.VITE_API_BASE_URL),
  maxUploadSizeBytes: parsePositiveInt(import.meta.env.VITE_MAX_UPLOAD_SIZE_BYTES, DEFAULT_MAX_UPLOAD_SIZE_BYTES),
};
