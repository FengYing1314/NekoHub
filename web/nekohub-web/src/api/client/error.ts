import axios from 'axios';
import type { ApiError, ApiErrorResponse } from '../../types/api';

export function extractApiError(error: unknown): ApiError | null {
  if (!axios.isAxiosError<ApiErrorResponse>(error)) {
    return null;
  }

  return error.response?.data?.error ?? null;
}

export function extractApiErrorMessage(error: unknown): string {
  const apiError = extractApiError(error);
  if (apiError?.message) {
    return apiError.message;
  }

  if (axios.isAxiosError<ApiErrorResponse>(error)) {
    if (error.response?.status === 401) {
      return '认证失败，请检查 API Key。';
    }

    if (error.message) {
      return error.message;
    }
  }

  if (error instanceof Error) {
    return error.message;
  }

  return '请求失败，请稍后重试。';
}
