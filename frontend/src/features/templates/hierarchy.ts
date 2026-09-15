/**
 * Reglas de jerarquía de la plantilla de evaluación: capítulo → sección → subsección →
 * (agrupador, opcional) → pregunta. Estas funciones son puras para poder probarlas sin
 * depender de la API ni de React.
 */
export const TEMPLATE_ITEM_TYPES = ["CHAPTER", "SECTION", "SUBSECTION", "GROUP", "QUESTION"] as const;
export type TemplateItemType = (typeof TEMPLATE_ITEM_TYPES)[number];

export const TEMPLATE_ITEM_TYPE_LABEL: Record<TemplateItemType, string> = {
  CHAPTER: "Capítulo",
  SECTION: "Sección",
  SUBSECTION: "Subsección",
  GROUP: "Agrupador",
  QUESTION: "Pregunta",
};

const ROOT = "__ROOT__" as const;
type HierarchyKey = TemplateItemType | typeof ROOT;

const CHILD_TYPES: Record<HierarchyKey, readonly TemplateItemType[]> = {
  [ROOT]: ["CHAPTER"],
  CHAPTER: ["SECTION"],
  SECTION: ["SUBSECTION"],
  SUBSECTION: ["GROUP", "QUESTION"],
  GROUP: ["QUESTION"],
  QUESTION: [],
};

function normalize(itemType: string | null): HierarchyKey {
  if (itemType === null) return ROOT;
  const upper = itemType.trim().toUpperCase();
  return (TEMPLATE_ITEM_TYPES as readonly string[]).includes(upper) ? (upper as TemplateItemType) : ROOT;
}

/** Tipos de ítem permitidos como hijos directos de `parentType` (`null` = raíz de la plantilla). */
export function allowedChildTypes(parentType: string | null): readonly TemplateItemType[] {
  return CHILD_TYPES[normalize(parentType)] ?? [];
}

/** ¿Es coherente crear/editar un ítem de tipo `itemType` bajo un padre de tipo `parentType`? */
export function isValidItemType(itemType: string, parentType: string | null): boolean {
  const upper = itemType.trim().toUpperCase();
  return allowedChildTypes(parentType).includes(upper as TemplateItemType);
}

export interface TemplateItemLike {
  id: number;
  parentId?: number | null;
  order: number;
  code: string;
}

export interface TemplateTreeNode<T extends TemplateItemLike> {
  item: T;
  depth: number;
  children: Array<TemplateTreeNode<T>>;
}

/** Arma el árbol jerárquico a partir de la lista plana que devuelve la API (usa `parentId`). */
export function buildItemTree<T extends TemplateItemLike>(items: readonly T[]): Array<TemplateTreeNode<T>> {
  const byParent = new Map<number | null, T[]>();
  for (const item of items) {
    const key = item.parentId ?? null;
    const siblings = byParent.get(key);
    if (siblings) siblings.push(item);
    else byParent.set(key, [item]);
  }
  const sortSiblings = (list: T[]) =>
    [...list].sort((a, b) => a.order - b.order || a.code.localeCompare(b.code, "es", { numeric: true }));

  const visit = (parentId: number | null, depth: number): Array<TemplateTreeNode<T>> =>
    sortSiblings(byParent.get(parentId) ?? []).map(item => ({
      item,
      depth,
      children: visit(item.id, depth + 1),
    }));

  return visit(null, 0);
}

/** Recorrido en preorden del árbol, útil para pintar una lista indentada. */
export function flattenTree<T extends TemplateItemLike>(
  nodes: ReadonlyArray<TemplateTreeNode<T>>,
): Array<{ item: T; depth: number }> {
  const result: Array<{ item: T; depth: number }> = [];
  const visit = (list: ReadonlyArray<TemplateTreeNode<T>>) => {
    for (const node of list) {
      result.push({ item: node.item, depth: node.depth });
      visit(node.children);
    }
  };
  visit(nodes);
  return result;
}
