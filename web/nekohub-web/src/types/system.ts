export interface SystemBootstrapResponse {
  apiKeyRequired: boolean;
  maxUploadSizeBytes: number;
  allowedContentTypes: string[];
}
