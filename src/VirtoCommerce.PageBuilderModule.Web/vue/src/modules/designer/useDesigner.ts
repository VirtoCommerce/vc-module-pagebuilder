import { computed, reactive, ref, shallowRef, watch } from "vue";
import { useManualRefHistory } from "@vueuse/core";
import { debounce } from "lodash-es";
import type { Field, Item, ItemSchema, PageDocument, Schemas, SectionGroup, Selection } from "./types";
import { blockSchemaOf, copyItem, fieldsOf, itemTitle, newId, newItem, withTextEdit } from "./schema";
import { fromDocument, isComponentRef, resolveRefs } from "./document";
import type { ClipboardItem } from "./clipboard";
import * as api from "./api";
import { GROUPS, groupItems, groupOf, pageOnly, withGroups } from "./groups";
import { THEME_FILES, THEME_PARTS, groupPath, type HistoryFile, type ThemePart } from "./history";
import {
  MAIN_SECTIONS,
  PAGE_CONTENT,
  hidingLosesMain,
  keptMainSection,
  mainSectionState,
  pageTypeOf,
  startingTemplate,
  templateEntry,
  templateKeyOf,
} from "./templates";
import { resolveTranslations, type Translating } from "./translatable";
import { moveItem } from "./rules";
import type { Capability, Handshake } from "./previewMessages";
import { hasThemeDraft, isNewThemeDraft, templateStatus, type ThemeStatus } from "./themePublication";
import { CatalogPendingError, type DesignerDiscardPage, type DesignerSaveFile, type DesignerSavePage } from "./designerSave";

const EMPTY_SCHEMAS: Schemas = { sections: {}, blocks: {}, shared: {}, objects: {} };
export interface PageRow {
  key: string;
  name: string;
  icon?: string;
  fields: Field[];
  position: "top" | "bottom";
}

