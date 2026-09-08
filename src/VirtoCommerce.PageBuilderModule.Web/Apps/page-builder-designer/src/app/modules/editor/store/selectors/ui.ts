import { createSelector } from "@ngrx/store";

import { ActionButtonDescriptor } from '@core/models';
import {
  selectTemplateUIState,
  selectCurrentSectionsFilter
} from './common';

import { PageVersion, PageVersionGroup, SectionStatesList, SectionState } from '@editor/models';
import { EditorModuleInfo } from '@models/modules';

import * as fromRoute from '@shared/routing';
import * as fromDomain from "./domain";
import * as fromData from "./data";
import * as fromShared from '@shared/store';

import { helpers, anchorHelpers } from "@editor/helpers";
import { appHelpers } from "@integration/helpers";

export const selectAddItemTitle = createSelector(
  fromData.selectSectionModelFromRoute,
  fromData.selectCurrentSchemaForEdit,
  (section, schema) => {
    if (!section)
      return 'Add block';
    const name = helpers.getSectionName(section, schema || null, 'section');
    return `Add block to '${name}'`;
  }
);

export const hoveredSectionId = createSelector(
  selectTemplateUIState,
  state => state.hoveredSectionId
);

export const isLoading = createSelector(
  selectTemplateUIState,
  state => !!(state?.isTemplateLoading || state?.isSchemasLoading)
);

export const selectAddSectionPaneGroupStates = createSelector(
  selectTemplateUIState,
  state => state.addSectionPaneStates
);

export const selectPreviewItemType = createSelector(
  selectTemplateUIState,
  state => state.previewItemType
);

export const selectCurrentDragSections = createSelector(
  selectTemplateUIState,
  state => state.dragSectionIds
);

function getSelectedIds(states: SectionStatesList): string[] {
  const result = [];
  for (const key in states) {
    if (states[key].selected) {
      result.push(key);
    }
    if (result.length === 0) {
      for (const blockKey in states[key].blocks) {
        if (states[key].blocks[blockKey].selected) {
          result.push(blockKey);
        }
      }
      if (result.length > 0) {
        break;
      }
    }
  }
  return result;
}

export const selectCheckedItems = createSelector(
  fromDomain.selectCurrentTemplateState,
  state => getSelectedIds(state?.sections || {})
);

export const hasSelectedSection = createSelector(
  fromDomain.selectCurrentTemplateState,
  state => !!state?.sections && Object.values(state.sections).some(x => x.selected)
);

export const selectKeyOfSectionWithSelectedBlock = createSelector(
  fromDomain.selectCurrentTemplateState,
  state => Object.keys(state?.sections || {}).find(x => Object.values(state?.sections[x].blocks || {}).some(b => b.selected))
);

export const selectSectionsState = createSelector(
  fromDomain.selectCurrentTemplateState,
  fromData.selectCurrentTemplateModel,
  fromData.selectSectionsSchemas,
  selectCurrentDragSections,
  hasSelectedSection,
  selectKeyOfSectionWithSelectedBlock,
  (state, model, schemas, dragSectionIds, isSectionSelected, sectionKeyWithSelectedBlock) => {
    return (schemas && model?.content.filter(x => x.type && x.id).reduce((result, section) => {
      const canHaveChildren = (schemas[section.type]?.blocks?.length || 0) > 0;
      return <SectionStatesList>{
        ...result,
        [section.id]: <SectionState>{
          expanded: canHaveChildren,
          canHaveChildren,
          isDragging: dragSectionIds.includes(section.id),
          selectable: !sectionKeyWithSelectedBlock,
          ...state?.sections[section.id],
          blocks: canHaveChildren ? section.blocks?.reduce((res, v) => ({
            ...res,
            [v.id]: {
              isDragging: dragSectionIds.includes(v.id),
              selected: res[v.id]?.selected || false,
              selectable: !isSectionSelected && (!sectionKeyWithSelectedBlock || sectionKeyWithSelectedBlock === section.id),
            }
          }), state?.sections[section.id]?.blocks || {}) : {},
        }
      };
    }, <SectionStatesList>{})) || <SectionStatesList>{};
  }
);

const templateDataContext = createSelector(
  fromData.selectCurrentTemplateModel,
  fromData.selectSectionsSchemas,
  fromData.selectBlocksSchemas,
  fromData.selectTemplateSettings,
  fromData.selectCurrentTemplateSettingsSchemas,
  (template, sectionsSchemas, blocksSchemas, settings, settingsSchemas) => ({
    template, sectionsSchemas, blocksSchemas, settings, settingsSchemas
  })
);

