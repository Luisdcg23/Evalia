export interface DocumentMetadata {
  documentType: string;
  fileName: string;
  mimeType: string;
  sizeBytes: number;
  hash: string;
  storageReference: string;
}

export async function createDocumentMetadata(file: File, documentType: string): Promise<DocumentMetadata> {
  const bytes = await file.arrayBuffer();
  const digest = await crypto.subtle.digest("SHA-256", bytes);
  const hash = Array.from(new Uint8Array(digest), value => value.toString(16).padStart(2, "0")).join("");
  return {
    documentType,
    fileName: file.name.trim(),
    mimeType: file.type || "application/octet-stream",
    sizeBytes: file.size,
    hash,
    storageReference: `client-sha256:${hash}`,
  };
}
