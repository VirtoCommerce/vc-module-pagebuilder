import { groupSections, createDefaultObject, parseIntOrDefault, NO_NAME_GROUP_KEY } from './utils';
import { SectionSchema } from '@models/index';

// ── groupSections ──────────────────────────────────────────────────

describe('groupSections', () => {
  it('groups sections by group property', () => {
    const list: SectionSchema[] = [
      { type: 'hero', name: 'Hero', group: 'content', groupIcon: 'star' } as any,
      { type: 'banner', name: 'Banner', group: 'content' } as any,
      { type: 'footer', name: 'Footer', group: 'layout', groupIcon: 'grid' } as any,
    ];

    const result = groupSections(list);

    expect(result.length).toBe(2);
    const content = result.find(g => g.name === 'content')!;
    expect(content.items.length).toBe(2);
    expect(content.icon).toBe('star');
    expect(content.noname).toBe(false);
  });

  it('marks unnamed group with noname: true', () => {
    const list: SectionSchema[] = [
      { type: 'hero', name: 'Hero' } as any,
    ];

    const result = groupSections(list);

    expect(result.length).toBe(1);
    expect(result[0].noname).toBe(true);
    expect(result[0].name).toBeUndefined();
  });

  it('picks first available groupIcon in group', () => {
    const list: SectionSchema[] = [
      { type: 'a', name: 'A', group: 'g' } as any,
      { type: 'b', name: 'B', group: 'g', groupIcon: 'icon-b' } as any,
    ];

    const result = groupSections(list);

    expect(result[0].icon).toBe('icon-b');
  });

  it('returns empty array for null/empty list', () => {
    expect(groupSections(null as any)).toEqual([]);
    expect(groupSections([])).toEqual([]);
  });

  it('preserves groupSort', () => {
    const list: SectionSchema[] = [
      { type: 'hero', name: 'Hero', group: 'content', groupSort: 2 } as any,
    ];

    const result = groupSections(list);

    expect(result[0].sort).toBe(2);
  });
});

// ── createDefaultObject ────────────────────────────────────────────

describe('createDefaultObject', () => {
  it('picks default values from descriptors', () => {
    const settings = [
      { id: 'title', default: 'Hello' },
      { id: 'count', default: 0 },
      { id: 'noDefault' },
    ] as any;

    const result = createDefaultObject(settings);

    expect(result).toEqual({ title: 'Hello', count: 0 });
  });

  it('returns empty object for null settings', () => {
    expect(createDefaultObject(null as any)).toEqual({});
  });

  it('returns empty object when no descriptors have default', () => {
    const settings = [{ id: 'a' }, { id: 'b' }] as any;

    expect(createDefaultObject(settings)).toEqual({});
  });
});

// ── parseIntOrDefault ──────────────────────────────────────────────

describe('parseIntOrDefault', () => {
  it('parses valid integer string', () => {
    expect(parseIntOrDefault('42', 0)).toBe(42);
  });

  it('returns default for null', () => {
    expect(parseIntOrDefault(null, 10)).toBe(10);
  });

  it('returns default for undefined', () => {
    expect(parseIntOrDefault(undefined, 10)).toBe(10);
  });

  it('returns default for NaN result', () => {
    expect(parseIntOrDefault('abc', 5)).toBe(5);
  });

  it('returns default for empty string', () => {
    expect(parseIntOrDefault('', 7)).toBe(7);
  });

  it('parses integer from number', () => {
    expect(parseIntOrDefault(42, 0)).toBe(42);
  });
});
