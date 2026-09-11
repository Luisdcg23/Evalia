/**
 * Debounce genérico para el autosave de la captura en campo (RF-13): agrupa ediciones rápidas
 * (p. ej. cada tecla en observaciones/comentarios) en una sola llamada por pregunta, en vez de
 * disparar una petición por cambio. Es una función pura, sin dependencias de React, para poder
 * probarla con temporizadores simulados.
 */
export interface Debounced<Args extends unknown[]> {
  (...args: Args): void;
  /** Cancela cualquier llamada pendiente sin ejecutarla. */
  cancel: () => void;
  /** Si hay una llamada pendiente, la ejecuta de inmediato con los últimos argumentos y cancela el temporizador. */
  flush: () => void;
}

export function createDebouncer<Args extends unknown[]>(fn: (...args: Args) => void, delayMs: number): Debounced<Args> {
  let timer: ReturnType<typeof setTimeout> | null = null;
  let pendingArgs: Args | null = null;

  const debounced = ((...args: Args) => {
    pendingArgs = args;
    if (timer) clearTimeout(timer);
    timer = setTimeout(() => {
      timer = null;
      const toRun = pendingArgs;
      pendingArgs = null;
      if (toRun) fn(...toRun);
    }, delayMs);
  }) as Debounced<Args>;

  debounced.cancel = () => {
    if (timer) clearTimeout(timer);
    timer = null;
    pendingArgs = null;
  };

  debounced.flush = () => {
    if (timer) clearTimeout(timer);
    timer = null;
    const toRun = pendingArgs;
    pendingArgs = null;
    if (toRun) fn(...toRun);
  };

  return debounced;
}

/**
 * Mantiene un debouncer independiente por pregunta (`itemId`): editar la pregunta 3 no debe
 * reiniciar el temporizador de la pregunta 1. `flushAll` se usa antes de enviar la evaluación,
 * para no dejar una edición reciente sin guardar.
 */
export class PerItemDebouncer<Args extends unknown[]> {
  private readonly debouncers = new Map<number, Debounced<Args>>();

  constructor(private readonly fn: (itemId: number, ...args: Args) => void, private readonly delayMs: number) {}

  schedule(itemId: number, ...args: Args): void {
    let debounced = this.debouncers.get(itemId);
    if (!debounced) {
      debounced = createDebouncer((...innerArgs: Args) => this.fn(itemId, ...innerArgs), this.delayMs);
      this.debouncers.set(itemId, debounced);
    }
    debounced(...args);
  }

  flushAll(): void {
    for (const debounced of this.debouncers.values()) debounced.flush();
  }

  cancelAll(): void {
    for (const debounced of this.debouncers.values()) debounced.cancel();
  }
}
