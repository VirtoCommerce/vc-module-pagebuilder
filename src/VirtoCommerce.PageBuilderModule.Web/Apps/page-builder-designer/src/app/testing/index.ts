import { SectionModel } from '@models/document/section.model';
import { TemplateModel } from '@models/document/template.model';
import { SectionSchema } from '@models/document/section.schema';
import { SchemasList, SectionState, TemplateState, BlockState } from '@editor/models';
import { EditorDataState } from '@editor/store/data/state';
import { EditorDomainState } from '@editor/store/domain/state';
import { EditorUIState } from '@editor/store/ui/state';
import { SharedState } from '@shared/store/state';
import { ThemeDataState } from '@theme/store/data/state';
import { ThemeDomainState } from '@theme/store/domain/state';
import { ThemeUIState } from '@theme/store/ui/state';
import { TemplateEntry } from '@shared/models';

export function createSection(overrides: Partial<SectionModel> = {}): SectionModel {
  return {
    id: 'section-1',
    type: 'hero',
    hidden: false,
    blocks: [],
    ...overrides,
  } as SectionModel;
}

export function createBlock(overrides: Partial<SectionModel> = {}): SectionModel {
  return {
    id: 'block-1',
    type: 'text',
    hidden: false,
    blocks: [],
    ...overrides,
  } as SectionModel;
}

export function createTemplate(overrides: Partial<TemplateModel> = {}): TemplateModel {
  return {
    settings: createSection({ id: 'settings', type: 'settings' }),
    content: [],
    ...overrides,
  };
}

export function createSchema(overrides: Partial<SectionSchema> = {}): SectionSchema {
  return {
    icon: 'icon',
    type: 'hero',
    name: 'Hero',
    settings: [],
    ...overrides,
  };
}

export function createSchemasList(overrides: Partial<SchemasList> = {}): SchemasList {
  return {
    sections: {},
    blocks: {},
    objects: {},
    shared: {},
    ...overrides,
  };
}

// ── State Builders ────────────────────────────────────────────────

export function createBlockState(overrides: Partial<BlockState> = {}): BlockState {
  return { selected: false, selectable: true, isDragging: false, ...overrides };
}

export function createSectionState(overrides: Partial<SectionState> = {}): SectionState {
  return {
    expanded: false,
    canHaveChildren: false,
    selected: false,
    selectable: true,
    isDragging: false,
    blocks: {},
    ...overrides,
  };
}

export function createTemplateState(overrides: Partial<TemplateState> = {}): TemplateState {
  return {
    id: 'template-1',
    isLoading: false,
    sections: {},
    ...overrides,
  };
}

export function createEditorDataState(overrides: Partial<EditorDataState> = {}): EditorDataState {
  return {
    templates: {},
    schemas: null,
    sharedComponents: {},
    sharedComponentContents: {},
    sharedComponentErrors: {},
    sharedComponentUsageRefreshIdsByTemplate: {},
    sharedComponentDetails: { componentId: null, loading: false, error: null },
    sharedComponentsSearch: {
        keyword: '',
        resultIds: [],
        optimisticResultIds: [],
        loadedCount: 0,
        totalCount: 0,
        loading: false,
        rebasePending: false,
        error: null,
    },
    ...overrides,
  };
}

export function createEditorDomainState(overrides: Partial<EditorDomainState> = {}): EditorDomainState {
  return { states: {}, schemaLoading: false, ...overrides };
}

export function createEditorUIState(overrides: Partial<EditorUIState> = {}): EditorUIState {
  return {
    addSectionPaneStates: {},
    previewItemType: null,
    currentSectionsFilter: null,
    dragSectionIds: [],
    isTemplateLoading: false,
    isSchemasLoading: false,
    hoveredSectionId: null,
    ...overrides,
  };
}

export function createSharedState(overrides: Partial<SharedState> = {}): SharedState {
  return {
    appInitialized: false,
    templatesEntriesLoading: false,
    templatesEntriesLoaded: false,
    templatesEntries: {},
    entriesStates: {},
    templatesFilter: null,
    templateSelected: null,
    childrenTemplatesState: {},
    ...overrides,
  };
}

export function createThemeDataState(overrides: Partial<ThemeDataState> = {}): ThemeDataState {
  return { settings: null, sourceSettings: null, settingsSchema: null, ...overrides };
}

export function createThemeDomainState(overrides: Partial<ThemeDomainState> = {}): ThemeDomainState {
  return { isDirty: false, ...overrides };
}

export function createThemeUIState(overrides: Partial<ThemeUIState> = {}): ThemeUIState {
  return { mode: 'list', presetsFilter: null, settingsLoading: false, schemaLoading: false, ...overrides };
}

export function createEntry(overrides: Partial<TemplateEntry> = {}): TemplateEntry {
  return {
    name: 'Home',
    key: 'home',
    type: 'page',
    previewUrl: '',
    previewRule: '',
    hasChildren: false,
    ...overrides,
  } as TemplateEntry;
}
