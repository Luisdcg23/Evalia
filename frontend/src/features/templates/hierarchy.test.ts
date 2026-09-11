import { describe, expect, it } from "vitest";
import { allowedChildTypes, buildItemTree, flattenTree, isValidItemType, type TemplateItemLike } from "./hierarchy";

describe("allowedChildTypes", () => {
  it("only allows CHAPTER at the root of the template", () => {
    expect(allowedChildTypes(null)).toEqual(["CHAPTER"]);
  });

  it("follows capítulo → sección → subsección → (agrupador) → pregunta", () => {
    expect(allowedChildTypes("CHAPTER")).toEqual(["SECTION"]);
    expect(allowedChildTypes("SECTION")).toEqual(["SUBSECTION"]);
    expect(allowedChildTypes("SUBSECTION")).toEqual(["GROUP", "QUESTION"]);
    expect(allowedChildTypes("GROUP")).toEqual(["QUESTION"]);
  });

  it("treats a question as a leaf with no valid children", () => {
    expect(allowedChildTypes("QUESTION")).toEqual([]);
  });

  it("is case-insensitive on the parent type", () => {
    expect(allowedChildTypes("chapter")).toEqual(["SECTION"]);
  });
});

describe("isValidItemType", () => {
  it("accepts a section under a chapter", () => {
    expect(isValidItemType("SECTION", "CHAPTER")).toBe(true);
  });

  it("rejects a question directly under a chapter (skipping levels)", () => {
    expect(isValidItemType("QUESTION", "CHAPTER")).toBe(false);
  });

  it("rejects a second chapter nested under another chapter", () => {
    expect(isValidItemType("CHAPTER", "CHAPTER")).toBe(false);
  });

  it("only accepts a chapter at the root", () => {
    expect(isValidItemType("CHAPTER", null)).toBe(true);
    expect(isValidItemType("SECTION", null)).toBe(false);
  });

  it("accepts both group and question directly under a subsection", () => {
    expect(isValidItemType("GROUP", "SUBSECTION")).toBe(true);
    expect(isValidItemType("QUESTION", "SUBSECTION")).toBe(true);
  });
});

interface FakeItem extends TemplateItemLike {
  label: string;
}

const ITEMS: FakeItem[] = [
  { id: 1, parentId: null, order: 20, code: "2", label: "Capítulo 2" },
  { id: 2, parentId: null, order: 10, code: "1", label: "Capítulo 1" },
  { id: 3, parentId: 2, order: 10, code: "1.1", label: "Sección 1.1" },
  { id: 4, parentId: 3, order: 10, code: "1.1.1", label: "Subsección 1.1.1" },
  { id: 5, parentId: 4, order: 20, code: "1.1.1.2", label: "Pregunta 2" },
  { id: 6, parentId: 4, order: 10, code: "1.1.1.1", label: "Pregunta 1" },
];

describe("buildItemTree", () => {
  it("nests items under their parentId and orders roots by their `order` field", () => {
    const tree = buildItemTree(ITEMS);

    expect(tree.map(node => node.item.label)).toEqual(["Capítulo 1", "Capítulo 2"]);
    expect(tree[0].children.map(node => node.item.label)).toEqual(["Sección 1.1"]);
    expect(tree[0].children[0].children[0].item.label).toBe("Subsección 1.1.1");
  });

  it("orders siblings by `order` before falling back to natural code comparison", () => {
    const tree = buildItemTree(ITEMS);
    const questions = tree[0].children[0].children[0].children;

    expect(questions.map(node => node.item.label)).toEqual(["Pregunta 1", "Pregunta 2"]);
  });

  it("assigns increasing depth per level", () => {
    const tree = buildItemTree(ITEMS);

    expect(tree[0].depth).toBe(0);
    expect(tree[0].children[0].depth).toBe(1);
    expect(tree[0].children[0].children[0].depth).toBe(2);
    expect(tree[0].children[0].children[0].children[0].depth).toBe(3);
  });

  it("returns an empty tree for an empty item list", () => {
    expect(buildItemTree([])).toEqual([]);
  });
});

describe("flattenTree", () => {
  it("walks the tree in preorder, listing a parent right before its children", () => {
    const flat = flattenTree(buildItemTree(ITEMS));

    expect(flat.map(entry => entry.item.label)).toEqual([
      "Capítulo 1", "Sección 1.1", "Subsección 1.1.1", "Pregunta 1", "Pregunta 2", "Capítulo 2",
    ]);
    expect(flat.map(entry => entry.depth)).toEqual([0, 1, 2, 3, 3, 0]);
  });
});
