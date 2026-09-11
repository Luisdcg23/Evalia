import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { createDebouncer, PerItemDebouncer } from "./autosave";

beforeEach(() => vi.useFakeTimers());
afterEach(() => vi.useRealTimers());

describe("createDebouncer", () => {
  it("collapses several rapid calls into a single invocation with the latest arguments", () => {
    const fn = vi.fn();
    const debounced = createDebouncer(fn, 500);

    debounced("a");
    vi.advanceTimersByTime(100);
    debounced("b");
    vi.advanceTimersByTime(100);
    debounced("c");
    vi.advanceTimersByTime(499);
    expect(fn).not.toHaveBeenCalled();

    vi.advanceTimersByTime(1);
    expect(fn).toHaveBeenCalledTimes(1);
    expect(fn).toHaveBeenCalledWith("c");
  });

  it("runs again after the delay elapses for a separate burst", () => {
    const fn = vi.fn();
    const debounced = createDebouncer(fn, 200);

    debounced(1);
    vi.advanceTimersByTime(200);
    debounced(2);
    vi.advanceTimersByTime(200);

    expect(fn).toHaveBeenCalledTimes(2);
    expect(fn).toHaveBeenNthCalledWith(1, 1);
    expect(fn).toHaveBeenNthCalledWith(2, 2);
  });

  it("cancel discards a pending call", () => {
    const fn = vi.fn();
    const debounced = createDebouncer(fn, 300);
    debounced("x");
    debounced.cancel();
    vi.advanceTimersByTime(1000);
    expect(fn).not.toHaveBeenCalled();
  });

  it("flush runs a pending call immediately with the latest arguments", () => {
    const fn = vi.fn();
    const debounced = createDebouncer(fn, 300);
    debounced("first");
    debounced("second");
    debounced.flush();
    expect(fn).toHaveBeenCalledTimes(1);
    expect(fn).toHaveBeenCalledWith("second");

    vi.advanceTimersByTime(1000);
    expect(fn).toHaveBeenCalledTimes(1);
  });

  it("flush without a pending call does nothing", () => {
    const fn = vi.fn();
    const debounced = createDebouncer(fn, 300);
    debounced.flush();
    expect(fn).not.toHaveBeenCalled();
  });
});

describe("PerItemDebouncer", () => {
  it("keeps an independent timer per item so editing one does not reset another", () => {
    const fn = vi.fn();
    const grouped = new PerItemDebouncer<[string]>(fn, 400);

    grouped.schedule(1, "respuesta item 1");
    vi.advanceTimersByTime(200);
    grouped.schedule(2, "respuesta item 2");
    vi.advanceTimersByTime(200);
    // El item 1 ya cumplió sus 400ms sin nuevas ediciones; el item 2 lleva solo 200ms.
    expect(fn).toHaveBeenCalledTimes(1);
    expect(fn).toHaveBeenCalledWith(1, "respuesta item 1");

    vi.advanceTimersByTime(200);
    expect(fn).toHaveBeenCalledTimes(2);
    expect(fn).toHaveBeenCalledWith(2, "respuesta item 2");
  });

  it("flushAll saves every pending item immediately, useful before submitting", () => {
    const fn = vi.fn();
    const grouped = new PerItemDebouncer<[string]>(fn, 1000);

    grouped.schedule(10, "a");
    grouped.schedule(11, "b");
    grouped.flushAll();

    expect(fn).toHaveBeenCalledTimes(2);
    expect(fn).toHaveBeenCalledWith(10, "a");
    expect(fn).toHaveBeenCalledWith(11, "b");

    vi.advanceTimersByTime(5000);
    expect(fn).toHaveBeenCalledTimes(2);
  });

  it("cancelAll drops every pending item without running them", () => {
    const fn = vi.fn();
    const grouped = new PerItemDebouncer<[string]>(fn, 500);
    grouped.schedule(1, "x");
    grouped.cancelAll();
    vi.advanceTimersByTime(5000);
    expect(fn).not.toHaveBeenCalled();
  });
});
