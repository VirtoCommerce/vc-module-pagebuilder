import { Dictionary } from '@models/index';


export function findInObjectOrFirst<T>(obj: Dictionary<T>, comparerFn: (item: T, key: string) => boolean): { key: string | null, obj: T | null } {
    const keys = Object.keys(obj);
    if (keys.length) {
        const result = keys.find(key => comparerFn(obj[key], key)) || keys[0];
        return { key: result, obj: obj[result] };
    }
    return { key: null, obj: null };
}

