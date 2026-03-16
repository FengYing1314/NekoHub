export interface PublicAssetListItemResponse {
  id: string;
  type: string;
  originalFileName: string | null;
  contentType: string;
  size: number;
  width: number | null;
  height: number | null;
  publicUrl: string | null;
  description: string | null;
  altText: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface PublicAssetDerivativeSummaryResponse {
  kind: string;
  contentType: string;
  extension: string;
  size: number;
  width: number | null;
  height: number | null;
  publicUrl: string | null;
  createdAtUtc: string;
}

export interface PublicAssetResponse {
  id: string;
  type: string;
  originalFileName: string | null;
  contentType: string;
  extension: string;
  size: number;
  width: number | null;
  height: number | null;
  publicUrl: string | null;
  description: string | null;
  altText: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  derivatives: PublicAssetDerivativeSummaryResponse[];
}

export interface PublicAssetPagedResponse {
  items: PublicAssetListItemResponse[];
  page: number;
  pageSize: number;
  total: number;
}

export interface ListPublicAssetsInput {
  page: number;
  pageSize: number;
  query?: string;
  contentType?: string;
}
