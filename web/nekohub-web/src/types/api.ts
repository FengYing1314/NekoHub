export interface ApiResponse<T> {
  data: T;
}

export interface ApiError {
  code: string;
  message: string;
  traceId?: string;
  status?: number;
}

export interface ApiErrorResponse {
  error: ApiError;
}
