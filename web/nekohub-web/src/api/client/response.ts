import type { AxiosResponse } from 'axios';
import type { ApiResponse } from '../../types/api';

export function unwrapApiResponse<T>(response: ApiResponse<T>): T {
  return response.data;
}

export function unwrapAxiosApiResponse<T>(response: AxiosResponse<ApiResponse<T>>): T {
  return unwrapApiResponse(response.data);
}
