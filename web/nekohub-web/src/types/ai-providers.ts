export interface AiProviderProfile {
  id: string;
  name: string;
  apiBaseUrl: string;
  apiKey: string;
  modelName: string;
  defaultSystemPrompt: string;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CreateAiProviderProfileRequest {
  name: string;
  apiBaseUrl: string;
  apiKey: string;
  modelName: string;
  defaultSystemPrompt?: string | null;
  isActive?: boolean;
}

export interface UpdateAiProviderProfileRequest {
  name?: string;
  apiBaseUrl?: string;
  apiKey?: string;
  modelName?: string;
  defaultSystemPrompt?: string | null;
  isActive?: boolean;
}

export interface AiProviderProfileTestRequest {
  profileId?: string;
  apiBaseUrl?: string;
  apiKey?: string;
  modelName?: string;
  defaultSystemPrompt?: string | null;
}

export interface AiProviderProfileTestResponse {
  succeeded: boolean;
  caption: string | null;
  resolvedModelName: string;
  resolvedApiBaseUrl: string;
  errorMessage: string | null;
}

export interface DeleteAiProviderProfileResponse {
  id: string;
  wasActive: boolean;
  status: string;
  deletedAtUtc: string;
}
