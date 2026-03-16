export interface ApiResponse<T> {
  data: T;
}

export type JsonPrimitive = string | number | boolean | null;
export type JsonValue = JsonPrimitive | JsonObject | JsonValue[];

export interface JsonObject {
  [key: string]: JsonValue;
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
