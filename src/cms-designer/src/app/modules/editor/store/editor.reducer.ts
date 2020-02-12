import { createReducer, on, Action } from '@ngrx/store';
import { BlocksSchema } from '@shared/models';
import { PageModel } from '@editor/models';
import * as Actions from './editor.actions';

export interface EditorState {
    pageLoading: boolean;
    schemaLoading: boolean;
    pageNotLoaded: boolean;
    schemaNotLoaded: boolean;

    showNewBlockSelector: boolean;
    currentSectionItem: number;
    initialPage: string;
    page: PageModel;
    blocksSchema: BlocksSchema;
    dirty: boolean;
    hoveredInPreviewId: number;
    editorMode: string;
}

const initialState: EditorState = {
    pageLoading: false,
    schemaLoading: false,
    pageNotLoaded: false,
    schemaNotLoaded: false,

    showNewBlockSelector: false,
    currentSectionItem: null,
    initialPage: null,
    page: null,
    blocksSchema: null,
    dirty: true,
    hoveredInPreviewId: 0,
    editorMode: 'normal'
};

const editorReducers = createReducer(
    initialState,
    on(Actions.addPageItem, (state, { block }) => ({
        ...state, page: { ...state.page, content: [...state.page.content, block] },
        showNewBlockSelector: false, currentSectionItem: block.id, dirty: true
    })),
    on(Actions.loadBlocksSchema, state => ({ ...state, schemaLoading: true })),
    on(Actions.blocksSchemaLoaded, (state, { schema }) => ({
        ...state, blocksSchema: schema, schemaLoading: false, schemaNotLoaded: false
    })),
    on(Actions.blocksSchemaFail, state => ({ ...state, schemaLoading: false, schemaNotLoaded: true })),
    on(Actions.clearPageChanges, state => ({ ...state, page: JSON.parse(state.initialPage), dirty: false })),
    on(Actions.clonePageItem, (state, { originalBlock, newBlock }) => {
        const content = state.page.content;
        const index = content.indexOf(originalBlock) + 1;
        const page = { ...state.page, content: [...content.slice(0, index), newBlock, ...content.slice(index)] };
        return { ...state, dirty: true, page };
    }),
    on(Actions.swapBlocks, (state, { previousIndex, currentIndex }) => {
        const content = [...state.page.content];
        const element = content.splice(previousIndex, 1);
        content.splice(currentIndex, 0, ...element);
        const page = { ...state.page, content };
        return { ...state, page };
    }),
    on(Actions.completeEditPageItem, state => ({ ...state, currentSectionItem: null })),
    on(Actions.loadPage, state => ({ ...state, pageLoading: true })),
    on(Actions.loadPageFail, state => ({ ...state, pageNotLoaded: true, pageLoading: false })),
    on(Actions.loadPageSuccess, (state, { page }) => ({
        ...state, page, initialPage: JSON.stringify(page), pageLoading: false, pageNotLoaded: false, dirty: false
    })),
    on(Actions.moveBlock, (state, { previousIndex, currentIndex }) => {
        const content = [...state.page.content];
        const element = content.splice(previousIndex, 1);
        const shift = previousIndex >= currentIndex ? 0 : 1;
        content.splice(currentIndex - shift, 0, ...element);
        const page = { ...state.page, content };
        return { ...state, page };
    }),
    on(Actions.orderChanged, state => ({ ...state, dirty: true })),
    on(Actions.removePageItem, (state, { block }) => {
        const content = [...state.page.content];
        const index = content.indexOf(block);
        content.splice(index, 1);
        const page = { ...state.page, content };
        return { ...state, page, currentSectionItem: null, dirty: true };
    }),
    on(Actions.savePage, state => ({ ...state, pageLoading: true })),
    on(Actions.savePageFail, state => ({ ...state, pageLoading: false })),
    on(Actions.savePageSuccess, state => ({ ...state, pageLoading: false, dirty: false, initialPage: JSON.stringify(state.page) })),
    on(Actions.selectPageItem, (state, { blockId }) => ({
        ...state,
        currentSectionItem: blockId,
        showNewBlockSelector: !!blockId ? false : state.showNewBlockSelector
    })),
    on(Actions.toggleNewBlockPane, (state, { display }) => ({ ...state, showNewBlockSelector: display })),
    on(Actions.toggleItemVisibility, (state, { block }) => {
        const content = [...state.page.content];
        const index = content.indexOf(block);
        const page = {
            ...state.page,
            content: [...content.slice(0, index), { ...block, hidden: !block.hidden }, ...content.slice(index + 1)]
        };
        return { ...state, dirty: true, page };
    }),
    on(Actions.markSectionHoveredInPreview, (state, { blockId }) => ({ ...state, hoveredInPreviewId: blockId })),
    on(Actions.setEditorMode, (state, { mode }) => ({ ...state, editorMode: mode })),
    on(Actions.updatePageItem, (state, { block }) => {
        const content = state.page.content;
        const element = content.find(x => x.id === state.currentSectionItem);
        const index = content.indexOf(element);
        const type = state.blocksSchema[element.type].static ? 'settings' : element.type;
        const page = {
            ...state.page,
            content: [...content.slice(0, index), { ...element, ...block, type }, ...content.slice(index + 1)]
        };
        return { ...state, dirty: true, page };
    })
);

export function reducer(state: EditorState, action: Action) {
    return editorReducers(state, action);
}
