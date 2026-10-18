import { computed, reactive, ref, shallowRef, watch } from "vue";
import { useManualRefHistory } from "@vueuse/core";
import { debounce } from "lodash-es";
import type { Field, Item, ItemSchema, PageDocument, Schemas, SectionGroup, Selection } from "./types";
import { copyItem, fieldsOf, itemTitle, newId, newItem, withTextAt } from "./schema";
import { chromeToSections, isComponentRef, resolveRefs } from "./document";
import type { ClipboardItem } from "./clipboard";
import * as api from "./api";
import { GROUPS, groupItems, groupOf, pageOnly, withGroups } from "./groups";
import { PAGE_CONTENT, pageTypeOf, startingTemplate, templateEntry, templateKeyOf } from "./templates";

const EMPTY_SCHEMAS: Schemas = { sections: {}, blocks: {}, shared: {}, objects: {} };
/** A page-level row of the tree: the page template's own fields, or a static section's (Page Header: SEO). */
export interface PageRow {
  key: string;
  name: string;
  icon?: string;
  fields: Field[];
  position: "top" | "bottom";
}

/**
 * The page being designed: its document and version, what is selected, undo/redo, the edits the panels make, and
 * saving and publishing. Every edit replaces the parts it changes, so the history (snapshots, debounced while
 * typing) and the preview see it.
 */
