import settings from '../../../../data/settings.json';

import { KNOWN_EXPRESSIONS } from './known-expressions';
import { evaluate, isSupportedExpression, UnsupportedExpressionError } from './safe-expression';
import { combine, filterBy, getValueByPath, toOptions } from './utils';

// vc-frontend's section schemas: not in this repository, so listed as they ship.
const VC_FRONTEND_EXPRESSIONS = [
  "!!this.item && this.item.action === 'url'",
  "!this.model.background",
  "Object.keys(this.response).map(x => ({ ...this.item, ...this.response[x], key: x, request: undefined })).reduce((acc, x) => ({ ...acc, [x.key]: x }), {})",
  "Object.keys(this.response).map(x => ({ ...this.item, ...this.response[x], request: undefined, key: x })).reduce((acc, x) => ({ ...acc, [x.key]: x }), {})",
  "this.model.countLimitation !== false",
  "this.model.countLimitation === true",
  "this.model.hideViewModeSelector !== false",
];

// Found the way the designer finds them, so that the list cannot drift from the settings.
function settingsExpressions(value: unknown, key = ''): string[] {
  if (typeof value === 'string') {
    if (key === 'selector') {
      return [value];
    }
    const raw = /^@\{\{(.+)\}\}$/.exec(value);
    if (raw) {
      return [raw[1]];
    }
    return [...value.matchAll(/\{\{(.+?)\}\}/g)].map(match => match[1]).filter(x => x.startsWith('=')).map(x => x.substring(1));
  }
  if (value && typeof value === 'object') {
    return Object.entries(value).flatMap(([childKey, child]) => settingsExpressions(child, Array.isArray(value) ? key : childKey));
  }
  return [];
}

const SETTINGS_EXPRESSIONS = settingsExpressions(settings);