export const editTemplateContext = createSelector(
  fromDomain.selectCurrentTemplateState,
  templateDataContext,
  selectSectionsState,
  selectCurrentDragSections,
  (templateState, dataContext, sectionsState, currentDragSection) => {
    const selectedSectionsCount = Object.values(sectionsState).filter(x => x.selected).length;
    const selectedBlocksCount = Object.values(sectionsState).reduce((acc, value) => acc + Object.values(value.blocks || {}).filter(x => x.selected).length, 0);
    const { template, sectionsSchemas, blocksSchemas, settings, settingsSchemas } = dataContext;
    return template && sectionsSchemas && blocksSchemas
      ? {
        template, templateState, sectionsState, sectionsSchemas, blocksSchemas, settings, settingsSchemas,
        selectedSectionsCount, selectedBlocksCount,
        currentDragSection,
        selectMode: selectedSectionsCount > 0 || selectedBlocksCount > 0
      }
      : null;
  }
);

export const selectAddItemContext = createSelector(
  fromData.selectGroupedSectionSchemas,
  selectAddSectionPaneGroupStates,
  selectPreviewItemType,
  selectCurrentSectionsFilter,
  fromData.selectSectionModelFromRoute,
  ({ groups, items }, states, previewItemType, filter, section) => ({
    groups,
    items,
    parentSection: section,
    states: {
      groups: groups.reduce((acc, value) => ({
        ...acc,
        [value.name]: <any>{
          ...states[value.name],
          opened: states[value.name]?.opened || !!filter
        },
      }), <any>{}),
      previewItemType
    },
  })
);

const isEditSettings = createSelector(
  fromRoute.getModeName,
  mode => mode === EditorModuleInfo.mode.editSettings // means page (or template) settings, static block
);

export const selectCurrentItemName = createSelector(
  fromData.selectBlockModelFromRoute,
  fromData.selectSectionModelFromRoute,
  fromData.selectSettingsFromRoute,
  fromData.selectCurrentSchemaForEdit,
  (block, section, settings, schema) => {
    const itemType = block ? 'current block' : 'current section';
    const defaultName = settings ? (<string>schema?.['name'] || 'settings') : itemType;
    const name = helpers.getSectionName(block || section || settings || null, schema || null, defaultName);
    return 'Edit ' + name;
  }
);

const sectionDataContext = createSelector(
  fromData.selectSectionModelFromRoute,
  fromData.selectSectionSchemaFromRoute,
  (section, sectionSchema) => ({ section, sectionSchema })
);

const blockDataContext = createSelector(
  fromData.selectBlockModelFromRoute,
  fromData.selectBlockSchemaFromRoute,
  (block, blockSchema) => ({ block, blockSchema })
);

const currentItemDataContext = createSelector(
  fromData.selectCurrentItemForEdit,
  fromData.selectCurrentSchemaForEdit,
  (model, schema) => ({ model, schema })
);

export const selectEditSectionContext = createSelector(
  sectionDataContext,
  blockDataContext,
  currentItemDataContext,
  fromData.selectCurrentTemplateModel,
  fromData.selectObjectsSchemas,
  isEditSettings,
  (sectionContext, blockContext, { model, schema }, template, objects, isSettings) =>
    !!schema && !!model
      ? <any>{
        section: sectionContext.section,
        sectionSchema: sectionContext.sectionSchema,
        block: blockContext.block,
        blockSchema: blockContext.blockSchema,
        schema, model,
        isEditSettings: isSettings,
        // Link target this item exposes on the rendered page, shown so a content manager can build
        // a table of contents without guessing (VCST-5704).
        anchor: anchorHelpers.getItemAnchor(model),
        editContext: {
          model, // current item under editing, can be block, section or settings
          block: blockContext.block, // current block or null
          section: sectionContext.section, // current section, useful in block
          template, // current template
          schema: schema,
          sectionSchema: blockContext.blockSchema,
          blockSchema: sectionContext.sectionSchema,
          objects,
          utils: appHelpers,
          page: template?.content,
          settings: template?.settings,
        }
      }
      : null
);

const routeTemplateContextParameters = createSelector(
  fromRoute.selectTemplateKeyParameter,
  fromRoute.selectSectionIdParameter,
  fromRoute.selectBlockIdParameter,
  fromRoute.selectInsertIndexParameter,
  (templateKey, sectionId, blockId, insertIndex) => ({ templateKey, sectionId, blockId, insertIndex })
);

export const changeTemplateContext = createSelector(
  templateDataContext,
  routeTemplateContextParameters,
  fromData.selectSectionModelFromRoute,
  fromData.selectBlockModelFromRoute,
  fromShared.selectCurrentTemplateEntry,
  ({ template, sectionsSchemas, blocksSchemas }, { templateKey, sectionId, blockId, insertIndex }, section, block, templateEntry) =>
    ({ template, section, block, sectionsSchemas, blocksSchemas, templateKey, sectionId, blockId, insertIndex, templateEntry })
);

