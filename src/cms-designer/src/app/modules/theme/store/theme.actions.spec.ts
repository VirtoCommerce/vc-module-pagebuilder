import { HttpErrorResponse } from '@angular/common/http';
import * as themeActions from './theme.actions';

describe('Theme actions', () => {
    describe('loadDefaultThemes', () => {
        it('should create an action', () => {
            const action = themeActions.loadDefaultThemes();
            expect({ ...action }).toEqual({ type: '[Theme] Load Default Themes' });
        });
    });

    describe('loadDefaultThemesSuccess', () => {
        it('should create an action with given payload', () => {
            const presets = {
                current: 'key',
                presets: {
                    key: {
                        key: 'value'
                    }
                }
            };
            const action = themeActions.loadDefaultThemesSuccess({ presets });
            expect({ ...action }).toEqual({ type: '[Theme] Load Default Themes Success', presets: { ...presets } });
        });
    });

    describe('loadDefaultThemesFail', () => {
        it('should create an action with given payload', () => {
            const payload = 'something went wrong';
            const error = new HttpErrorResponse({ error: payload });
            const action = themeActions.loadDefaultThemesFail({ error });
            expect({ ...action }).toEqual({ type: '[Theme] Load Default Themes Fail', error });
        });
    });

    describe('saveTheme', () => {
        it('should create an action', () => {
            const action = themeActions.saveTheme();
            expect({ ...action }).toEqual({ type: '[Theme] Save Theme' });
        });
    });

    describe('saveThemeSuccess', () => {
        it('should create an action', () => {
            const action = themeActions.saveThemeSuccess({ values: { current: 'new' } });
            expect({ ...action }).toEqual({ type: '[Theme] Save Theme Success', values: { current: 'new' } });
        });
    });

    describe('saveThemeFail', () => {
        it('should create an action with given payload', () => {
            const error = 'something went wrong';
            const action = themeActions.saveThemeFail({ error });
            expect({ ...action }).toEqual({ type: '[Theme] Save Theme Fail', error });
        });
    });

    describe('loadSchema', () => {
        it('should create an action', () => {
            const action = themeActions.loadSchema();
            expect({ ...action }).toEqual({ type: '[Theme] Load Schema' });
        });
    });

    describe('loadSchemaSuccess', () => {
        it('should create an action with given payload', () => {
            const schema = [
                {
                    name: 'payload name',
                    icon: 'default',
                    settings: [
                        {
                            type: 'test purposes'
                        }
                    ]
                }
            ];
            const action = themeActions.loadSchemaSuccess({ schema });
            expect({ ...action }).toEqual({ type: '[Theme] Load Schema Success', schema: [...schema] });
        });
    });

    describe('loadSchemaFail', () => {
        it('should create an action with given payload', () => {
            const payload = 'something went wrong';
            const error = new HttpErrorResponse({ error: payload });
            const action = themeActions.loadSchemaFail({ error });
            expect({ ...action }).toEqual({ type: '[Theme] Load Schema Fail', error });
        });
    });

    describe('selectSchemaItem', () => {
        it('should create an action with given payload', () => {
            const item = {
                name: 'test purposes',
                icon: 'default',
                settings: [
                    {
                        type: 'test purposes'
                    }
                ]
            };
            const action = themeActions.selectSchemaItem({ item });
            expect({ ...action }).toEqual({ type: '[Theme] Select Schema Item', item: { ...item } });
        });
    });

    describe('showPresetsPane', () => {
        it('should create an action', () => {
            const action = themeActions.showPresetsPane();
            expect({ ...action }).toEqual({ type: '[Theme] Show Presets Pane' });
        });
    });

    describe('cancelPreset', () => {
        it('should create an action', () => {
            const action = themeActions.cancelPreset();
            expect({ ...action }).toEqual({ type: '[Theme] Cancel Preset' });
        });
    });

    describe('applyPreset', () => {
        it('should create an action with given payload', () => {
            const preset = 'preset-name';
            const action = themeActions.applyPreset({ preset });
            expect({ ...action }).toEqual({ type: '[Theme] Apply preset', preset });
        });
    });

    describe('updateTheme', () => {
        it('should create an action with given payload', () => {
            const payload = {
                color: 'red',
                width: 100,
                active: true
            };
            const action = themeActions.updateTheme({ values: payload });
            expect({ ...action }).toEqual({ type: '[Theme] Update Theme', values: { ...payload } });
        });
    });

    describe('clearThemeChanges', () => {
        it('should create an action', () => {
            const action = themeActions.clearThemeChanges();
            expect({ ...action }).toEqual({ type: '[Theme] Clear Theme Changes' });
        });
    });

    describe('removePreset', () => {
        it('should create an action with given payload', () => {
            const preset = 'preset-to-remove';
            const action = themeActions.removePreset({preset});
            expect({ ...action }).toEqual({ type: '[Theme] Remove Preset', preset });
        });
    });

    describe('createPreset', () => {
        it('should create an action with given payload', () => {
            const preset = 'name-for-new-preset';
            const action = themeActions.createPreset({preset});
            expect({ ...action }).toEqual({ type: '[Theme] Create Preset', preset });
        });
    });

    describe('selectPreset', () => {
        it('should create an action with given payload', () => {
            const preset = 'selected-preset';
            const action = themeActions.previewPreset({ preset });
            expect({ ...action }).toEqual({ type: '[Theme] Preview Preset', preset });
        });
    });
    describe('updateDraft', () => {
        it('should create an action with given payload', () => {
            const action = themeActions.updateDraft();
            expect({ ...action }).toEqual({ type: '[Theme] Update Draft' });
        });
    });
    describe('updateDraftSuccess', () => {
        it('should create an action with given payload', () => {
            const action = themeActions.updateDraftSuccess();
            expect({ ...action }).toEqual({ type: '[Theme] Update Draft Success' });
        });
    });
    describe('updateDraftFail', () => {
        it('should create an action with given payload', () => {
            const error = 'something went wrong';
            const action = themeActions.updateDraftFail({ error });
            expect({ ...action }).toEqual({ type: '[Theme] Update Draft Fail', error });
        });
    });
    describe('closeEditors', () => {
        it('should create an action with given payload', () => {
            const action = themeActions.closeEditors();
            expect({ ...action }).toEqual({ type: '[Theme] Close Editors' });
        });
    });
});
