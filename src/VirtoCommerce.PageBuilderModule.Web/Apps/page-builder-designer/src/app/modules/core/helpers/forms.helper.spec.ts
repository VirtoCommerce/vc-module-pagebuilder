import { generateForm, generateFormArray, mergeDescriptors } from './forms.helper';
import { SectionPropertyDescriptor } from '@models/controls';

// ── generateForm ───────────────────────────────────────────────────

describe('generateForm', () => {
  it('creates FormGroup with controls matching properties', () => {
    const model = { title: 'Hello', count: 5 };
    const properties: SectionPropertyDescriptor[] = [
      { id: 'title', type: 'string' } as any,
      { id: 'count', type: 'number' } as any,
    ];

    const form = generateForm(model, properties);

    expect(form.get('title')!.value).toBe('Hello');
    expect(form.get('count')!.value).toBe(5);
  });

  it('returns empty FormGroup for null properties', () => {
    const form = generateForm({}, null as any);
    expect(Object.keys(form.controls).length).toBe(0);
  });

  it('skips properties without id', () => {
    const model = { title: 'Hello' };
    const properties = [{ type: 'string' }] as any;

    const form = generateForm(model, properties);

    expect(Object.keys(form.controls).length).toBe(0);
  });

  it('sets null for missing model properties', () => {
    const model = {};
    const properties: SectionPropertyDescriptor[] = [
      { id: 'title', type: 'string' } as any,
    ];

    const form = generateForm(model, properties);

    expect(form.get('title')!.value).toBeNull();
  });
});

// ── generateFormArray ──────────────────────────────────────────────

describe('generateFormArray', () => {
  it('creates FormArray from items', () => {
    const items = [{ name: 'a' }, { name: 'b' }];
    const properties: SectionPropertyDescriptor[] = [
      { id: 'name', type: 'string' } as any,
    ];

    const formArray = generateFormArray(items, properties);

    expect(formArray.length).toBe(2);
    expect(formArray.at(0).get('name')!.value).toBe('a');
    expect(formArray.at(1).get('name')!.value).toBe('b');
  });

  it('returns empty FormArray for empty items', () => {
    const formArray = generateFormArray([], []);
    expect(formArray.length).toBe(0);
  });
});

// ── mergeDescriptors ───────────────────────────────────────────────

describe('mergeDescriptors', () => {
  it('merges shared and element descriptors', () => {
    const objects = {
      sharedObj: { settings: [{ id: 'a', type: 'string' }, { id: 'b', type: 'number' }] },
    } as any;
    const descriptor = {
      elementDescriptor: 'sharedObj',
      element: [{ id: 'b', type: 'text' }, { id: 'c', type: 'checkbox' }],
    } as any;

    const result = mergeDescriptors(objects, descriptor);

    expect(result.length).toBe(3);
    // shared 'a' comes first (not in element)
    expect(result[0].id).toBe('a');
    // element 'b' overrides shared 'b'
    expect(result[1].id).toBe('b');
    expect(result[1].type).toBe('text');
    // element 'c' added
    expect(result[2].id).toBe('c');
  });

  it('returns only element when no elementDescriptor', () => {
    const descriptor = {
      element: [{ id: 'a', type: 'string' }],
    } as any;

    const result = mergeDescriptors({}, descriptor);

    expect(result).toEqual([{ id: 'a', type: 'string' }]);
  });

  it('returns shared settings when no element', () => {
    const objects = {
      obj: { settings: [{ id: 'x', type: 'string' }] },
    } as any;
    const descriptor = { elementDescriptor: 'obj' } as any;

    const result = mergeDescriptors(objects, descriptor);

    expect(result).toEqual([{ id: 'x', type: 'string' }]);
  });
});
