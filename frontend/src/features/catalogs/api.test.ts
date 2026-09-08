import { afterEach, expect, it, vi } from "vitest";
import { createRiskLevel } from "./api";

afterEach(() => vi.unstubAllGlobals());

it("creates a numeric risk level through the authenticated catalog endpoint", async () => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
    ok: true,
    json: async () => ({ id: 3, name: "Alto", points: 3 }),
  }));

  const created = await createRiskLevel("token-local", { name: " Alto ", points: 3 });

  expect(created.points).toBe(3);
  expect(fetch).toHaveBeenCalledWith("http://localhost:5080/api/catalogs/risk-levels", expect.objectContaining({
    body: JSON.stringify({ name: "Alto", points: 3 }),
  }));
});
