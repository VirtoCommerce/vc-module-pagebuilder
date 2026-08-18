import { installPageWideAnchors } from './link-anchors';
import { PageAnchor, PageAnchorsProvider, clearActivePageAnchorsProvider } from '@core/services';

type CkAnchor = { name: string | null; id: string | null };
type DialogListener = (event: any) => void;

const globalRef = globalThis as { CKEDITOR?: any };

/** Mimics the parts of a CKEditor select and its table cell that the override touches. */
function createSelect() {
    const cell = { hidden: false, hide() { this.hidden = true; }, show() { this.hidden = false; } };
    return {
        items: <[string, string | undefined][]>[],
        value: '',
        setupCalls: 0,
        cell,
        clear() { this.items = []; },
        add(label: string, value?: string) { this.items.push([label, value]); },
        setValue(value: string) { this.value = value; },
        getElement: () => ({ getParent: () => cell }),
        setup(_data: unknown) { this.setupCalls++; }
    };
}

function stubCkEditor(fieldLocal: CkAnchor[] = []) {
    const link: { getEditorAnchors: (editor?: unknown) => CkAnchor[] } = { getEditorAnchors: () => fieldLocal };
    const listeners: DialogListener[] = [];
    globalRef.CKEDITOR = { plugins: { link }, on: (_: string, l: DialogListener) => listeners.push(l) };
    return { link, listeners };
}

/** Runs the registered `dialogDefinition` handler against a stubbed link dialog. */
function openLinkDialog(listeners: DialogListener[]) {
    const anchorName = createSelect();
    const anchorId = createSelect();
    const elements: Record<string, unknown> = { anchorName, anchorId };

    listeners.forEach(listener => listener({
        data: { name: 'link', definition: { getContents: () => ({ get: (id: string) => elements[id] }) } }
    }));

    return { anchorName, anchorId };
}

function provider(anchors: PageAnchor[]): PageAnchorsProvider {
    return { getAnchors: () => anchors };
}

const pageAnchors: PageAnchor[] = [
    { value: 'imagexun', label: 'Hero image' },
    { value: 'specifications', label: 'Specifications' }
];

afterEach(() => {
    delete globalRef.CKEDITOR;
});

describe('installPageWideAnchors', () => {
    it('does nothing while the link plugin is not loaded', () => {
        expect(installPageWideAnchors(provider(pageAnchors))).toBe(false);
    });

    it('publishes the page anchors to the link dialog', () => {
        const { link } = stubCkEditor();

        expect(installPageWideAnchors(provider(pageAnchors))).toBe(true);
        expect(link.getEditorAnchors(null)).toEqual([
            { name: 'imagexun', id: null },
            { name: 'specifications', id: null }
        ]);
    });

    it('never fills the element id slot, so the built href is always the picked anchor', () => {
        const { link } = stubCkEditor();
        installPageWideAnchors(provider(pageAnchors));

        expect(link.getEditorAnchors(null).every((anchor: CkAnchor) => anchor.id === null)).toBe(true);
    });

    it('keeps CKEditor field-local anchors when the page has none', () => {
        const fieldLocal = [{ name: 'in-text', id: null }];
        const { link } = stubCkEditor(fieldLocal);

        installPageWideAnchors(provider([]));

        expect(link.getEditorAnchors(null)).toEqual(fieldLocal);
    });

    it('wraps the override only once, however many editor instances are created', () => {
        const { link } = stubCkEditor();
        installPageWideAnchors(provider(pageAnchors));
        const wrapped = link.getEditorAnchors;

        expect(installPageWideAnchors(provider(pageAnchors))).toBe(true);
        expect(link.getEditorAnchors).toBe(wrapped);
    });

    it('follows the provider of the editor route that is currently active', () => {
        const { link, listeners } = stubCkEditor();
        installPageWideAnchors(provider(pageAnchors));

        // Leaving and re-entering the editor destroys the route scoped service and creates a new one,
        // while the override on the global CKEDITOR namespace survives.
        installPageWideAnchors(provider([{ value: 'reopened', label: 'Reopened page' }]));

        expect(link.getEditorAnchors(null)).toEqual([{ name: 'reopened', id: null }]);

        const { anchorName } = openLinkDialog(listeners);
        anchorName.setup({});
        expect(anchorName.items).toEqual([['', undefined], ['Reopened page (reopened)', 'reopened']]);
    });

    it('reflects later page edits because the provider is read on every open', () => {
        const { link } = stubCkEditor();
        let anchors = [...pageAnchors];
        installPageWideAnchors({ getAnchors: () => anchors });

        anchors = [{ value: 'added-later', label: 'Added later' }];

        expect(link.getEditorAnchors(null)).toEqual([{ name: 'added-later', id: null }]);
    });
});

