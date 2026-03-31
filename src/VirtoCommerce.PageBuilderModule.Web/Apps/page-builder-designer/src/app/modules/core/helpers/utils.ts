import { ItemsGroup } from '@core/models';
import { ControlDescriptor } from '@models/controls';
import { SectionSchema } from '@models/index';

export const NO_NAME_GROUP_KEY = '__noname__';
export function groupSections(list: SectionSchema[]): ItemsGroup<SectionSchema>[] {
    if (list && list.length) {
        const groups = list.reduce((acc, value) => {
            const groupName = value.group || NO_NAME_GROUP_KEY;
            if (!acc[groupName]) {
                acc[groupName] = <ItemsGroup<SectionSchema>>{
                    icon: value.groupIcon,
                    name: value.group,
                    items: [],
                    noname: !value.group,
                    sort: value.groupSort
                };
            }
            acc[groupName].items.push(value);
            if (!acc[groupName].icon) {
                acc[groupName].icon = value.groupIcon;
            }
            return acc;
        }, <any>{});
        return Object.keys(groups).map(key => groups[key]);
    }
    return [];
}

export function createDefaultObject(settings: ControlDescriptor[]): any {
    if (!settings) {
        return {};
    }
    return settings.filter(x => typeof(x.default) !== 'undefined').reduce((acc, value) => ({...acc, [<string>value.id] : value.default}), {});
}

export function parseIntOrDefault(v: any, defaultValue: number): number {
    const result = v ? parseInt(v, 10) : defaultValue;
    return isNaN(result) ? defaultValue : result;
}