export function useDesigner() {
  const groupId = ref<string>();
  const storeId = ref<string>();
  /** a page kept as a file (a site's Files › Pages); with gitFlow, its versions are in git */
  const file = ref<api.FilePage>();
  /** a shared component edited like a page: its sections show on every page that places it (no publishing) */
  const shared = ref<api.SharedComponent>();
  const gitFlow = ref(false);
  /** the store's theme (its section groups' files); groupsLoaded: the page's tree has its Header and Footer */
  const theme = ref<string>();
  const groupsLoaded = ref(false);
  const savedGroupsJson = ref<Record<SectionGroup, string>>({ header: "[]", footer: "[]" });
  const doc = ref<PageDocument>({ settings: {} as Item, content: [] });
  const schemas = shallowRef<Schemas>(EMPTY_SCHEMAS);
  /** what the settings panel edits; null: nothing (the panel hides) */
  const selection = ref<Selection | null>(null);
  /** The section under the pointer in the preview (its row lights up) */
  const hovered = ref<string | null>(null);

  // persistence state
  const eTag = ref<string>();
  const savedJson = ref("");
  const status = ref<api.PublishStatus>({});
  const busy = ref<"loading" | "saving" | "publishing" | "restoring">();
  const loadError = ref<unknown>();

  // undo/redo: a snapshot once edits pause (typing makes one step); undoing restores one, which records nothing
  const history = useManualRefHistory(doc, { clone: true, capacity: 100 });
  const record = debounce(() => {
    if (JSON.stringify(history.last.value.snapshot) !== JSON.stringify(doc.value)) history.commit();
  }, 400);
  watch(doc, record, { deep: true });
  // an edit still waiting for the pause counts: it is recorded first
  const canUndo = computed(() => history.canUndo.value || JSON.stringify(doc.value) !== JSON.stringify(history.last.value.snapshot));
  const canRedo = computed(() => history.canRedo.value);
  function undo() {
    record.flush();
    history.undo();
  }
  function redo() {
    record.flush();
    history.redo();
  }
  const pageDirty = computed(() => JSON.stringify(pageOnly(doc.value)) !== savedJson.value);
  const changedGroups = (): Partial<Record<SectionGroup, Item[]>> =>
    groupsLoaded.value
      ? Object.fromEntries(GROUPS.map((g) => [g, groupItems(doc.value, g)] as const).filter(([g, items]) => JSON.stringify(items) !== savedGroupsJson.value[g]))
      : {};
  const dirty = computed(() => pageDirty.value || Object.keys(changedGroups()).length > 0);

  // ── schemas ────────────────────────────────────────────────────────────────

  const sectionSchema = (type?: string): ItemSchema | undefined => (type ? schemas.value.sections[type] : undefined);
  // a block's schema is looked up in blocks, then sections (as the Angular designer did)
  const blockSchema = (type?: string): ItemSchema | undefined => (type ? (schemas.value.blocks[type] ?? schemas.value.sections[type]) : undefined);

  /** The theme template open (a theme file under templates/): its key ("product.printer") and page type */
  const templateKey = computed(() => (file.value?.type === "themes" ? templateKeyOf(file.value.path) : undefined));
  const pageType = computed(() => (templateKey.value ? pageTypeOf(templateKey.value) : undefined));

  /**
   * The template entry of what is open (a page, or a product, category or page template): its key restricts section
   * targetTemplates, its settings are the page fields.
   */
  const template = computed(() => templateEntry(schemas.value, pageType.value));
  /** page-level fields the storefront's descriptors don't have: a page's Template, a page template's Layout */
  const extraPageFields = ref<Field[]>([]);

  /** Section types that can be added: not page-level, not hidden, and meant for this kind of page. */
  const sectionTypes = computed(() => {
    const { key, entry } = template.value;
    // where the page's own sections go: on a page template, once
    const pageContent = pageType.value === "page" && !doc.value.content.some((x) => x.type === PAGE_CONTENT);
    return Object.entries(schemas.value.sections)
      .filter(([, schema]) => !schema.static && !schema.hidden && !schema.groups?.length)
      .filter(([type]) => type !== PAGE_CONTENT || pageContent)
      .filter(([type, schema]) => (entry.sections ? entry.sections.includes(type) : !schema.targetTemplates?.length || schema.targetTemplates.includes(key)))
      .map(([type, schema]) => ({ type, schema }))
      .sort((a, b) => (a.schema.name ?? a.type).localeCompare(b.schema.name ?? b.type));
  });

  /** Section types a group can hold (Shopify's `enabled_on.groups`) */
  const groupSectionTypes = (group: SectionGroup) =>
    Object.entries(schemas.value.sections)
      .filter(([, schema]) => !schema.hidden && !!schema.groups?.includes(group))
      .map(([type, schema]) => ({ type, schema }))
      .sort((a, b) => (a.schema.name ?? a.type).localeCompare(b.schema.name ?? b.type));

  /** where the page's own sections end (the footer group's begin) */
  const pageEnd = () => {
    const footer = doc.value.content.findIndex((x) => groupOf(x) === "footer");
    return footer < 0 ? doc.value.content.length : footer;
  };
  /** where a group's sections end */
  const groupEnd = (group: SectionGroup) => (group === "header" ? doc.value.content.filter((x) => groupOf(x) === "header").length : doc.value.content.length);

  const pageRows = computed<PageRow[]>(() => {
    const rows: PageRow[] = [];
    // a shared component has only its sections: no page settings, no header or footer
    if (shared.value) return rows;
    const described = template.value.entry.settings ?? [];
    // a field the storefront's descriptor has itself is its own
    const own = [...described, ...extraPageFields.value.filter((x) => !described.some((d) => d.id === x.id))];
    if (own.length) rows.push({ key: "settings", name: "", icon: "lucide-file-cog", fields: own, position: "top" });
    for (const [type, schema] of Object.entries(schemas.value.sections)) {
      if (schema.static) rows.push({ key: type, name: schema.name ?? type, icon: schema.icon, fields: schema.settings ?? [], position: schema.static === "bottom" ? "bottom" : "top" });
    }
    return rows;
  });

  // ── selection ──────────────────────────────────────────────────────────────

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

  // ── edits ──────────────────────────────────────────────────────────────────

  function setContent(content: Item[]) {
    doc.value = { ...doc.value, content };
  }
  function updateSection(id: string, change: (section: Item) => Item) {
    setContent(doc.value.content.map((x) => (x.id === id ? change(x) : x)));
  }

  /** A field of what is selected changed (page rows edit the page settings). */
  function setField(id: string, value: unknown) {
    const sel = selection.value;
    if (!sel) return;
    if (sel.kind === "page") doc.value = { ...doc.value, settings: { ...doc.value.settings, [id]: value } };
    else if (sel.kind === "section") updateSection(sel.id, (x) => ({ ...x, [id]: value }));
    else updateSection(sel.sectionId, (x) => ({ ...x, blocks: x.blocks?.map((b) => (b.id === sel.id ? { ...b, [id]: value } : b)) }));
  }

  /**
   * A text edited on the page (the preview's inline editing): the string at the path of that section or block.
   * Returns what to select, or undefined when the item or the text isn't there (nothing changes then).
   */
  function setText(itemId: string, path: (string | number)[], text: string): Selection | undefined {
    for (const section of doc.value.content) {
      if (section.id === itemId) {
        const changed = withTextAt(section, path, text);
        if (!changed) return undefined;
        updateSection(section.id, () => changed);
        return { kind: "section", id: section.id };
      }
      const block = section.blocks?.find((b) => b.id === itemId);
      if (block) {
        const changed = withTextAt(block, path, text);
        if (!changed) return undefined;
        updateSection(section.id, (x) => ({ ...x, blocks: x.blocks?.map((b) => (b.id === itemId ? changed : b)) }));
        return { kind: "block", sectionId: section.id, id: itemId };
      }
    }
    return undefined;
  }

  /** Adds a section at the end of the page's own sections, or of a section group's */
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
    const content = [...doc.value.content];
    content.splice(to, 0, ...content.splice(from, 1));
    setContent(content);
  }

  function moveBlock(sectionId: string, from: number, to: number) {
    updateSection(sectionId, (x) => {
      const blocks = [...(x.blocks ?? [])];
      blocks.splice(to, 0, ...blocks.splice(from, 1));
      return { ...x, blocks };
    });
  }

  function duplicate(sel: Selection) {
    if (sel.kind === "section") {
      const index = doc.value.content.findIndex((x) => x.id === sel.id);
      const original = doc.value.content[index];
      // a reference is copied as another placement of the same component
      const copy = isComponentRef(original) ? { ...original, id: newId("componentRef") + newId("") } : copyItem(original);
      const content = [...doc.value.content];
      content.splice(index + 1, 0, copy);
      setContent(content);
      selection.value = { kind: "section", id: copy.id };
    } else if (sel.kind === "block") {
      const blocks = findSection(sel.sectionId)?.blocks ?? [];
      const index = blocks.findIndex((b) => b.id === sel.id);
      const copy = copyItem(blocks[index]);
      updateSection(sel.sectionId, (x) => ({ ...x, blocks: [...blocks.slice(0, index + 1), copy, ...blocks.slice(index + 1)] }));
      selection.value = { kind: "block", sectionId: sel.sectionId, id: copy.id };
    }
  }

  function remove(sel: Selection) {
    if (sel.kind === "page") return;
    if (sel.kind === "section") setContent(doc.value.content.filter((x) => x.id !== sel.id));
    else updateSection(sel.sectionId, (x) => ({ ...x, blocks: x.blocks?.filter((b) => b.id !== sel.id) }));
    // the selection was the removed item or inside it: a removed block selects its section, a removed section nothing
    const current = selection.value;
    if (current && current.kind !== "page" && (current.id === sel.id || (current.kind === "block" && current.sectionId === sel.id))) {
      selection.value = sel.kind === "block" ? { kind: "section", id: sel.sectionId } : null;
    }
  }

  /**
   * Pastes a copied section (after the selected one, else at the end) or block (after the selected block, else at the
   * end of the selected section, if that section takes such blocks). Returns why it couldn't, as a message key.
   */
  function paste(copied: ClipboardItem): string | undefined {
    const sel = selection.value;
    if (copied.type === "section") {
      const item: Item = isComponentRef(copied.content) ? { ...copied.content, id: newId("componentRef") + newId("") } : copyItem(copied.content);
      // after the selected section; into its group only if the section is for that group, else at the page's end
      let target: Item | undefined;
      if (sel?.kind === "block") target = findSection(sel.sectionId);
      else if (sel?.kind === "section") target = findSection(sel.id);
      const group = target && groupOf(target);
      const fits = !group || !!sectionSchema(item.type)?.groups?.includes(group);
      if (group && fits) item.__group = group;
      else delete item.__group;
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
    const block = copyItem(copied.content);
    const blocks = [...(section.blocks ?? [])];
    blocks.splice(sel.kind === "block" ? blocks.findIndex((b) => b.id === sel.id) + 1 : blocks.length, 0, block);
    updateSection(sectionId, (x) => ({ ...x, blocks }));
    selection.value = { kind: "block", sectionId, id: block.id };
    return undefined;
  }

  // ── several sections at once (the list's select mode) ──────────────────────

  const checked = ref<string[]>([]);
  function toggleChecked(id: string) {
    checked.value = checked.value.includes(id) ? checked.value.filter((x) => x !== id) : [...checked.value, id];
  }

  function removeChecked() {
    const ids = new Set(checked.value);
    setContent(doc.value.content.filter((x) => !ids.has(x.id)));
    const sel = selection.value;
    if (sel && sel.kind !== "page" && ids.has(sel.kind === "block" ? sel.sectionId : sel.id)) selection.value = null;
    checked.value = [];
  }

  /**
   * Makes the checked sections a shared component (they must be next to each other, and none a shared one) and puts
   * it on the page in their place. Returns why it couldn't, as a message key.
   */
  async function saveCheckedAsShared(name: string): Promise<string | undefined> {
    const indexes = checked.value.map((id) => doc.value.content.findIndex((x) => x.id === id)).sort((a, b) => a - b);
    const sections = indexes.map((i) => doc.value.content[i]);
    if (!sections.length || indexes.some((i, n) => i < 0 || (n > 0 && i !== indexes[n - 1] + 1))) return "PAGE_BUILDER.DESIGNER.SELECT_MODE.NOT_ADJACENT";
    if (sections.some(isComponentRef)) return "PAGE_BUILDER.DESIGNER.SELECT_MODE.HAS_SHARED";
    const component = await api.createSharedComponent(storeId.value!, name, { settings: { type: "settings" } as Item, content: sections.map((x) => ({ ...x })) });
    const placement: Item = { id: newId("componentRef") + newId(""), type: "componentRef", componentRef: component.id };
    const content = doc.value.content.filter((x) => !checked.value.includes(x.id));
    content.splice(indexes[0], 0, placement);
    setContent(content);
    checked.value = [];
    selection.value = { kind: "section", id: placement.id };
    await loadComponents();
    return undefined;
  }

  function toggleHidden(sel: Selection) {
    if (sel.kind === "section") updateSection(sel.id, (x) => (isComponentRef(x) ? x : { ...x, hidden: !x.hidden }));
    else if (sel.kind === "block") updateSection(sel.sectionId, (x) => ({ ...x, blocks: x.blocks?.map((b) => (b.id === sel.id ? { ...b, hidden: !b.hidden } : b)) }));
  }

  // ── shared components ──────────────────────────────────────────────────────

  /** The shared components the page places: name, usage, content (null: missing) */
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

  /** Places a shared component on the page (a reference: edits to the component show on every page using it). */
  async function insertShared(component: api.SharedComponent, index = pageEnd()) {
    // a shared component can't hold another one
    if (shared.value) return;
    const placement: Item = { id: newId("componentRef") + newId(""), type: "componentRef", componentRef: component.id };
    const content = [...doc.value.content];
    content.splice(index, 0, placement);
    setContent(content);
    selection.value = { kind: "section", id: placement.id };
    await loadComponents();
  }

  /** Copies a placed component's sections into the page in place of the reference. */
  async function detach(id: string) {
    const placement = findSection(id);
    if (!placement || !isComponentRef(placement)) return;
    const content = (await api.sharedComponentContent(placement.componentRef)).content.map(copyItem);
    const index = doc.value.content.findIndex((x) => x.id === id);
    if (index < 0) return;
    const next = [...doc.value.content];
    next.splice(index, 1, ...content);
    setContent(next);
    selection.value = content[0] ? { kind: "section", id: content[0].id } : null;
  }

  /** The names the preview shows on its section outlines: the page's own sections (placements have theirs) */
  const sectionLabels = computed(() =>
    Object.fromEntries(
      doc.value.content.filter((x) => !isComponentRef(x) && !x.hidden).map((x) => [x.id, itemTitle(x, sectionSchema(x.type))]),
    ),
  );

  /** What the preview renders: references resolved, labelled for the outline the preview draws around them */
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

  /** The section groups as the preview shows them (none: the page has no Header and Footer) */
  const groupsPreview = computed(() => (groupsLoaded.value ? { header: groupItems(doc.value, "header"), footer: groupItems(doc.value, "footer") } : undefined));

  // ── persistence ────────────────────────────────────────────────────────────

  const currentGroups = () => Object.fromEntries(GROUPS.map((g) => [g, groupItems(doc.value, g)])) as Record<SectionGroup, Item[]>;

  /** The theme's section groups, for a page's tree; none when they can't be read (no Header and Footer then) */
  async function loadGroups(store: string, themeName?: string) {
    unseeded.clear();
    try {
      theme.value = themeName ?? (await api.storeTheme(store));
      const [header, footer] = await Promise.all(GROUPS.map((g) => api.loadSectionGroup(store, theme.value!, g)));
      groupsLoaded.value = true;
      if (!header) unseeded.add("header");
      if (!footer) unseeded.add("footer");
      return { header: header ?? [], footer: footer ?? [] };
    } catch {
      groupsLoaded.value = false;
      return undefined;
    }
  }

  // A theme without a group file still has a header and a footer: the group starts as its Header (or Footer) section
  // with nothing set, which the storefront shows as it always was, and is written to the theme once edited. Seeded once
  // the preview's schemas have that section (an older storefront has none), and only while nothing is unsaved.
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

  // A page's breadcrumbs and H1, page settings before, open as the Breadcrumbs and Page heading sections once the
  // preview's schemas have those (an older storefront has none), as the saved baseline: the site shows the page as it
  // was until it is saved. Only while nothing is unsaved, as the seeding above.
  function chromeAsSections() {
    const kinds = schemas.value.sections;
    const page = !!groupId.value || (!!file.value && file.value.type !== "themes");
    if (!page || dirty.value || !kinds.breadcrumbs || !kinds["page-heading"]) return;
    const moved = chromeToSections(pageOnly(doc.value));
    if (moved) reset(moved);
  }
  watch(schemas, chromeAsSections);

  /** The loaded or saved document is the new starting point: nothing to undo, nothing unsaved. */
  function reset(next: PageDocument, groups: Partial<Record<SectionGroup, Item[]>> = currentGroups()) {
    record.cancel();
    doc.value = groupsLoaded.value ? withGroups(next, groups) : next;
    savedJson.value = JSON.stringify(pageOnly(doc.value));
    savedGroupsJson.value = { header: JSON.stringify(groupItems(doc.value, "header")), footer: JSON.stringify(groupItems(doc.value, "footer")) };
    // the baseline is the last commit: commit the loaded page, then forget the steps
    history.commit();
    history.clear();
  }

  // A template with no theme file yet opens as the storefront's built-in one (its descriptor's `default` document),
  // once the preview's schemas say what that is; a page template always has the page's content in it. As the saved
  // baseline, like the seeding above.
  const templateIsNew = ref(false);
  function seedTemplate() {
    const type = pageType.value;
    if (!templateIsNew.value || !type || dirty.value || pageOnly(doc.value).content.length) return;
    const start = startingTemplate(type, undefined, template.value.entry);
    if (start.content.length) reset(start);
  }
  watch(schemas, seedTemplate);

  /** Opens a page: a database page (`groupId`), a file page, a theme template (a file page of type "themes"). */
  async function open(source: { groupId: string; storeId: string } | { sharedComponentId: string; storeId: string } | api.FilePage) {
    // what was open before (the designer switches between a page and the templates) is forgotten
    groupId.value = undefined;
    file.value = undefined;
    shared.value = undefined;
    eTag.value = undefined;
    gitFlow.value = false;
    groupsLoaded.value = false;
    templateIsNew.value = false;
    selection.value = null;
    hovered.value = null;
    checked.value = [];
    components.clear();
    status.value = {};
    groupDrafts.value = [];
    pageStatus = {};
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
        // a template shows with the theme's header and footer, as a page does (another theme file has none)
        const [git, page, groups] = await Promise.all([
          themeFile ? false : api.isGitFlow(source.storeId, source.theme),
          api.findFilePage(source),
          themeFile && !templateKey.value ? undefined : loadGroups(source.storeId, source.theme),
        ]);
        // theme files are never in git, even when the store's pages are: they are saved, published and discarded as blobs
        gitFlow.value = git;
        templateIsNew.value = !page && !!templateKey.value;
        reset(page ?? (pageType.value ? startingTemplate(pageType.value) : { settings: { type: "settings" } as Item, content: [] }), groups);
      }
      // the preview's schemas may be in already
      seedGroups();
      seedTemplate();
      chromeAsSections();
      await Promise.all([loadComponents(true), refreshStatus()]);
    } catch (e) {
      loadError.value = e;
    } finally {
      busy.value = undefined;
    }
  }

  /** header/footer drafts the site doesn't show yet: they go live with the page's Publish */
  const groupDrafts = ref<SectionGroup[]>([]);
  /** the page's own state, without its header and footer */
  let pageStatus: api.PublishStatus = {};

  async function refreshStatus() {
    const fallback = { published: true, hasChanges: false };
    if (groupId.value) pageStatus = await api.publishStatus(groupId.value).catch(() => fallback);
    else if (file.value) pageStatus = await api.filePublishStatus(file.value, gitFlow.value).catch(() => fallback);
    else return;
    groupDrafts.value = storeId.value && theme.value ? await api.sectionGroupDrafts(storeId.value, theme.value, GROUPS).catch(() => []) : [];
    // unpublished header/footer changes are changes of the page too: Publish puts them live
    status.value = { ...pageStatus, hasChanges: !!pageStatus.hasChanges || groupDrafts.value.length > 0 };
  }

  /**
   * Throws the saved drafts away, the page's and its header's and footer's, so what is published is current again.
   * A page that was never published keeps its draft: it is the only copy.
   */
  async function discardDrafts() {
    if (busy.value) return;
    busy.value = "restoring";
    try {
      if (pageStatus.hasChanges && pageStatus.published) {
        const page = file.value;
        if (groupId.value) await api.discardGroupDraft(groupId.value);
        else if (page) await (gitFlow.value ? api.discardFileDraft(page) : api.discardBlobDraft(page.storeId, page.type, page.path, page.theme));
      }
      if (groupDrafts.value.length && storeId.value && theme.value) await api.discardSectionGroupDrafts(storeId.value, theme.value, groupDrafts.value);
    } finally {
      busy.value = undefined;
    }
  }

  /**
   * Saves the draft (in git: a commit on this editor's branch). A database page throws ApiError 412 when someone
   * else saved it meanwhile; edits made while saving stay unsaved.
   */
  async function save() {
    if ((!groupId.value && !file.value && !shared.value) || busy.value) return;
    // the page's own document, saved as its draft if it changed; changed groups as drafts of the theme's files
    const sent = pageOnly(doc.value);
    const pageChanged = JSON.stringify(sent) !== savedJson.value;
    const groups = changedGroups();
    if (pageChanged && groupId.value && !eTag.value) throw new api.ApiError(428, "");
    busy.value = "saving";
    try {
      if (!pageChanged) {
        // nothing of the page's own to save
      } else if (groupId.value) eTag.value = (await api.savePage(groupId.value, sent, eTag.value!)) ?? eTag.value;
      else if (shared.value) await api.saveSharedComponentContent(shared.value.id, sent);
      else {
        await api.saveFilePage(file.value!, sent);
        templateIsNew.value = false;
      }
      savedJson.value = JSON.stringify(sent);
      if (Object.keys(groups).length) {
        await api.saveSectionGroups(storeId.value!, theme.value!, groups);
        for (const [g, items] of Object.entries(groups)) savedGroupsJson.value[g as SectionGroup] = JSON.stringify(items);
      }
      await Promise.all([refreshStatus(), loadComponents(true)]);
    } finally {
      busy.value = undefined;
    }
  }

  /** Publishes the saved draft or takes the page off the site. In git, `rebase` publishes over someone else's version. */
  async function publish(live: boolean, rebase = false) {
    if (busy.value) return;
    busy.value = "publishing";
    try {
      // the page itself when it has something to publish (or to take off the site), then its header and footer drafts
      const pageToPublish = !live || !!pageStatus.hasChanges || !pageStatus.published;
      if (!pageToPublish) {
        // only header/footer changes
      } else if (groupId.value) await api.setPublished(groupId.value, live);
      else if (file.value) await api.setFilePublished(file.value, gitFlow.value, live, rebase);
      if (live && groupDrafts.value.length) await api.publishSectionGroups(storeId.value!, theme.value!, groupDrafts.value);
    } finally {
      busy.value = undefined;
      await refreshStatus();
    }
  }

  async function promote() {
    if (busy.value || !file.value) return;
    busy.value = "publishing";
    try {
      await api.promoteFilePage(file.value);
    } finally {
      busy.value = undefined;
      await refreshStatus();
    }
  }

  /** git: makes a version this editor's draft again, and opens it */
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

  // a pull request that will merge on its own is in flight; one awaiting a merge can be published again
  const inFlight = (x?: { pending?: boolean; awaitingMerge?: boolean } | null) => !!x?.pending && !x.awaitingMerge;
  const canPublish = computed(() => !busy.value && !dirty.value && !!status.value.hasChanges && !inFlight(status.value));
  const canUnpublish = computed(
    () => !busy.value && !dirty.value && !!status.value.published && !status.value.hasChanges && !inFlight(status.value),
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
    selection,
    groupsLoaded,
    groupsPreview,
    groupSectionTypes,
    hovered,
    sectionLabels,
    dirty,
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
    template,
    templateKey,
    pageType,
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
    groupDrafts,
    discardDrafts,
    preview,
    setField,
    setText,
    addSection,
    addBlock,
    moveSection,
    moveBlock,
    duplicate,
    remove,
    toggleHidden,
    detach,
    insertShared,
    paste,
    checked,
    toggleChecked,
    removeChecked,
    saveCheckedAsShared,
    open,
    save,
    publish,
    promote,
    restore,
  };
}

export type Designer = ReturnType<typeof useDesigner>;
