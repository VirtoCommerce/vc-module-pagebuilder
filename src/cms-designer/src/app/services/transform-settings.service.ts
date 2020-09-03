import { isArray } from 'util';
import { Injectable } from '@angular/core';

import { BlockSchema, ValueType } from '@shared/models';
import { PresetsModel } from '@themes/models';

@Injectable({
    providedIn: 'root'
})
export class TransformSettingsService {

    transform(themes: { presets: PresetsModel, basePresets: PresetsModel }): { presets: PresetsModel, basePresets: PresetsModel } {
        const currentTheme = this.readCurrentSettings(themes.presets, themes.basePresets);
        const baseTheme = this.readBaseSettings(themes.presets, themes.basePresets);
        const result = { ...baseTheme, ...currentTheme };
        return { presets: { current: result, presets: themes.presets.presets }, basePresets: themes.basePresets };
    }

    cleanSettings(model: { [key: string]: ValueType }, presets: PresetsModel, basePresets: PresetsModel, schema: BlockSchema[]): PresetsModel {
        const result: any = {};
        const baseModel = this.readBaseSettings(presets, basePresets);
        Object.keys(model).forEach(key => {
            const value = model[key];
            const baseValue = baseModel[key];
            if (Array.isArray(value) && Array.isArray(baseValue)) {
                const values = <Array<ValueType>>value;
                if (values.length === baseValue.length) {
                    const hasDiffs = values.find((x, i) => baseValue[i] != x);
                    if (hasDiffs) {
                        result[key] = value;
                    }
                }
            } else if (value !== baseValue) {
                result[key] = value;
            }
        });
        schema.forEach(s => s.settings.forEach(x => {
            const value = result[x.id];
            if (typeof value !== undefined && value === x.default) {
                delete result[x.id];
            }
        }));
        return {
            current: result,
            presets: presets.presets
        };
    }

    private readBaseSettings(currentTheme: PresetsModel, baseTheme: PresetsModel) {
        const baseThemeCurrent = baseTheme.current;
        if (typeof baseThemeCurrent !== 'string')
        {
            return baseThemeCurrent;
        }
        const currentThemeCurrent = currentTheme.current;
        const resultPreset = typeof currentThemeCurrent === 'string'
            ? this.getPreset(currentThemeCurrent, baseTheme)
            : this.getPreset(baseThemeCurrent, baseTheme);
        return resultPreset || {};
    }

    private readCurrentSettings(currentTheme: PresetsModel, baseTheme: PresetsModel) {
        var current = currentTheme.current;
        if (typeof current === 'string') {
            return this.getPreset(current, currentTheme) || this.getPreset(current, baseTheme) || {};
        }
        return current;
    }

    private getPreset(name: string, theme: PresetsModel): any {
        if (theme && theme.presets)
            return theme.presets[name];
        return null;
    }
}