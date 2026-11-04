import { JSONPath } from 'jsonpath-plus';

import { ValueDescriptorModel } from '@models/index';

import { evaluate, UnsupportedExpressionError } from './safe-expression';
import { KNOWN_EXPRESSIONS } from './known-expressions';

export function spreadPropertyByOther(obj: any, keyProperty: string, ...spreadProperties: string[]): any {
  const groups = Object.keys(obj).reduce((acc, key) => {
    const group = obj[key][keyProperty];
    if (!acc) {
      return acc;
    }

    return {
      ...acc,
      [group]: spreadProperties.reduce((propsAcc, prop) => ({
        ...propsAcc,
        [prop]: obj[key][prop] || acc?.[group]?.[prop]
      }), <any>{}),
    };
  }, <any>{});
  return Object.keys(obj).reduce((acc, key) => {
    const group = obj[key][keyProperty] as string;
    return {
      ...acc,
      [key]: {
        ...obj[key],
        ...(group ? groups[group] : null)
      }
    };
  }, {});
}

export function generateAnchor(value: string): string {
  if (!value) {
    return generateUniqueString(10);
  }
  // replace spaces with dashes
  return value.toLowerCase()
    .replaceAll(/[^\w\s-]+/g, '')
    .replaceAll(/\n$/g, '')
    .replaceAll(/\s+/g, '-')
    .replaceAll(/^-+|-+$/g, '');
}

export function generateUniqueString(length: number): string {
  const characters = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_-';
  const randomChar = () => characters[Math.floor(Math.random() * characters.length)];
  return Array.from({ length }, randomChar).join('');
}

export function onlyLettersAndDigits(value: string): string {
  if (value) {
    return value.replaceAll(/[^a-zA-Z0-9]/g, '');
  }
  return value;
}

// original regex was defined as: const nargs = /\{\{([=0-9a-zA-Z_\.]+)\}\}/g;
const nargs = /\{\{(.+?)\}\}/g;

const rawExpr = /^@\{\{(.+)\}\}$/;

export function template(value: string, ...args: any) {

  const values = (args?.length === 1 && typeof args[0] === 'object' ? args[0] : args) || {};

  // @{{expr}} — return raw result preserving type (array, object, etc.)
  const rawMatch = rawExpr.exec(value);
  if (rawMatch) {
    const result = evalInContext(rawMatch[1], values);
    return result ?? null;
  }

  return value.replaceAll(nargs, (match, i, index) => {
    let result;

    if (value[index - 1] === '{' &&
      value[index + match.length] === '}') {
      return `{${i}}`;
    } else if (i.startsWith('=')) {
      result = evalInContext(i.substring(1), values);
    }
    else {
      result = getValueByPath(values, i);
    }
    if (result === null || result === undefined) {
      return '';
    }
    return result;
  });
}

const EVAL_UTILS = {
  combine,
  getValueOrDefault,
  getValueByPath,
  stripHtmlTags,
  toList,
  cutString,
  tryParseJson,
  getItemValue,
  filterBy,
  toOptions,
};

export function evalInContext(expr: string, context: any): any {
  try {
    return evaluate(expr, context, EVAL_UTILS, KNOWN_EXPRESSIONS);
  } catch (e) {
    if (e instanceof UnsupportedExpressionError) {
      console.warn('Expression not executed: it is not one of the supported forms', expr, e.message);
    } else {
      console.log(expr, context, e);
    }
  }
  return null;
}

// A filter or a script in a path is schema text as well, so it runs through the expression language and not
// through jsonpath-plus's own interpreter, which calls whatever methods the data has. jsonpath-plus hands over
// the script with `@` already spelled `_$_v` (and `@root` `_$_root`, ...) and those names on the sandbox.
function evalJsonPathScript(script: string, sandbox: object): unknown {
  return evaluate(script.replaceAll(/(?<![\w$.])_\$_/g, 'this._$_'), sandbox, EVAL_UTILS, KNOWN_EXPRESSIONS);
}

export function getValueOrDefault(value: any, defaultValue: any = null) {
  if (value === 0 || value === null || value === false || value === '') {
    return value;
  }
  return value || defaultValue;
}

export function getValueByPath(model: any, path: any): any {
  let value: unknown;
  try {
    value = JSONPath({ path, json: model, wrap: true, eval: evalJsonPathScript });
  } catch (e) {
    console.warn('JSONPath not evaluated', path, (e as Error)?.message);
    return null;
  }
  const result = Array.isArray(value) ? value[0] : value;
  if (typeof result === 'function') {
    return null;
  }
  return result;
}

export function stripHtmlTags(str: string) {
  return str.replaceAll(/<[^>]*>/g, ' ');
}

export function combine(...parts: string[]): string {
  return parts.reduce((acc, part, index) => {
    if (!part) {
      return acc;
    }
    if (index === 0) {
      return part;
    }
    if (acc.endsWith('/') && part.startsWith('/')) {
      return acc + part.substring(1);
    }
    if (!acc.endsWith('/') && !part.startsWith('/')) {
      return acc + '/' + part;
    }
    return acc + part;
  }, '');
}

export function toList(obj: any, keyPropertyName: string): any[] {
  return Object.keys(obj).map(key => ({ [keyPropertyName]: key, ...obj[key] }));
}

/** The items of a list whose own `field` equals `value`: expressions have no callbacks to filter with. */
export function filterBy(list: unknown, field: string, value: unknown): any[] {
  return Array.isArray(list) ? list.filter(item => ownValue(item, field) === value) : [];
}

/** Select options from a list: the label is the first of `labelFields` an item has, the value is its own `valueField`. */
export function toOptions(list: unknown, labelFields: string | string[], valueField: string): { label: unknown; value: unknown }[] {
  if (!Array.isArray(list)) {
    return [];
  }
  const fields = Array.isArray(labelFields) ? labelFields : [labelFields];
  return list.map(item => ({
    label: fields.map(field => ownValue(item, field)).find(Boolean),
    value: ownValue(item, valueField),
  }));
}

function ownValue(item: unknown, field: string): unknown {
  return item !== null && typeof item === 'object' && Object.hasOwn(item, field) ? (item as any)[field] : undefined;
}

export function tryParseJson(value: string): any {
  try {
    return JSON.parse(value);
  } catch (e) {
    return null;
  }
}

export function getItemValue(item: any, descriptor: (string | ValueDescriptorModel)[]): any {
  const result: any = {};
  descriptor.forEach(p => {
    const [query, property, isArray] = typeof p === 'string' ? [p, p, null] : [p.query, p.key, p.isArray];
    const x = getValueByPath(item, query);
    result[property] = arrayCastByConfig(x, isArray);
  });
  return result;
}

export function arrayCastByConfig(item: any, isArray: boolean | null = null): any {
  if (isArray === null) {
    return item;
  }
  if (Array.isArray(item) && !isArray) {
    if (item.length > 0) {
      return item[0];
    } else {
      return null;
    }
  } else if (!Array.isArray(item) && isArray) {
    return [item];
  }
  return item;
}

export function cutString(value: string, length = 50): string {
  if (!value) return '';
  if (value.length <= length) return value;
  return value.substring(0, length) + '...';
}