describe('safe expressions', () => {
  it('finds the expressions of the designer settings', () => {
    expect(SETTINGS_EXPRESSIONS.length).toBeGreaterThan(20);
  });

  it('supports every expression that settings and schemas ship', () => {
    const unsupported = [...SETTINGS_EXPRESSIONS, ...VC_FRONTEND_EXPRESSIONS]
      .filter(expression => !isSupportedExpression(expression, KNOWN_EXPRESSIONS));

    expect(unsupported).toEqual([]);
  });

  it('reads the context through this', () => {
    expect(evaluate('this.model.countLimitation !== false', { model: { countLimitation: true } })).toBe(true);
    expect(evaluate('!this.model.background', { model: {} })).toBe(true);
  });

  it('evaluates arithmetic, logic and conditionals', () => {
    expect(evaluate('1 + 2 * 3', {})).toBe(7);
    expect(evaluate("this.a ? 'yes' : 'no'", { a: 0 })).toBe('no');
    expect(evaluate('this.items || []', { items: null })).toEqual([]);
    expect(evaluate('this.value ?? 5', { value: 0 })).toBe(0);
  });

  it('calls the helpers it is given and plain string and array methods', () => {
    expect(evaluate("combine(this.url, '/designer-preview')", { url: 'https://store' }, { combine })).toBe('https://store/designer-preview');
    expect(evaluate("(this.permissions || []).indexOf('builder:theme') === -1", { permissions: ['builder:theme'] })).toBe(false);
    expect(evaluate('encodeURIComponent(this.folder)', { folder: 'a b' })).toBe('a%20b');
    expect(evaluate('JSON.stringify(this.content, null, 2)', { content: { a: 1 } })).toBe('{\n  "a": 1\n}');
  });

  it('runs a known form as its own code', () => {
    const response = { hero: { title: 'Hero' } };

    const result = evaluate(
      'Object.keys(this.response).map(x => ({ ...this.item, ...this.response[x], request: undefined, key: x })).reduce((acc, x) => ({ ...acc, [x.key]: x }), {})',
      { response, item: { type: 'page' } },
      {},
      KNOWN_EXPRESSIONS,
    );

    expect(result).toEqual({ hero: { type: 'page', title: 'Hero', key: 'hero', request: undefined } });
  });

  it('runs the asset url form like the expression it replaces', () => {
    const expression = "(function(url) { if (!url) return ''; var trimmed = url.trim(); if (!trimmed) return ''; if (/^(?:[a-z][a-z\\d+\\-.]*:)?\\/\\//i.test(trimmed) || trimmed.indexOf('data:') === 0) return trimmed; var normalized = trimmed.charAt(0) === '/' ? trimmed : '/' + trimmed; return normalized.toLowerCase().indexOf('/assets/') === 0 ? normalized : combine('/assets', normalized); })(this.assetUrl)";

    expect(evaluate(expression, { assetUrl: 'images/a.png' }, { combine }, KNOWN_EXPRESSIONS)).toBe('/assets/images/a.png');
    expect(evaluate(expression, { assetUrl: '/assets/a.png' }, { combine }, KNOWN_EXPRESSIONS)).toBe('/assets/a.png');
    expect(evaluate(expression, { assetUrl: 'https://cdn/a.png' }, { combine }, KNOWN_EXPRESSIONS)).toBe('https://cdn/a.png');
    expect(evaluate(expression, { assetUrl: ' ' }, { combine }, KNOWN_EXPRESSIONS)).toBe('');
  });

  [
    "(function() { return document.cookie })()",
    'this.constructor.constructor("alert(1)")()',
    'this.__proto__',
    "this['constructor']",
    'alert(1)',
    'window.location',
    'fetch("https://elsewhere.example")',
    'this.items.map(x => x)',
    'new Function("return 1")()',
    'this.a = 1',
    "'abc'.constructor",
    'this.toString()',
    'constructor',
    'toString',
    '__proto__',
    'hasOwnProperty',
  ].forEach(expression => {
    it(`does not run ${expression}`, () => {
      expect(() => evaluate(expression, { items: [], a: 0 })).toThrowError(UnsupportedExpressionError);
    });
  });

  it('ends the whole chain at an optional link that meets nothing', () => {
    expect(evaluate("this.a?.b.c || 'x'", {})).toBe('x');
    expect(evaluate('this.a?.b.trim()', {})).toBeUndefined();
    expect(evaluate('this.a?.b', { a: { b: 1 } })).toBe(1);
  });

  it('reports a call on nothing as a runtime error, not as an unsupported form', () => {
    expect(() => evaluate("this.permissions.indexOf('builder:theme')", {})).toThrowError(TypeError);
  });

  it('does not silently drop a JSON.stringify replacer', () => {
    expect(() => evaluate("JSON.stringify(this.x, ['id'])", { x: { id: 1, name: 'a' } })).toThrowError(UnsupportedExpressionError);
  });

  it('evaluates the selector example of the template syntax docs', () => {
    const response = { settings: [{ name: 'MySettingName', value: 'a\nb' }] };
    const expression = `(getValueByPath(this.response, "$.settings[?(@.name=='MySettingName')].value") || '').split('\\n')`;

    expect(evaluate(expression, { response }, { getValueByPath })).toEqual(['a', 'b']);
  });

  it('builds select options from the page without callbacks', () => {
    const page = [
      { type: 'popup', __id: 'p1', name: 'Promo' },
      { type: 'popup', __id: 'p2' },
      { type: 'hero', __id: 'h1', name: 'Hero' },
    ];

    const options = evaluate("toOptions(filterBy(this.page, 'type', 'popup'), ['name', '__id'], '__id')", { page }, { filterBy, toOptions });

    expect(options).toEqual([{ label: 'Promo', value: 'p1' }, { label: 'p2', value: 'p2' }]);
  });

  it('reads only own fields in the option helpers', () => {
    expect(filterBy([{}], 'constructor', Object)).toEqual([]);
    expect(toOptions([{}], 'constructor', 'toString')).toEqual([{ label: undefined, value: undefined }]);
  });

  it('does not hand Object to a helper that walks into it', () => {
    const expression = `getValueByPath([constructor], "$[?(@.getPrototypeOf(@).constructor('globalThis.__safeExpressionEscaped = true')())]")`;

    expect(() => evaluate(expression, {}, { getValueByPath })).toThrow(UnsupportedExpressionError);
    expect((globalThis as any).__safeExpressionEscaped).toBeUndefined();
  });
});