export const selectToolbarButtonsState = (context: { useTheme: boolean, useDrafts: boolean, useUnpublish: boolean, useExternalPreview: boolean, useHistory?: boolean }) => createSelector(
  // fromDomain.selectCurrentTemplateState,
  fromShared.hasDirty,
  fromDomain.selectCurrentTemplateState,
  (hasDirty, state) => {
    const result = <ActionButtonDescriptor[][]>[];
    if (context.useTheme) {
      result.push([
        {
          icon: 'settings',
          alias: 'theme-settings',
          title: 'Theme settings',
          type: 'outline'
        }
      ]);
    }

    if (context.useExternalPreview) {
      result.push([
        {
          canAction: !hasDirty,
          icon: 'visibility',
          alias: 'external-preview',
          title: 'Preview',
          type: 'outline'
        }
      ]);
    }

    // Only a store whose pages live in git has versions to show, and the server says so by offering the
    // "history" descriptor. The count is of unpublished versions that are neither mine nor bulk imports:
    // it means "somebody else has work here that production does not have", which is the case this whole
    // feature exists for — an edit made outside the builder used to be invisible until it was published.
    if (context.useHistory) {
      const otherDrafts = state?.history?.otherDraftCount ?? 0;
      result.push([
        {
          icon: 'history',
          alias: 'history',
          title: otherDrafts > 0 ? `Version history (${otherDrafts})` : 'Version history',
          type: 'outline'
        }
      ]);
    }

    if (context.useDrafts && !state?.isLoading && !state?.error) {
      const buttons = <ActionButtonDescriptor[]>[];

      // Both flows can take a page down — with pages in git that means deleting it from the production
      // branch — but only a store configured for it gets the button, and the server says so by whether
      // it offers the descriptor at all.
      if (context.useUnpublish) {
        buttons.push({
          canAction: !hasDirty && state?.published && !state?.hasChanges && !state?.pending,
          icon: 'unpublished',
          alias: 'unpublish',
          title: 'Unpublish',
          type: 'outline'
        });
      }

      buttons.push({
        // a pull request for this page is already open — publishing again would achieve nothing
        canAction: !hasDirty && state?.hasChanges && !state?.pending,
        icon: 'publish',
        alias: 'publish',
        title: state?.pending ? 'Publishing…' : 'Publish',
        type: 'outline'
      });

      result.push(buttons);
    }

    // [
    //     {
    //         alias: 'preview',
    //         title: 'Preview',
    //         type: 'outline'
    //     }
    // ],
    // [
    //     {
    //         canAction: false,
    //         icon: 'undo',
    //         alias: 'undo'
    //     },
    //     {
    //         canAction: false,
    //         icon: 'redo',
    //         alias: 'redo'
    //     }
    // ],

    result.push([
      {
        canAction: hasDirty,
        title: 'Save',
        alias: 'save',
        type: 'primary'
      }
    ]);

    return result;
  }
);

/**
 * The open page's versions, with runs of consecutive commits by the same author on the same branch folded
 * into one row.
 *
 * Every save is a commit, so an afternoon of editing arrives as a stack of near-identical entries — twelve
 * within three hours on one page of the content repository. Unfolded, the list buries the versions somebody
 * would actually want to go back to.
 */
export const selectPageHistory = createSelector(
  fromDomain.selectCurrentTemplateState,
  fromShared.hasDirty,
  (state, hasDirty) => {
    const history = state?.history;
    if (!history) {
      return null;
    }

    const groups: PageVersionGroup[] = [];
    for (const version of history.versions) {
      const previous = groups[groups.length - 1];
      if (previous && sameRun(previous.version, version)) {
        previous.older.push(version);
      } else {
        groups.push({ version, older: [] });
      }
    }

    // unsaved edits block a restore: it re-reads the page from the branch, which would drop them
    return { ...history, groups, hasDirty };
  }
);

const RunWindowMs = 15 * 60 * 1000;

function sameRun(head: PageVersion, next: PageVersion): boolean {
  // published and unpublished versions are never folded together: whether a version is live is the first
  // thing the panel says about it
  if (head.published !== next.published || head.bulk !== next.bulk) {
    return false;
  }
  if ((head.author?.email ?? '') !== (next.author?.email ?? '') || head.branches[0] !== next.branches[0]) {
    return false;
  }
  const gap = Date.parse(head.date ?? '') - Date.parse(next.date ?? '');
  return Number.isFinite(gap) && gap >= 0 && gap <= RunWindowMs;
}
