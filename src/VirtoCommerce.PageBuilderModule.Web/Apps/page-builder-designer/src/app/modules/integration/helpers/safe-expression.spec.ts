import './known-expressions';
import { evaluate, isSupportedExpression, UnsupportedExpressionError } from './safe-expression';
import { combine } from './utils';

// Every expression the designer's data/settings.json and vc-frontend's section schemas ship today.
const SHIPPED_EXPRESSIONS = [
  "!!this.item && this.item.action === 'url'",
  "!this.model.background",
  "(function() { var s = (this.response.settings || []).find(function(x) { return x.name === 'VirtoCommerce.PageBuilderModule.Store.PreviewUserIds' }); return s && s.value ? s.value.split('\\n').map(function(x) { return x.trim() }).filter(Boolean) : [] }).call(this)",
  "(function(publicUrl, modifiedDate) { if (!publicUrl || !modifiedDate) return publicUrl; return publicUrl + (publicUrl.indexOf('?') >= 0 ? '&' : '?') + 't=' + encodeURIComponent(modifiedDate); })(this.publicAssetUrl, this.modifiedDate)",
  "(function(url) { if (!url) return ''; var trimmed = url.trim(); if (!trimmed) return ''; if (/^(?:[a-z][a-z\\d+\\-.]*:)?\\/\\//i.test(trimmed) || trimmed.indexOf('data:') === 0) return trimmed; var normalized = trimmed.charAt(0) === '/' ? trimmed : '/' + trimmed; return normalized.toLowerCase().indexOf('/assets/') === 0 ? normalized : combine('/assets', normalized); })(this.assetUrl)",
  "(this.response.results || []).map(function(u) { return { id: u.id, name: u.userName || u.email || u.id, photoUrl: u.photoUrl || null } })",
  "JSON.stringify(this.content, null, 2)",
  "JSON.stringify(this.templatesToSave.map(x => ({ path: x.entry.path, groupId: x.entry.groupId, type: x.entry.type, content: x.content })))",
  "Object.keys(this.response).map(x => ({ ...this.item, ...this.response[x], key: x, request: undefined })).reduce((acc, x) => ({ ...acc, [x.key]: x }), {})",
  "Object.keys(this.response).map(x => ({ ...this.item, ...this.response[x], request: undefined, key: x })).reduce((acc, x) => ({ ...acc, [x.key]: x }), {})",
  "combine(this.settings.storefrontUrl, '/designer-preview?pageId=' + (this.groupId || this.location.params.previewId))",
  "combine(this.settings.storefrontUrl, this.settings.previewPath)",
  "encodeURIComponent(this.folderUrl)",
  "this.acceptedTypes",
  "this.assetUrls",
  "this.groupId ? '/api/page-builder-pages/grouped/' + this.groupId + '/content?includeVersion=true' : null",
  "this.model.countLimitation !== false",
  "this.model.countLimitation === true",
  "this.model.hideViewModeSelector !== false",
  "this.response && this.response[0] ? (this.response[0].url || this.response[0].relativeUrl || null) : null",
  "this.response.isAdministrator === true || (this.response.permissions || []).indexOf('builder:shared-components:create') !== -1",
  "this.response.isAdministrator === true || (this.response.permissions || []).indexOf('builder:shared-components:read') !== -1",
  "this.response.isAdministrator === true || (this.response.permissions || []).indexOf('builder:shared-components:update') !== -1",
  "this.response.isAdministrator === true || (this.response.permissions || []).indexOf('platform:asset:create') !== -1",
  "this.response.permissions.indexOf('builder:templates') === -1",
  "this.response.permissions.indexOf('builder:theme') === -1",
  "this.settings.previewImpersonation || []",
  "this.skip",
  "this.take"
];

describe('safe expressions', () => {
  it('supports every expression that settings and schemas ship', () => {
    const unsupported = SHIPPED_EXPRESSIONS.filter(expression => !isSupportedExpression(expression));

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
    );

    expect(result).toEqual({ hero: { type: 'page', title: 'Hero', key: 'hero', request: undefined } });
  });

  it('runs the asset url form like the expression it replaces', () => {
    const expression = "(function(url) { if (!url) return ''; var trimmed = url.trim(); if (!trimmed) return ''; if (/^(?:[a-z][a-z\\d+\\-.]*:)?\\/\\//i.test(trimmed) || trimmed.indexOf('data:') === 0) return trimmed; var normalized = trimmed.charAt(0) === '/' ? trimmed : '/' + trimmed; return normalized.toLowerCase().indexOf('/assets/') === 0 ? normalized : combine('/assets', normalized); })(this.assetUrl)";

    expect(evaluate(expression, { assetUrl: 'images/a.png' }, { combine })).toBe('/assets/images/a.png');
    expect(evaluate(expression, { assetUrl: '/assets/a.png' }, { combine })).toBe('/assets/a.png');
    expect(evaluate(expression, { assetUrl: 'https://cdn/a.png' }, { combine })).toBe('https://cdn/a.png');
    expect(evaluate(expression, { assetUrl: ' ' }, { combine })).toBe('');
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
  ].forEach(expression => {
    it(`does not run ${expression}`, () => {
      expect(() => evaluate(expression, { items: [], a: 0 })).toThrowError(UnsupportedExpressionError);
    });
  });
});
