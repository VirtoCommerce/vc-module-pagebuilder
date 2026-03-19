export interface ThemeUIState {
    mode: 'list' | 'tile';
    presetsFilter: string | null;
    settingsLoading: boolean;
    schemaLoading: boolean;
};

export const initialState: ThemeUIState = {
    mode: 'list',
    presetsFilter: null,
    settingsLoading: false,
    schemaLoading: false
};