describe('anchor picker', () => {
    it('lists the item name with the anchor it links to', () => {
        const { listeners } = stubCkEditor();
        installPageWideAnchors(provider(pageAnchors));

        const { anchorName } = openLinkDialog(listeners);
        anchorName.setup({});

        expect(anchorName.items).toEqual([
            ['', undefined],
            ['Hero image (imagexun)', 'imagexun'],
            ['Specifications (specifications)', 'specifications']
        ]);
    });

    it('shows the bare value when the item has no name of its own', () => {
        const { listeners } = stubCkEditor();
        installPageWideAnchors(provider([{ value: 'text2', label: 'text2' }]));

        const { anchorName } = openLinkDialog(listeners);
        anchorName.setup({});

        expect(anchorName.items).toEqual([['', undefined], ['text2', 'text2']]);
    });

    it('selects the anchor an existing link points at', () => {
        const { listeners } = stubCkEditor();
        installPageWideAnchors(provider(pageAnchors));

        const { anchorName } = openLinkDialog(listeners);
        anchorName.setup({ anchor: { name: 'specifications' } });

        expect(anchorName.value).toBe('specifications');
    });

    it('keeps an anchor that is no longer on the page selectable, so the link survives a save', () => {
        const { listeners } = stubCkEditor();
        installPageWideAnchors(provider(pageAnchors));

        const { anchorName } = openLinkDialog(listeners);
        anchorName.setup({ anchor: { name: 'typed-by-hand' } });

        expect(anchorName.items).toContainEqual(['typed-by-hand', 'typed-by-hand']);
        expect(anchorName.value).toBe('typed-by-hand');
    });

    it('leaves the stock picker alone when the page has no anchors', () => {
        const { listeners } = stubCkEditor();
        installPageWideAnchors(provider([]));

        const { anchorName } = openLinkDialog(listeners);
        anchorName.setup({});

        expect(anchorName.setupCalls).toBe(1);
        expect(anchorName.items).toEqual([]);
    });

    it('hides the empty element id cell so the anchor select gets the whole row', () => {
        const { listeners } = stubCkEditor();
        installPageWideAnchors(provider(pageAnchors));

        const { anchorId } = openLinkDialog(listeners);
        anchorId.setup({});

        expect(anchorId.cell.hidden).toBe(true);
        expect(anchorId.setupCalls).toBe(1);
    });

    it('keeps the element id cell when falling back to CKEditor anchors', () => {
        const { listeners } = stubCkEditor();
        installPageWideAnchors(provider([]));

        const { anchorId } = openLinkDialog(listeners);
        anchorId.cell.hide();
        anchorId.setup({});

        expect(anchorId.cell.hidden).toBe(false);
    });
});

describe('clearActivePageAnchorsProvider', () => {
    it('restores the field-local anchors once the editor route is destroyed', () => {
        const fieldLocal = [{ name: 'in-text', id: null }];
        const { link, listeners } = stubCkEditor(fieldLocal);
        const editorRoute = provider(pageAnchors);
        installPageWideAnchors(editorRoute);

        // Leaving /pages tears the route scoped service down, while the override on the global
        // CKEDITOR namespace stays behind for the editors of every other module.
        clearActivePageAnchorsProvider(editorRoute);

        expect(link.getEditorAnchors(null)).toEqual(fieldLocal);

        const { anchorName, anchorId } = openLinkDialog(listeners);
        anchorName.setup({});
        anchorId.cell.hide();
        anchorId.setup({});

        expect(anchorName.setupCalls).toBe(1);
        expect(anchorName.items).toEqual([]);
        expect(anchorId.cell.hidden).toBe(false);
    });

    it('ignores a provider that a later editor route has already replaced', () => {
        const { link } = stubCkEditor();
        const left = provider(pageAnchors);
        installPageWideAnchors(left);
        installPageWideAnchors(provider([{ value: 'reopened', label: 'Reopened page' }]));

        clearActivePageAnchorsProvider(left);

        expect(link.getEditorAnchors(null)).toEqual([{ name: 'reopened', id: null }]);
    });
});
