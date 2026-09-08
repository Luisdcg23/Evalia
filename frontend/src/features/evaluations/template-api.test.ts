import { afterEach, expect, it, vi } from "vitest";
import { createTemplateItem } from "./template-api";

afterEach(() => vi.unstubAllGlobals());

it("creates a hierarchy item preserving its parent and scoring configuration", async () => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
    ok: true,
    json: async () => ({ id: 7, templateId: 2, parentId: 3, code: "1.1", description: "Ubicación", itemType: "SECTION" }),
  }));

  await createTemplateItem("token", 2, {
    parentId: 3, code: "1.1", description: " Ubicación ", itemType: "SECTION", order: 1,
    weight: null, isRequired: false, isCritical: false, allowsNotApplicable: true, responseType: null,
  });

  expect(fetch).toHaveBeenCalledWith("http://localhost:5080/api/evaluation-templates/2/items", expect.any(Object));
  const request = vi.mocked(fetch).mock.calls[0][1] as RequestInit;
  expect(JSON.parse(request.body as string)).toEqual(expect.objectContaining({
    parentId: 3, description: "Ubicación", itemType: "SECTION",
  }));
});
