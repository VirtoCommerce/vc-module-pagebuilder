import {
  spreadPropertyByOther,
  generateAnchor,
  generateUniqueString,
  onlyLettersAndDigits,
  template,
  evalInContext,
  getValueOrDefault,
  getValueByPath,
  stripHtmlTags,
  combine,
  toList,
  tryParseJson,
  getItemValue,
  arrayCastByConfig,
  cutString,
} from './utils';

// ── spreadPropertyByOther ──────────────────────────────────────────

describe('spreadPropertyByOther', () => {
  it('spreads group properties to all items in same group', () => {
    const obj = {
      hero: { group: 'content', groupIcon: 'star', name: 'Hero' },
      banner: { group: 'content', groupIcon: null, name: 'Banner' },
      footer: { group: 'layout', groupIcon: 'grid', name: 'Footer' },
    };

    const result = spreadPropertyByOther(obj, 'group', 'groupIcon');

    expect(result.hero.groupIcon).toBe('star');
    expect(result.banner.groupIcon).toBe('star'); // inherited from hero
    expect(result.footer.groupIcon).toBe('grid');
  });

  it('handles empty object', () => {
    const result = spreadPropertyByOther({}, 'group', 'icon');
    expect(result).toEqual({});
  });
});

// ── generateAnchor ─────────────────────────────────────────────────

describe('generateAnchor', () => {
  it('converts spaces to dashes', () => {
    expect(generateAnchor('Hello World')).toBe('hello-world');
  });

  it('strips special characters', () => {
    expect(generateAnchor('Hello! @World#')).toBe('hello-world');
  });

  it('generates unique string for empty input', () => {
    const result = generateAnchor('');
    expect(result.length).toBe(10);
  });

  it('lowercases input', () => {
    expect(generateAnchor('HELLO')).toBe('hello');
  });

  it('trims leading and trailing dashes', () => {
    expect(generateAnchor('--hello--')).toBe('hello');
  });
});

// ── generateUniqueString ───────────────────────────────────────────

describe('generateUniqueString', () => {
  it('returns string of requested length', () => {
    expect(generateUniqueString(10).length).toBe(10);
    expect(generateUniqueString(4).length).toBe(4);
  });

  it('returns only allowed characters', () => {
    const allowed = /^[0-9A-Za-z_-]+$/;
    expect(generateUniqueString(100)).toMatch(allowed);
  });

  it('returns empty string for length 0', () => {
    expect(generateUniqueString(0)).toBe('');
  });
});

// ── onlyLettersAndDigits ───────────────────────────────────────────

describe('onlyLettersAndDigits', () => {
  it('strips non-alphanumeric characters', () => {
    expect(onlyLettersAndDigits('hello-world_123!')).toBe('helloworld123');
  });

  it('returns empty string for all special characters', () => {
    expect(onlyLettersAndDigits('!@#$%')).toBe('');
  });

  it('returns falsy value as-is', () => {
    expect(onlyLettersAndDigits('')).toBe('');
  });
});

// ── template ───────────────────────────────────────────────────────

describe('template', () => {
  it('substitutes {{key}} tokens', () => {
    expect(template('Hello {{name}}!', { name: 'World' })).toBe('Hello World!');
  });

  it('replaces missing keys with empty string', () => {
    expect(template('Hello {{name}}!', {})).toBe('Hello !');
  });

  it('evaluates {{=expression}} tokens', () => {
    expect(template('Result: {{=1+2}}', {})).toBe('Result: 3');
  });

  it('handles nested paths', () => {
    expect(template('{{a.b}}', { a: { b: 'deep' } })).toBe('deep');
  });

  it('handles triple braces (regex captures {name)', () => {
    // {{{name}}} — regex matches {{{name}} capturing '{name', which is not a valid path
    expect(template('{{{name}}}', { name: 'World' })).toBe('}');
  });

  it('handles null/undefined values as empty string', () => {
    expect(template('{{val}}', { val: null })).toBe('');
    expect(template('{{val}}', { val: undefined })).toBe('');
  });
});

// ── evalInContext ───────────────────────────────────────────────────

describe('evalInContext', () => {
  it('evaluates simple expression', () => {
    expect(evalInContext('1 + 2', {})).toBe(3);
  });

  it('accesses context via this', () => {
    expect(evalInContext('this.x * 2', { x: 5 })).toBe(10);
  });

  it('has access to utility functions', () => {
    expect(evalInContext('combine("a", "b")', {})).toBe('a/b');
  });

  it('returns null on error', () => {
    expect(evalInContext('undeclaredVar.prop', {})).toBeNull();
  });
});

// ── getValueOrDefault ──────────────────────────────────────────────

describe('getValueOrDefault', () => {
  it('returns 0 as-is (not default)', () => {
    expect(getValueOrDefault(0, 'fallback')).toBe(0);
  });

  it('returns false as-is', () => {
    expect(getValueOrDefault(false, 'fallback')).toBe(false);
  });

  it('returns empty string as-is', () => {
    expect(getValueOrDefault('', 'fallback')).toBe('');
  });

  it('returns null as-is', () => {
    expect(getValueOrDefault(null, 'fallback')).toBeNull();
  });

  it('returns default for undefined', () => {
    expect(getValueOrDefault(undefined, 'fallback')).toBe('fallback');
  });

  it('returns truthy value', () => {
    expect(getValueOrDefault('hello', 'fallback')).toBe('hello');
  });

  it('defaults to null when no default given', () => {
    expect(getValueOrDefault(undefined)).toBeNull();
  });
});

