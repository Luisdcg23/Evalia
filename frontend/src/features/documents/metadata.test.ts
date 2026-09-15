import { expect, it } from "vitest";
import { createDocumentMetadata } from "./metadata";

it("creates deterministic portable metadata from an attached file", async () => {
  const file = new File(["contenido"], " carta.pdf ", { type: "application/pdf" });
  const first = await createDocumentMetadata(file, "CARTA_AUTORIZACION");
  const second = await createDocumentMetadata(file, "CARTA_AUTORIZACION");

  expect(first).toMatchObject({ documentType: "CARTA_AUTORIZACION", fileName: "carta.pdf", mimeType: "application/pdf", sizeBytes: 9 });
  expect(first.hash).toMatch(/^[a-f0-9]{64}$/);
  expect(first.storageReference).toBe(`client-sha256:${first.hash}`);
  expect(second.hash).toBe(first.hash);
});
