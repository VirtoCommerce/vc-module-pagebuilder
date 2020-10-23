import { BlockSchema, ComponentContext } from '@shared/models';
import { CreateBlockModel } from '@editor/models';
import { createFeatureSelector, createSelector } from '@ngrx/store';

import * as fromRoot from 'src/app/store';
import * as fromEditor from './editor.reducer';

export interface State extends fromRoot.State {
    editor: fromEditor.EditorState;
}

const getEditorFeatureState = createFeatureSelector<fromEditor.EditorState>('editor');

export const getIsDirty = createSelector(
    getEditorFeatureState,
    state => !state.pageNotLoaded && state.dirty
);

// export const getCategories = createSelector(
//     getEditorFeatureState,
//     state => state.categories
// );

export const getAddNewSectionMode = createSelector(
    getEditorFeatureState,
    state => state.showNewBlockSelector
);

export const getBlocksSchema = createSelector(
    getEditorFeatureState,
    state => state.blocksSchema
);

export const getPage = createSelector(
    getEditorFeatureState,
    state => state.page
);

export const getPageForEdit = createSelector(
    getEditorFeatureState,
    getBlocksSchema,
    (state, schema) => state.page && schema ? <any>{
        ...state.page,
        content: state.page.content.filter(x => schema[x.type])
    } : null
);

export const getCurrentSectionItem = createSelector(
    getEditorFeatureState,
    getPage,
    getBlocksSchema,
    (state, page, schema) => {
        if (page && schema && state.currentSectionItem) {
            if (typeof (state.currentSectionItem) !== 'string') {
                return page.content.find(x => x.id === state.currentSectionItem)
            } else {
                // todo: is it correct to returns new object from state?
                return { ...page.settings, type: state.currentSectionItem };
            }
        }
        return null;
    }
);

export const getTabs = createSelector(
    getCurrentSectionItem,
    getBlocksSchema,
    (block, schema) => {
        if (block && schema) {
            const tabs = schema[block.type].settings.reduce(
                (result, list) => result.indexOf(list.tab || 'General') === -1
                    ? result.concat(list.tab || 'General')
                    : result,
                []
            ).sort();
            return tabs;
        }
        return [];
    }
);

export const getCurrentTab = createSelector(
    getEditorFeatureState,
    getTabs,
    (state, tabs) => {
        if (!!state.currentEditorTab && tabs.indexOf(state.currentEditorTab) !== -1) {
            return state.currentEditorTab;
        }
        return tabs[0];
    }
);

export const getIsLoading = createSelector(
    getEditorFeatureState,
    state => state.pageLoading || state.schemaLoading
);

export const getPageLoading = createSelector(
    getEditorFeatureState,
    state => state.pageLoading
);

export const getSchemaLoading = createSelector(
    getEditorFeatureState,
    state => state.schemaLoading
);

export const getSchemaNotLoaded = createSelector(
    getEditorFeatureState,
    state => state.schemaNotLoaded
);

export const getPageNotLoaded = createSelector(
    getEditorFeatureState,
    state => !state.schemaNotLoaded && state.pageNotLoaded
);

export const getHoveredId = createSelector(
    getEditorFeatureState,
    state => state.hoveredInPreviewId
);

export const getPageTitle = createSelector(
    getPage,
    page => {
        if (page != null && page.settings) {
            return page.settings.header || 'CMS Editor';
        }
        return 'CMS Editor';
    }
);

export const getPageLayout = createSelector(
    getPage,
    page => page != null && page.settings ? <string>page.settings['layout'] : null
);

export const getCurrentBlockName = createSelector(
    getCurrentSectionItem,
    section => {
        if (section) {
            return section.name || 'block';
        }
        return null;
    }
);

export const getEditorMode = createSelector(
    getEditorFeatureState,
    state => state.editorMode
);

export const getItemsForCreate = createSelector(
    getBlocksSchema,
    (schema) => {
        const result: CreateBlockModel = {
            items: [],
            groups: []
        };
        const groups: { [key: string]: BlockSchema[] } = {};
        Object.keys(schema).filter(x => !schema[x].static && !schema[x].hide).forEach(x => {
            const category = schema[x].category;
            if (!category) {
                result.items.push(schema[x]);
            } else {
                if (!groups[category]) {
                    groups[category] = [];
                }
                groups[category].push(schema[x]);
            }
        });
        Object.keys(groups).forEach(x => result.groups.push({ name: x, items: groups[x] }));
        return result;
    }
);

export const getEditItemContext = createSelector(
    getEditorFeatureState,
    getCurrentSectionItem,
    (state, block) => (<ComponentContext>{ page: state.page.content, settings: state.page.settings, block, filter: state.currentEditorTab, mode: state.editorMode })
);
