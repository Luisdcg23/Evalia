import { afterEach, describe, expect, it, vi } from "vitest";
import {
  activateTemplate, createTemplate, createTemplateItem, createTemplateVersion, deleteTemplateItem,
  getActiveTemplate, listTemplateItems, listTemplates, publishTemplate, updateTemplateItem,
} from "./api";

describe("evaluation templates API", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("lists templates ordered by the backend, sending only the bearer token", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
      ok: true,
      json: async () => [{ id: 1, familyId: "f1", name: "Ficha de prueba", version: 1, status: "DRAFT", createdAt: "2026-09-01T00:00:00Z" }],
    }));

    const templates = await listTemplates("token-local");

    expect(templates[0].status).toBe("DRAFT");
    expect(fetch).toHaveBeenCalledWith("http://localhost:5080/api/evaluation-templates", {
      headers: { Authorization: "Bearer token-local" },
    });
  });

  it("propagates the fetch failure message when listing fails", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false, status: 500, json: async () => ({}) }));

    await expect(listTemplates("token-local")).rejects.toThrow(/no fue posible consultar las plantillas/i);
  });

  it("trims the name and posts a new draft template", async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ id: 9, familyId: "f9", name: "Plantilla de prueba", version: 1, status: "DRAFT", createdAt: "2026-09-11T00:00:00Z" }),
    });
    vi.stubGlobal("fetch", fetchMock);

    await createTemplate("token-local", "  Plantilla de prueba  ");

    expect(fetchMock).toHaveBeenCalledWith("http://localhost:5080/api/evaluation-templates", {
      method: "POST",
      headers: { Authorization: "Bearer token-local", "Content-Type": "application/json" },
      body: JSON.stringify({ name: "Plantilla de prueba" }),
    });
  });

  it("maps a 400 on template creation to a validation message", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
      ok: false, status: 400, json: async () => ({ errors: { template: ["El nombre de la plantilla es obligatorio."] } }),
    }));

    await expect(createTemplate("token-local", "  ")).rejects.toThrow(/nombre de la plantilla es obligatorio/i);
  });

  it("lists the item tree of a template", async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => [{
        id: 1, templateId: 9, parentId: null, code: "1", description: "Capítulo 1", itemType: "CHAPTER", order: 10,
        weight: null, isRequired: false, isCritical: false, allowsNotApplicable: true, responseType: null,
        rulesJson: "{}", scoreConfigurationJson: "{}", isActive: true, versionToken: "tok-1",
      }],
    });
    vi.stubGlobal("fetch", fetchMock);

    const items = await listTemplateItems("token-local", 9);

    expect(items).toHaveLength(1);
    expect(fetchMock).toHaveBeenCalledWith("http://localhost:5080/api/evaluation-templates/9/items", {
      headers: { Authorization: "Bearer token-local" },
    });
  });

  it("maps a 404 on item listing to a not-found message", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false, status: 404, json: async () => ({}) }));

    await expect(listTemplateItems("token-local", 404)).rejects.toThrow(/no existe/i);
  });

  it("normalizes the payload when creating an item (trims text, uppercases itemType, applies defaults)", async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ id: 2, templateId: 9, parentId: null, code: "1", description: "Capítulo 1", itemType: "CHAPTER", order: 10, weight: null, isRequired: false, isCritical: false, allowsNotApplicable: true, responseType: null, rulesJson: "{}", scoreConfigurationJson: "{}", isActive: true, versionToken: "tok-1" }),
    });
    vi.stubGlobal("fetch", fetchMock);

    await createTemplateItem("token-local", 9, {
      code: " 1 ", description: " Capítulo 1 ", itemType: "chapter", order: 10, parentId: null,
    });

    const call = fetchMock.mock.calls[0];
    expect(call[0]).toBe("http://localhost:5080/api/evaluation-templates/9/items");
    expect(call[1]).toMatchObject({ method: "POST", headers: { Authorization: "Bearer token-local", "Content-Type": "application/json" } });
    expect(JSON.parse(call[1].body as string)).toMatchObject({
      code: "1", description: "Capítulo 1", itemType: "CHAPTER", order: 10, parentId: null,
      isRequired: false, isCritical: false, allowsNotApplicable: true, responseType: null,
    });
  });

  it("maps a 409 on item creation (published template) to the backend's conflict message", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
      ok: false, status: 409, json: async () => ({ message: "La versión publicada es inmutable." }),
    }));

    await expect(createTemplateItem("token-local", 9, {
      code: "1", description: "Capítulo 1", itemType: "CHAPTER", order: 10,
    })).rejects.toThrow(/versión publicada es inmutable/i);
  });

  it("sends the versionToken when updating an item for optimistic concurrency", async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ id: 2, templateId: 9, parentId: null, code: "1", description: "Capítulo 1 editado", itemType: "CHAPTER", order: 10, weight: null, isRequired: false, isCritical: false, allowsNotApplicable: true, responseType: null, rulesJson: "{}", scoreConfigurationJson: "{}", isActive: true, versionToken: "tok-2" }),
    });
    vi.stubGlobal("fetch", fetchMock);

    await updateTemplateItem("token-local", 9, 2, {
      code: "1", description: "Capítulo 1 editado", itemType: "CHAPTER", order: 10, versionToken: "tok-1",
    });

    const call = fetchMock.mock.calls[0];
    expect(call[0]).toBe("http://localhost:5080/api/evaluation-templates/9/items/2");
    expect(call[1]).toMatchObject({ method: "PUT" });
    expect(JSON.parse(call[1].body as string)).toMatchObject({ versionToken: "tok-1" });
  });

  it("maps a 409 on item update (stale versionToken) to a conflict message", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
      ok: false, status: 409, json: async () => ({ message: "El ítem fue modificado por otro usuario." }),
    }));

    await expect(updateTemplateItem("token-local", 9, 2, {
      code: "1", description: "x", itemType: "CHAPTER", order: 10, versionToken: "stale",
    })).rejects.toThrow(/modificado por otro usuario/i);
  });

  it("deletes an item with no body", async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, status: 204, json: async () => ({}) });
    vi.stubGlobal("fetch", fetchMock);

    await deleteTemplateItem("token-local", 9, 2);

    expect(fetchMock).toHaveBeenCalledWith("http://localhost:5080/api/evaluation-templates/9/items/2", {
      method: "DELETE",
      headers: { Authorization: "Bearer token-local" },
    });
  });

  it("maps a 409 on delete (dependent items) to the backend's message", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
      ok: false, status: 409, json: async () => ({ message: "Mueva o elimine primero los ítems dependientes." }),
    }));

    await expect(deleteTemplateItem("token-local", 9, 2)).rejects.toThrow(/ítems dependientes/i);
  });

  it("publishes a template with no request body", async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true, json: async () => ({ id: 9, familyId: "f9", name: "Plantilla de prueba", version: 1, status: "PUBLISHED", createdAt: "2026-09-11T00:00:00Z", publishedAt: "2026-09-11T01:00:00Z" }),
    });
    vi.stubGlobal("fetch", fetchMock);

    const published = await publishTemplate("token-local", 9);

    expect(published.status).toBe("PUBLISHED");
    expect(fetchMock).toHaveBeenCalledWith("http://localhost:5080/api/evaluation-templates/9/publish", {
      method: "POST",
      headers: { Authorization: "Bearer token-local" },
    });
  });

  it("maps a 409 on publish (empty template) to the backend's message", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
      ok: false, status: 409, json: async () => ({ message: "La plantilla debe contener al menos un ítem." }),
    }));

    await expect(publishTemplate("token-local", 9)).rejects.toThrow(/al menos un ítem/i);
  });

  it("requests a new draft version cloned from a published template", async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true, json: async () => ({ id: 10, familyId: "f9", name: "Plantilla de prueba", version: 2, status: "DRAFT", createdAt: "2026-09-11T02:00:00Z" }),
    });
    vi.stubGlobal("fetch", fetchMock);

    const clone = await createTemplateVersion("token-local", 9);

    expect(clone.version).toBe(2);
    expect(fetchMock).toHaveBeenCalledWith("http://localhost:5080/api/evaluation-templates/9/versions", {
      method: "POST",
      headers: { Authorization: "Bearer token-local" },
    });
  });

  it("maps a 409 on version creation (source not published) to a clear message", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false, status: 409, json: async () => ({}) }));

    await expect(createTemplateVersion("token-local", 9)).rejects.toThrow(/solo una versión publicada puede clonarse/i);
  });

  it("reads which template is active", async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true, json: async () => ({ templateId: 3, activatedAt: "2026-09-11T01:14:46Z" }),
    });
    vi.stubGlobal("fetch", fetchMock);

    const active = await getActiveTemplate("token-local");

    expect(active.templateId).toBe(3);
    expect(fetchMock).toHaveBeenCalledWith("http://localhost:5080/api/evaluation-templates/active", {
      headers: { Authorization: "Bearer token-local" },
    });
  });

  it("reports no active template as templateId null", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
      ok: true, json: async () => ({ templateId: null, activatedAt: null }),
    }));

    const active = await getActiveTemplate("token-local");

    expect(active.templateId).toBeNull();
  });

  it("activates a published template with no request body", async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true, json: async () => ({ templateId: 9, activatedAt: "2026-09-11T02:00:00Z" }),
    });
    vi.stubGlobal("fetch", fetchMock);

    const activated = await activateTemplate("token-local", 9);

    expect(activated.templateId).toBe(9);
    expect(fetchMock).toHaveBeenCalledWith("http://localhost:5080/api/evaluation-templates/9/activate", {
      method: "POST",
      headers: { Authorization: "Bearer token-local" },
    });
  });

  it("maps a 409 on activation (unpublished or band mismatch) to the backend's message", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
      ok: false, status: 409, json: async () => ({ message: "Solo una plantilla publicada puede activarse para evaluaciones." }),
    }));

    await expect(activateTemplate("token-local", 9)).rejects.toThrow(/solo una plantilla publicada puede activarse/i);
  });
});
