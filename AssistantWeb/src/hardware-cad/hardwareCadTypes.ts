export type HardwareRecord = {
  hardwareRecordId: string;
  hwdNumber?: string;
  mcMasterPartNumber: string;
  vendor?: string;
  description?: string;
};

export type ViewerDerivative = {
  schemaVersion: string;
  sourceSha256: string;
  sourceAssetId: string;
  glbSha256: string;
  glbByteLength: number;
  glbContentAddressedPath: string;
  converterId: string;
  converterVersion: string;
  convertedAtUtc: string;
  warnings: string[];
  isValid: boolean;
};

export type VendorCadBinding = {
  hardwareRecordId: string;
  mcMasterPartNumber: string;
  vendor: string;
  preferredVariantKey: string;
  authoritativeCadLink: string;
  normalizedCadUrl: string;
  boundAtUtc: string;
  bindingReceiptId: string;
};

export type VendorCadAsset = {
  assetId: string;
  partNumber: string;
  sourceCadLink: string;
  normalizedCadUrl: string;
  sha256: string;
  byteLength: number;
  acquiredAtUtc: string;
  contentAddressedPath: string;
};

export type VendorCadAcquisitionReceipt = {
  receiptId: string;
  timestampUtc: string;
  hardwareRecordId: string;
  partNumber: string;
  status: string;
  binding?: VendorCadBinding;
  asset?: VendorCadAsset;
  viewerDerivative?: ViewerDerivative;
  cacheHit: boolean;
  limitations: string[];
  errors: Array<{ code: string; message: string }>;
  safeToRetry: boolean;
};