// ── getValueByPath ─────────────────────────────────────────────────

describe('getValueByPath', () => {
  it('resolves simple path', () => {
    expect(getValueByPath({ name: 'test' }, 'name')).toBe('test');
  });

  it('resolves nested path', () => {
    expect(getValueByPath({ a: { b: { c: 42 } } }, 'a.b.c')).toBe(42);
  });

  it('returns null for function values', () => {
    expect(getValueByPath({ fn: () => {} }, 'fn')).toBeNull();
  });

  it('returns undefined for missing path', () => {
    expect(getValueByPath({ a: 1 }, 'b')).toBeUndefined();
  });
});

// ── stripHtmlTags ──────────────────────────────────────────────────

describe('stripHtmlTags', () => {
  it('strips tags and preserves text', () => {
    expect(stripHtmlTags('<b>Bold</b> text')).toBe(' Bold  text');
  });

  it('returns plain text unchanged', () => {
    expect(stripHtmlTags('no tags')).toBe('no tags');
  });

  it('handles self-closing tags', () => {
    expect(stripHtmlTags('line<br/>break')).toBe('line break');
  });
});

// ── combine ────────────────────────────────────────────────────────

describe('combine', () => {
  it('joins parts with slash', () => {
    expect(combine('a', 'b', 'c')).toBe('a/b/c');
  });

  it('does not double slashes', () => {
    expect(combine('a/', '/b')).toBe('a/b');
  });

  it('handles trailing slash + no leading slash', () => {
    expect(combine('a/', 'b')).toBe('a/b');
  });

  it('handles no trailing slash + leading slash', () => {
    expect(combine('a', '/b')).toBe('a/b');
  });

  it('skips empty parts', () => {
    expect(combine('a', '', 'b')).toBe('a/b');
  });

  it('returns empty for no parts', () => {
    expect(combine()).toBe('');
  });
});

// ── toList ─────────────────────────────────────────────────────────

describe('toList', () => {
  it('converts object to array with key property', () => {
    const obj = { a: { val: 1 }, b: { val: 2 } };

    const result = toList(obj, 'key');

    expect(result).toEqual([
      { key: 'a', val: 1 },
      { key: 'b', val: 2 },
    ]);
  });
});

// ── tryParseJson ───────────────────────────────────────────────────

describe('tryParseJson', () => {
  it('parses valid JSON', () => {
    expect(tryParseJson('{"a":1}')).toEqual({ a: 1 });
  });

  it('returns null for invalid JSON', () => {
    expect(tryParseJson('not json')).toBeNull();
  });
});

// ── getItemValue ───────────────────────────────────────────────────

describe('getItemValue', () => {
  it('extracts values by string descriptors', () => {
    const item = { name: 'Test', count: 5 };

    const result = getItemValue(item, ['name', 'count']);

    expect(result).toEqual({ name: 'Test', count: 5 });
  });

  it('extracts values by object descriptors', () => {
    const item = { data: { title: 'Hello' } };

    const result = getItemValue(item, [{ query: 'data.title', key: 'name', isArray: false }]);

    expect(result).toEqual({ name: 'Hello' });
  });

  it('applies isArray casting', () => {
    const item = { tags: 'single' };

    const result = getItemValue(item, [{ query: 'tags', key: 'tags', isArray: true }]);

    expect(result.tags).toEqual(['single']);
  });
});

// ── arrayCastByConfig ──────────────────────────────────────────────

describe('arrayCastByConfig', () => {
  it('returns item as-is when isArray is null', () => {
    expect(arrayCastByConfig('hello', null)).toBe('hello');
  });

  it('unwraps array when isArray is false', () => {
    expect(arrayCastByConfig(['first', 'second'], false)).toBe('first');
  });

  it('returns null for empty array when isArray is false', () => {
    expect(arrayCastByConfig([], false)).toBeNull();
  });

  it('wraps non-array when isArray is true', () => {
    expect(arrayCastByConfig('hello', true)).toEqual(['hello']);
  });

  it('returns array as-is when isArray is true and already array', () => {
    expect(arrayCastByConfig([1, 2], true)).toEqual([1, 2]);
  });
});

// ── cutString ──────────────────────────────────────────────────────

describe('cutString', () => {
  it('returns short string unchanged', () => {
    expect(cutString('hello', 50)).toBe('hello');
  });

  it('truncates long string with ellipsis', () => {
    expect(cutString('a'.repeat(60), 50)).toBe('a'.repeat(50) + '...');
  });

  it('returns empty string for falsy input', () => {
    expect(cutString('')).toBe('');
    expect(cutString(null as any)).toBe('');
  });

  it('uses default limit of 50', () => {
    expect(cutString('a'.repeat(51))).toBe('a'.repeat(50) + '...');
  });
});