/** Every edit replaces the parts it changes, so the undo history and the preview see it. */
export function useDesigner() {
  const groupId = ref<string>();
  const storeId = ref<string>();
  const file = ref<api.FilePage>();
  const shared = ref<api.SharedComponent>();
  const gitFlow = ref(false);
  const theme = ref<string>();
  const groupsLoaded = ref(false);
  const savedGroupsJson = ref<Record<SectionGroup, string>>({ header: "[]", footer: "[]" });
  const doc = ref<PageDocument>({ settings: {} as Item, content: [] });
  const schemas = shallowRef<Schemas>(EMPTY_SCHEMAS);
  const handshake = shallowRef<Handshake>();
  const can = (capability: Capability) => !!handshake.value?.capabilities.includes(capability);
  const selection = ref<Selection | null>(null);
  const hovered = ref<string | null>(null);

  const eTag = ref<string>();
  const themeETags = ref<Record<string, string>>({});
  const savedJson = ref("");
  const status = ref<api.PublishStatus>({});
  const busy = ref<"loading" | "saving" | "publishing" | "restoring">();
  const loadError = ref<unknown>();

  // A snapshot is taken once edits pause, so typing makes one undo step.
  const history = useManualRefHistory(doc, { clone: true, capacity: 100 });
  const record = debounce(() => {
    if (JSON.stringify(history.last.value.snapshot) !== JSON.stringify(doc.value)) history.commit();
  }, 400);
  watch(doc, record, { deep: true });
  // An edit still waiting for the pause counts, so it is recorded first.
  const canUndo = computed(() => history.canUndo.value || JSON.stringify(doc.value) !== JSON.stringify(history.last.value.snapshot));
  const canRedo = computed(() => history.canRedo.value);
  function undo() {
    record.flush();
    history.undo();
    settleSelection();
  }
  function redo() {
    record.flush();
    history.redo();
    settleSelection();
  }
  function settleSelection() {
    checked.value = checked.value.filter((id) => !!findSection(id));
    const sel = selection.value;
    if (!sel || sel.kind === "page") return;
    const section = findSection(sel.kind === "block" ? sel.sectionId : sel.id);
    if (!section) selection.value = null;
    else if (sel.kind === "block" && !section.blocks?.some((b) => b.id === sel.id)) selection.value = { kind: "section", id: section.id };
  }
  const pageDirty = computed(() => JSON.stringify(pageOnly(doc.value)) !== savedJson.value);
  const changedGroups = (): Partial<Record<SectionGroup, Item[]>> =>
    groupsLoaded.value
      ? Object.fromEntries(GROUPS.map((g) => [g, groupItems(doc.value, g)] as const).filter(([g, items]) => JSON.stringify(items) !== savedGroupsJson.value[g]))
      : {};
  // What could not be opened has no edits: its document is still the one open before, or none.
  const dirty = computed(() => !loadError.value && (pageDirty.value || Object.keys(changedGroups()).length > 0));
  const changedParts = computed<HistoryFile[]>(() =>
    loadError.value ? [] : [...(pageDirty.value ? (["page"] as const) : []), ...(Object.keys(changedGroups()) as SectionGroup[])],
  );

  const sectionSchema = (type?: string): ItemSchema | undefined => (type ? schemas.value.sections[type] : undefined);
  const isAtLimit = (type: string) => {
    const limit = sectionSchema(type)?.limit;
    return typeof limit === "number" && doc.value.content.filter((x) => x.type === type).length >= limit;
  };
  const blockSchema = (type?: string): ItemSchema | undefined => (type ? blockSchemaOf(schemas.value, type) : undefined);

  const templateKey = computed(() => (file.value?.type === "themes" ? templateKeyOf(file.value.path) : undefined));
  const pageType = computed(() => (templateKey.value ? pageTypeOf(templateKey.value) : undefined));
  /** A shared component cannot hold another, and the storefront reads a template as it is. */
  const canPlaceShared = computed(() => !shared.value && !templateKey.value);

  const languages = ref<{ all: string[]; default?: string }>({ all: [] });
  const language = ref<string>();
  const translates = (section?: Item) => !shared.value && (!!templateKey.value || !!section?.__group);
  function translating(section?: Item, edited = language.value): Translating | undefined {
    const defaultLanguage = languages.value.default;
    return translates(section) && edited && defaultLanguage ? { language: edited, defaultLanguage, languages: languages.value.all } : undefined;
  }

  function inEditedLanguage<T>(value: T): T {
    const defaultLanguage = languages.value.default ?? "";
    return resolveTranslations(value, language.value ?? defaultLanguage, defaultLanguage);
  }

  /** A page's sections and shared components keep one text, so a dictionary becomes the edited language's text. */
  function settled(item: Item, section: Item = item): Item {
    return translates(section) ? item : inEditedLanguage(item);
  }

  const mainSection = computed(() => {
    const type = pageType.value;
    const state = mainSectionState(doc.value.content, type);
    return type && state ? { type: MAIN_SECTIONS[type], state } : undefined;
  });
  const MAIN_LOCKED = "PAGE_BUILDER.DESIGNER.MAIN_SECTION.LOCKED";
  const isMainKept = (id: string) => !!keptMainSection(doc.value.content, pageType.value, [id]);
  const isMainShown = (id: string) => hidingLosesMain(doc.value.content, pageType.value, id);

  const template = computed(() => templateEntry(schemas.value, pageType.value));
  const extraPageFields = ref<Field[]>([]);

  const addableSections = (fits: (type: string, schema: ItemSchema) => boolean) =>
    Object.entries(schemas.value.sections)
      .filter(([type, schema]) => !schema.hidden && !isAtLimit(type) && fits(type, schema))
      .map(([type, schema]) => ({ type, schema }))
      .sort((a, b) => (a.schema.name ?? a.type).localeCompare(b.schema.name ?? b.type));

  const sectionTypes = computed(() => {
    const { key, entry } = template.value;
    const pageContent = pageType.value === "page" && !doc.value.content.some((x) => x.type === PAGE_CONTENT);
    return addableSections(
      (type, schema) =>
        !schema.static &&
        !schema.groups?.length &&
        (type !== PAGE_CONTENT || pageContent) &&
        (entry.sections ? entry.sections.includes(type) : !schema.targetTemplates?.length || schema.targetTemplates.includes(key)),
    );
  });

  const groupSectionTypes = (group: SectionGroup) => addableSections((_, schema) => !!schema.groups?.includes(group));

  const pageEnd = () => {
    const footer = doc.value.content.findIndex((x) => groupOf(x) === "footer");
    return footer < 0 ? doc.value.content.length : footer;
  };
  const groupEnd = (group: SectionGroup) => (group === "header" ? doc.value.content.filter((x) => groupOf(x) === "header").length : doc.value.content.length);

  const pageRows = computed<PageRow[]>(() => {
    const rows: PageRow[] = [];
    if (shared.value) return rows;
    const described = template.value.entry.settings ?? [];
    const own = [...described, ...extraPageFields.value.filter((x) => !described.some((d) => d.id === x.id))];
    if (own.length) rows.push({ key: "settings", name: "", icon: "lucide-file-cog", fields: own, position: "top" });
    for (const [type, schema] of Object.entries(schemas.value.sections)) {
      if (schema.static) rows.push({ key: type, name: schema.name ?? type, icon: schema.icon, fields: schema.settings ?? [], position: schema.static === "bottom" ? "bottom" : "top" });
    }
    return rows;
  });

  const findSection = (id: string) => doc.value.content.find((x) => x.id === id);
  const selectedSection = computed(() => {
    const sel = selection.value;
    if (!sel || sel.kind === "page") return undefined;
    return findSection(sel.kind === "block" ? sel.sectionId : sel.id);
  });
  const selectedItem = computed(() => {
    const sel = selection.value;
    if (sel?.kind === "section") return findSection(sel.id);
    if (sel?.kind === "block") return findSection(sel.sectionId)?.blocks?.find((x) => x.id === sel.id);
    return undefined;
  });
  const selectedFields = computed(() => {
    const sel = selection.value;
    if (!sel) return [];
    if (sel.kind === "page") return pageRows.value.find((x) => x.key === sel.key)?.fields ?? [];
    const item = selectedItem.value;
    if (!item || isComponentRef(item)) return [];
    if (sel.kind === "section") return fieldsOf(sectionSchema(item.type), "section", schemas.value);
    return fieldsOf(blockSchema(item.type), "block", schemas.value);
  });

  function setContent(content: Item[]) {
    doc.value = { ...doc.value, content };
  }
  function updateSection(id: string, change: (section: Item) => Item) {
    setContent(doc.value.content.map((x) => (x.id === id ? change(x) : x)));
  }

  function setField(id: string, value: unknown) {
    const sel = selection.value;
    if (!sel) return;
    if (sel.kind === "page") doc.value = { ...doc.value, settings: { ...doc.value.settings, [id]: value } };
    else if (sel.kind === "section") updateSection(sel.id, (x) => ({ ...x, [id]: value }));
    else updateSection(sel.sectionId, (x) => ({ ...x, blocks: x.blocks?.map((b) => (b.id === sel.id ? { ...b, [id]: value } : b)) }));
  }

  let textEditRefused = false;
  function textEdited(item: Item, kind: "section" | "block", section: Item, path: (string | number)[], text: string, cultureName?: string): Item | undefined {
    const fields = fieldsOf(kind === "section" ? sectionSchema(item.type) : blockSchema(item.type), kind, schemas.value);
    const changed = withTextEdit(item, fields, path, text, cultureName ? translating(section, cultureName) : undefined);
    if (!changed && !textEditRefused) {
      textEditRefused = true;
      console.warn("A text edited in the preview was ignored: its schema has no text setting there", item.type, path);
    }
    return changed;
  }

  function setText(itemId: string, path: (string | number)[], text: string, cultureName?: string): Selection | undefined {
    for (const section of doc.value.content) {
      if (isLocked(section.id) && (section.id === itemId || section.blocks?.some((b) => b.id === itemId))) return undefined;
      if (section.id === itemId) {
        const changed = textEdited(section, "section", section, path, text, cultureName);
        if (!changed) return undefined;
        updateSection(section.id, () => changed);
        return { kind: "section", id: section.id };
      }
      const block = section.blocks?.find((b) => b.id === itemId);
      if (block) {
        const changed = textEdited(block, "block", section, path, text, cultureName);
        if (!changed) return undefined;
        updateSection(section.id, (x) => ({ ...x, blocks: x.blocks?.map((b) => (b.id === itemId ? changed : b)) }));
        return { kind: "block", sectionId: section.id, id: itemId };
      }
    }
    return undefined;
  }

  function addSection(type: string, group?: SectionGroup) {
    const section: Item = { ...newItem(type, "section", schemas.value), ...(group ? { __group: group } : {}) };
    const content = [...doc.value.content];
    content.splice(group ? groupEnd(group) : pageEnd(), 0, section);
    setContent(content);
    selection.value = { kind: "section", id: section.id };
  }

  function addBlock(sectionId: string, type: string) {
    const block = newItem(type, "block", schemas.value);
    updateSection(sectionId, (x) => ({ ...x, blocks: [...(x.blocks ?? []), block] }));
    selection.value = { kind: "block", sectionId, id: block.id };
  }

  function moveSection(from: number, to: number) {
    setContent(moveItem(doc.value.content, from, to));
  }

  const placementId = () => newId("componentRef") + newId("");
  const newPlacement = (componentRef: string): Item => ({ id: placementId(), type: "componentRef", componentRef });

  function duplicate(sel: Selection) {
    if (sel.kind === "section") {
      const index = doc.value.content.findIndex((x) => x.id === sel.id);
      if (index < 0) return;
      const original = doc.value.content[index];
      if ((isComponentRef(original) && !canPlaceShared.value) || isAtLimit(original.type)) return;
      const copy = isComponentRef(original) ? { ...original, id: placementId() } : copyItem(original);
      const content = [...doc.value.content];
      content.splice(index + 1, 0, copy);
      setContent(content);
      selection.value = { kind: "section", id: copy.id };
    } else if (sel.kind === "block") {
      const blocks = findSection(sel.sectionId)?.blocks ?? [];
      const index = blocks.findIndex((b) => b.id === sel.id);
      if (index < 0) return;
      const copy = copyItem(blocks[index]);
      updateSection(sel.sectionId, (x) => ({ ...x, blocks: [...blocks.slice(0, index + 1), copy, ...blocks.slice(index + 1)] }));
      selection.value = { kind: "block", sectionId: sel.sectionId, id: copy.id };
    }
  }

  function remove(sel: Selection): string | undefined {
    if (sel.kind === "page") return undefined;
    if (sel.kind === "section" && isMainKept(sel.id)) return MAIN_LOCKED;
    if (sel.kind === "section") setContent(doc.value.content.filter((x) => x.id !== sel.id));
    else updateSection(sel.sectionId, (x) => ({ ...x, blocks: x.blocks?.filter((b) => b.id !== sel.id) }));
    const current = selection.value;
    if (current && current.kind !== "page" && (current.id === sel.id || (current.kind === "block" && current.sectionId === sel.id))) {
      selection.value = sel.kind === "block" ? { kind: "section", id: sel.sectionId } : null;
    }
    return undefined;
  }

  function restoreMainSection() {
    const main = mainSection.value;
    if (!main || main.state === "shown") return;
    const hidden = doc.value.content.find((x) => !x.__group && x.type === main.type);
    if (hidden) {
      updateSection(hidden.id, (x) => ({ ...x, hidden: false }));
      selection.value = { kind: "section", id: hidden.id };
    } else addSection(main.type);
  }

  function paste(copied: ClipboardItem): string | undefined {
    const sel = selection.value;
    if (copied.type === "section") {
      if (isComponentRef(copied.content) && !canPlaceShared.value) return "PAGE_BUILDER.DESIGNER.SHARED_ON_PAGES_ONLY";
      if (isAtLimit(copied.content.type)) return "PAGE_BUILDER.DESIGNER.CLIPBOARD.LIMIT";
      const copy: Item = isComponentRef(copied.content) ? { ...copied.content, id: placementId() } : copyItem(copied.content);
      let target: Item | undefined;
      if (sel?.kind === "block") target = findSection(sel.sectionId);
      else if (sel?.kind === "section") target = findSection(sel.id);
      const group = target && groupOf(target);
      const groups = sectionSchema(copy.type)?.groups;
      const fits = !group || !!groups?.includes(group);
      if (!(group && fits) && groups?.length) return "PAGE_BUILDER.DESIGNER.CLIPBOARD.GROUP_ONLY";
      if (group && fits) copy.__group = group;
      else delete copy.__group;
      const item = settled(copy);
      const at = target && fits ? doc.value.content.indexOf(target) + 1 : pageEnd();
      const content = [...doc.value.content];
      content.splice(at, 0, item);
      setContent(content);
      selection.value = { kind: "section", id: item.id };
      void loadComponents();
      return undefined;
    }
    if (!sel || sel.kind === "page") return "PAGE_BUILDER.DESIGNER.CLIPBOARD.BLOCK_NEEDS_SECTION";
    const sectionId = sel.kind === "block" ? sel.sectionId : sel.id;
    const section = findSection(sectionId);
    if (!section || !sectionSchema(section.type)?.blocks?.includes(copied.content.type)) return "PAGE_BUILDER.DESIGNER.CLIPBOARD.BLOCK_NOT_ALLOWED";
    const block = settled(copyItem(copied.content), section);
    const blocks = [...(section.blocks ?? [])];
    blocks.splice(sel.kind === "block" ? blocks.findIndex((b) => b.id === sel.id) + 1 : blocks.length, 0, block);
    updateSection(sectionId, (x) => ({ ...x, blocks }));
    selection.value = { kind: "block", sectionId, id: block.id };
    return undefined;
  }

  const checked = ref<string[]>([]);
  // Select mode picks among the page's own sections, so it ends once none are left.
  const selecting = ref(false);
  watch(
    () => doc.value.content.some((x) => !x.__group),
    (hasSections) => {
      if (!hasSections) selecting.value = false;
    },
  );
  function toggleChecked(id: string) {
    checked.value = checked.value.includes(id) ? checked.value.filter((x) => x !== id) : [...checked.value, id];
  }

  function removeChecked(): string | undefined {
    const kept = keptMainSection(doc.value.content, pageType.value, checked.value);
    const ids = new Set(checked.value.filter((id) => id !== kept));
    setContent(doc.value.content.filter((x) => !ids.has(x.id)));
    const sel = selection.value;
    if (sel && sel.kind !== "page" && ids.has(sel.kind === "block" ? sel.sectionId : sel.id)) selection.value = null;
    checked.value = [];
    return kept ? MAIN_LOCKED : undefined;
  }

  async function saveCheckedAsShared(name: string): Promise<string | undefined> {
    if (!canPlaceShared.value) return "PAGE_BUILDER.DESIGNER.SHARED_ON_PAGES_ONLY";
    const indexes = checked.value.map((id) => doc.value.content.findIndex((x) => x.id === id)).sort((a, b) => a - b);
    const sections = indexes.map((i) => doc.value.content[i]);
    if (!sections.length || indexes.some((i, n) => i < 0 || (n > 0 && i !== indexes[n - 1] + 1))) return "PAGE_BUILDER.DESIGNER.SELECT_MODE.NOT_ADJACENT";
    if (sections.some(isComponentRef)) return "PAGE_BUILDER.DESIGNER.SELECT_MODE.HAS_SHARED";
    // A shared component keeps no texts per language.
    const content = inEditedLanguage(sections);
    const component = await api.createSharedComponent(storeId.value!, name, { settings: { type: "settings" } as Item, content });
    const placement = newPlacement(component.id);
    const rest = doc.value.content.filter((x) => !checked.value.includes(x.id));
    rest.splice(indexes[0], 0, placement);
    setContent(rest);
    checked.value = [];
    selection.value = { kind: "section", id: placement.id };
    await loadComponents();
    return undefined;
  }

  function toggleHidden(sel: Selection): string | undefined {
    if (sel.kind === "section" && isMainShown(sel.id)) return MAIN_LOCKED;
    if (sel.kind === "section") updateSection(sel.id, (x) => (isComponentRef(x) ? x : { ...x, hidden: !x.hidden }));
    else if (sel.kind === "block") updateSection(sel.sectionId, (x) => ({ ...x, blocks: x.blocks?.map((b) => (b.id === sel.id ? { ...b, hidden: !b.hidden } : b)) }));
    return undefined;
  }

  const components = reactive(new Map<string, { name?: string; usageCount?: number; content: Item[] } | null>());

  async function loadComponents(force = false) {
    const ids = new Set(doc.value.content.filter(isComponentRef).map((x) => x.componentRef));
    await Promise.all(
      [...ids]
        .filter((id) => force || !components.has(id))
        .map(async (id) => {
          try {
            const [info, content] = await Promise.all([api.sharedComponent(id), api.sharedComponentContent(id)]);
            components.set(id, { name: info.name, usageCount: info.usageCount, content: content.content });
          } catch {
            components.set(id, null);
          }
        }),
    );
  }

  async function insertShared(component: api.SharedComponent, index = pageEnd()) {
    if (!canPlaceShared.value) return;
    const placement = newPlacement(component.id);
    const content = [...doc.value.content];
    content.splice(index, 0, placement);
    setContent(content);
    selection.value = { kind: "section", id: placement.id };
    await loadComponents();
  }

  async function detach(id: string) {
    const placement = findSection(id);
    if (!placement || !isComponentRef(placement)) return;
    const content = (await api.sharedComponentContent(placement.componentRef)).content.map((x) => settled(copyItem(x)));
    const index = doc.value.content.findIndex((x) => x.id === id);
    if (index < 0) return;
    const next = [...doc.value.content];
    next.splice(index, 1, ...content);
    setContent(next);
    selection.value = content[0] ? { kind: "section", id: content[0].id } : null;
  }

  const sectionLabels = computed(() =>
    Object.fromEntries(
      doc.value.content.filter((x) => !isComponentRef(x) && !x.hidden).map((x) => [x.id, itemTitle(x, sectionSchema(x.type), languages.value.default)]),
    ),
  );

  const preview = computed(() =>
    resolveRefs(
      pageOnly(doc.value),
      new Map(
        [...components].map(([id, x]) => [
          id,
          x && { content: x.content, label: x.name },
        ]),
      ),
    ),
  );

  const groupsEditable = ref(true);

  const groupsShown = computed(() => groupsLoaded.value && can("groups") && groupsEditable.value);

  const isLocked = (sectionId: string) => !groupsEditable.value && doc.value.content.some((x) => x.id === sectionId && !!groupOf(x));

  const groupsPreview = computed(() => (groupsLoaded.value ? { header: groupItems(doc.value, "header"), footer: groupItems(doc.value, "footer") } : undefined));

  const currentGroups = () => Object.fromEntries(GROUPS.map((g) => [g, groupItems(doc.value, g)])) as Record<SectionGroup, Item[]>;

  async function loadGroups(store: string, themeName?: string) {
    unseeded.clear();
    try {
      theme.value = themeName ?? (await api.storeTheme(store));
      const [header, footer] = await Promise.all(GROUPS.map((g) => api.loadSectionGroup(store, theme.value!, g)));
      groupsLoaded.value = true;
      for (const [g, file] of [["header", header], ["footer", footer]] as const) {
        themeETags.value[groupPath(theme.value!, g)] = file.eTag;
        if (!file.items) unseeded.add(g);
      }
      return { header: header.items ?? [], footer: footer.items ?? [] };
    } catch {
      groupsLoaded.value = false;
      return undefined;
    }
  }

  // A theme without a group file still has a header and a footer, so the group starts as that section with nothing set.
  // Seeded only once the preview's schemas have it (an older storefront has none) and only while nothing is unsaved.
  const DEFAULT_SECTION: Record<SectionGroup, string> = { header: "site-header", footer: "site-footer" };
  const unseeded = new Set<SectionGroup>();
  function seedGroups() {
    const seeds = [...unseeded].filter((g) => schemas.value.sections[DEFAULT_SECTION[g]] && !groupItems(doc.value, g).length);
    if (!seeds.length || dirty.value) return;
    const groups = currentGroups();
    for (const g of seeds) {
      unseeded.delete(g);
      groups[g] = [{ id: newId(DEFAULT_SECTION[g]), type: DEFAULT_SECTION[g] }];
    }
    reset(pageOnly(doc.value), groups);
  }
  watch(schemas, seedGroups);

  function reset(next: PageDocument, groups: Partial<Record<SectionGroup, Item[]>> = currentGroups()) {
    record.cancel();
    doc.value = groupsLoaded.value ? withGroups(next, groups) : next;
    savedJson.value = JSON.stringify(pageOnly(doc.value));
    savedGroupsJson.value = { header: JSON.stringify(groupItems(doc.value, "header")), footer: JSON.stringify(groupItems(doc.value, "footer")) };
    // The baseline is the last commit, so commit the loaded page and then forget the steps.
    history.commit();
    history.clear();
  }

  // A template with no theme file opens as the storefront's built-in one, known only once the preview's schemas arrive.
  const templateIsNew = ref(false);
  let templateSeeded = false;
  const mainItem = (type: string) => newItem(type, "section", schemas.value);
  function seedTemplate() {
    const type = pageType.value;
    if (!templateIsNew.value || templateSeeded || !type || dirty.value || !Object.keys(schemas.value.sections).length) return;
    templateSeeded = true;
    reset(startingTemplate(type, undefined, template.value.entry, mainItem));
  }
  watch(schemas, seedTemplate);

  async function open(source: { groupId: string; storeId: string } | { sharedComponentId: string; storeId: string } | api.FilePage) {
    groupId.value = undefined;
    file.value = undefined;
    shared.value = undefined;
    eTag.value = undefined;
    themeETags.value = {};
    gitFlow.value = false;
    groupsLoaded.value = false;
    templateIsNew.value = false;
    templateSeeded = false;
    selection.value = null;
    hovered.value = null;
    checked.value = [];
    selecting.value = false;
    components.clear();
    status.value = {};
    storeId.value = source.storeId;
    busy.value = "loading";
    loadError.value = undefined;
    try {
      if ("groupId" in source) {
        groupId.value = source.groupId;
        const [page, groups] = await Promise.all([api.loadPage(source.groupId), loadGroups(source.storeId)]);
        eTag.value = page.eTag;
        reset(page.doc, groups);
      } else if ("sharedComponentId" in source) {
        const [info, content] = await Promise.all([api.sharedComponent(source.sharedComponentId), api.sharedComponentContent(source.sharedComponentId)]);
        shared.value = info;
        reset(content);
      } else {
        file.value = source;
        const themeFile = source.type === "themes";
        const [git, { doc: page, eTag: fileETag }, groups] = await Promise.all([
          themeFile ? false : api.gitFlowOf(source.storeId, source.theme).then((x) => x.on),
          themeFile ? api.findTemplate(source) : api.findFilePage(source).then((doc) => ({ doc, eTag: undefined })),
          themeFile && !templateKey.value ? undefined : loadGroups(source.storeId, source.theme),
        ]);
        // Theme files are never in git, even when the store's pages are: they are saved, published and discarded as blobs.
        gitFlow.value = git;
        if (fileETag !== undefined) themeETags.value[source.path] = fileETag;
        templateIsNew.value = !page && !!templateKey.value;
        reset(page ?? (pageType.value ? startingTemplate(pageType.value, undefined, undefined, mainItem) : { settings: { type: "settings" } as Item, content: [] }), groups);
      }
      // The preview's schemas may be in already.
      seedGroups();
      seedTemplate();
      await Promise.all([loadComponents(true), refreshStatus()]);
    } catch (e) {
      loadError.value = e;
    } finally {
      busy.value = undefined;
    }
  }

  const themeStatus = ref<ThemeStatus>();
  const themeFileOpen = computed(() => file.value?.type === "themes");
  const discardable = computed<{ page: boolean; theme: ThemePart[] }>(() => ({
    page: !!status.value.hasChanges && (themeFileOpen.value ? !isNewThemeDraft(themeStatus.value, file.value!.theme, file.value!.path) : !!status.value.published),
    theme:
      theme.value && !shared.value
        ? THEME_PARTS.filter((part) => {
            const path = THEME_FILES[part](theme.value!);
            return hasThemeDraft(themeStatus.value, theme.value!, path) && !isNewThemeDraft(themeStatus.value, theme.value!, path);
          })
        : [],
  }));

  async function refreshThemeStatus() {
    if (!storeId.value || shared.value) return;
    themeStatus.value = await api.themeStatus(storeId.value).catch(() => themeStatus.value);
  }

  async function refreshStatus() {
    const page = file.value;
    if (!groupId.value && !page) return;
    // A state that cannot be read stays as last read, and the publish actions wait for one.
    const known = () => status.value;
    const [, own] = await Promise.all([
      refreshThemeStatus(),
      groupId.value ? api.publishStatus(groupId.value).catch(known) : themeFileOpen.value ? undefined : api.filePublishStatus(page!).catch(known),
    ]);
    status.value = own ?? templateStatus(themeStatus.value, page!.theme, page!.path);
  }

  async function discardDrafts(choice: { page: boolean; theme: ThemePart[] }) {
    if (busy.value || !storeId.value) return;
    const page = file.value;
    const themeName = theme.value;
    const files = themeName ? discardable.value.theme.filter((part) => choice.theme.includes(part)).map((part) => THEME_FILES[part](themeName)) : [];
    let pagePart: DesignerDiscardPage | undefined;
    if (choice.page && discardable.value.page) {
      if (groupId.value) pagePart = { groupId: groupId.value };
      else if (page && themeFileOpen.value) files.push(page.path);
      else if (page) pagePart = { path: page.path, type: page.type };
    }
    if (!pagePart && !files.length) return;
    busy.value = "restoring";
    try {
      await api.discardDesigner(storeId.value, { page: pagePart, themeFiles: files });
    } finally {
      busy.value = undefined;
    }
  }

  async function save(themeFiles: DesignerSaveFile[] = []): Promise<Record<string, string> | false> {
    if ((!groupId.value && !file.value && !shared.value) || busy.value || loadError.value) return false;
    const sent = pageOnly(doc.value);
    const pageChanged = JSON.stringify(sent) !== savedJson.value;
    const groups = changedGroups();
    if (pageChanged && groupId.value && !eTag.value) throw new api.ApiError(428, "");
    const files = [...themeFiles, ...(theme.value ? api.sectionGroupFiles(theme.value, groups, themeETags.value) : [])];
    let page: DesignerSavePage | undefined;
    if (pageChanged && !shared.value) {
      if (groupId.value) page = { groupId: groupId.value, eTag: eTag.value!, content: fromDocument(sent) };
      else if (themeFileOpen.value) {
        const path = file.value!.path;
        files.unshift({ path, eTag: themeETags.value[path] ?? "", content: api.filePageContent(sent) });
      } else page = { path: file.value!.path, type: file.value!.type, content: api.filePageContent(sent) };
    }
    // Checked before a shared component's content goes, so that a Save sends all or nothing.
    if (files.length && !handshake.value?.hash) throw new CatalogPendingError();
    busy.value = "saving";
    try {
      const versions: Record<string, string> = {};
      if (pageChanged && shared.value) await api.saveSharedComponentContent(shared.value.id, sent);
      if (page || files.length) {
        const saved = await api.saveDesigner(storeId.value!, { page, themeFiles: files }, handshake.value?.hash);
        if (groupId.value) eTag.value = saved.eTag ?? eTag.value;
        for (const x of saved.themeFiles ?? []) versions[x.path] = x.eTag;
        for (const path of Object.keys(themeETags.value)) if (versions[path]) themeETags.value[path] = versions[path];
      }
      if (pageChanged && themeFileOpen.value) templateIsNew.value = false;
      savedJson.value = JSON.stringify(sent);
      for (const [g, items] of Object.entries(groups)) savedGroupsJson.value[g as SectionGroup] = JSON.stringify(items);
      await Promise.all([refreshStatus(), loadComponents(true)]);
      return versions;
    } finally {
      busy.value = undefined;
    }
  }

  async function whilePublishing<T>(action: () => Promise<T>): Promise<T> {
    busy.value = "publishing";
    try {
      return await action();
    } finally {
      // The actions stay off until the state after the publish is read.
      await refreshStatus();
      busy.value = undefined;
    }
  }

  /** In git, `rebase` publishes over someone else's version. */
  async function publish(live: boolean, rebase = false) {
    if (busy.value || themeFileOpen.value) return;
    await whilePublishing(async () => {
      if (groupId.value) await api.setPublished(groupId.value, live);
      else if (file.value) await api.setFilePublished(file.value, live, rebase);
    });
  }

  async function publishTheme(): Promise<number | undefined> {
    if (busy.value || !storeId.value) return undefined;
    return whilePublishing(() => api.publishTheme(storeId.value!, handshake.value?.hash));
  }

  async function promote() {
    if (busy.value || !file.value) return;
    await whilePublishing(() => api.promoteFilePage(file.value!));
  }

  async function restore(sha: string) {
    if (busy.value || !file.value) return;
    busy.value = "restoring";
    try {
      await api.restoreFileVersion(file.value, sha);
      reset(await api.loadFilePage(file.value));
      await Promise.all([loadComponents(true), refreshStatus()]);
    } finally {
      busy.value = undefined;
    }
  }

  const inFlight = (x?: { pending?: boolean; awaitingMerge?: boolean } | null) => !!x?.pending && !x.awaitingMerge;
  // A live page behind the production branch follows the branch: publish while it has the page, unpublish once it does not.
  const canPublish = computed(
    () =>
      !busy.value &&
      !dirty.value &&
      (!!status.value.hasChanges || (!!status.value.liveBehind && !!status.value.published)) &&
      !inFlight(status.value),
  );
  const canUnpublish = computed(
    () =>
      !busy.value &&
      !dirty.value &&
      ((!!status.value.published && !status.value.hasChanges) || (!!status.value.liveBehind && !status.value.published)) &&
      !inFlight(status.value),
  );
  const canPromote = computed(() => {
    const production = status.value.production;
    return !busy.value && !dirty.value && !!status.value.published && !status.value.hasChanges && !!production?.behind && !inFlight(production);
  });

  return {
    groupId,
    storeId,
    file,
    gitFlow,
    doc,
    schemas,
    handshake,
    can,
    selection,
    groupsShown,
    groupsEditable,
    isLocked,
    groupsPreview,
    groupSectionTypes,
    hovered,
    sectionLabels,
    dirty,
    changedParts,
    canUndo,
    canRedo,
    undo,
    redo,
    status,
    busy,
    loadError,
    canPublish,
    canUnpublish,
    canPromote,
    templateKey,
    pageType,
    canPlaceShared,
    languages,
    language,
    translating,
    mainSection,
    mainItem,
    isMainKept,
    isAtLimit,
    isMainShown,
    restoreMainSection,
    templateIsNew,
    extraPageFields,
    sectionSchema,
    blockSchema,
    sectionTypes,
    pageRows,
    selectedSection,
    selectedItem,
    selectedFields,
    components,
    loadComponents,
    shared,
    themeFileOpen,
    themeStatus,
    refreshThemeStatus,
    discardable,
    discardDrafts,
    preview,
    setField,
    setText,
    addSection,
    addBlock,
    moveSection,
    duplicate,
    remove,
    toggleHidden,
    detach,
    insertShared,
    paste,
    checked,
    selecting,
    toggleChecked,
    removeChecked,
    saveCheckedAsShared,
    open,
    save,
    publish,
    publishTheme,
    promote,
    restore,
  };
}

export type Designer = ReturnType<typeof useDesigner>;
