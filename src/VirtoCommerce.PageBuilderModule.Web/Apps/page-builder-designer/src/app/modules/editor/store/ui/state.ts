import { Dictionary } from "@models/index";

export interface EditorUIState {
    addSectionPaneStates: Dictionary<{
        opened: boolean;
    }>;
    previewItemType: string | null;
    currentSectionsFilter: string | null;
    dragSectionIds: string[];
    isTemplateLoading: boolean;
    isSchemasLoading: boolean;
    hoveredSectionId: string | null;
};

export const initialState: EditorUIState = {
    addSectionPaneStates: {},
    previewItemType: null,
    currentSectionsFilter: null,
    dragSectionIds: [],
    isTemplateLoading: false,
    isSchemasLoading: false,
    hoveredSectionId: null,
};
