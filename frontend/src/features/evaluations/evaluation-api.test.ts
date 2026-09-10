import { afterEach, expect, it, vi } from "vitest";
import { saveResponse } from "./evaluation-api";

afterEach(() => vi.unstubAllGlobals());

it("saves the answer of a question against its evaluation", async () => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: true, json: async () => ({ id: 31 }) }));

  await saveResponse("token", { evaluationId: 4, itemId: 12, optionCode: "CP", observations: "Sin registro", comments: "" });

  expect(fetch).toHaveBeenCalledWith("http://localhost:5080/api/evaluations/4/responses/12", expect.objectContaining({ method: "PUT" }));
  const request = vi.mocked(fetch).mock.calls[0][1] as RequestInit;
  expect(JSON.parse(request.body as string)).toEqual({ optionCode: "CP", observations: "Sin registro", comments: "" });
});
