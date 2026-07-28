import { createSection, createTemplate } from '@app/testing';
import { createLinkedComponentReference } from '@editor/helpers';

import {
  createInsertionAnchor,
  getSelectedSections,
  hasLinkedPlacement,
  resolveInsertionAnchor,
  sameIds,
  sameSectionRevision,
} from './document-operation.helpers';

describe('Linked Component document operation helpers', () => {
  it('keeps an insertion anchored when unrelated sections are appended', () => {
    const before = createSection({ id: 'before' });
    const after = createSection({ id: 'after' });
    const anchor = createInsertionAnchor(createTemplate({ content: [before, after] }), 1);
    const latest = createTemplate({ content: [before, after, createSection({ id: 'unrelated' })] });

    expect(resolveInsertionAnchor(latest, anchor)).toBe(1);
  });

  it('rejects an insertion when the sections around its anchor are no longer adjacent', () => {
    const before = createSection({ id: 'before' });
    const after = createSection({ id: 'after' });
    const anchor = createInsertionAnchor(createTemplate({ content: [before, after] }), 1);
    const latest = createTemplate({
      content: [before, createSection({ id: 'interleaved' }), after],
    });

    expect(resolveInsertionAnchor(latest, anchor)).toBeNull();
  });

  it('accepts only adjacent independent sections', () => {
    const first = createSection({ id: 'first' });
    const second = createSection({ id: 'second' });
    const reference = createLinkedComponentReference('component-1', 'reference');
    const template = createTemplate({ content: [first, second, reference] });

    expect(getSelectedSections(template, ['first', 'second'])).toEqual({
      sections: [first, second],
      error: null,
    });
    expect(getSelectedSections(template, ['first', 'reference']).error).toContain('independent sections');
  });

  it('compares selection order and submitted section revisions strictly', () => {
    const first = createSection({ id: 'first' });
    const second = createSection({ id: 'second' });

    expect(sameIds(['first', 'second'], ['first', 'second'])).toBe(true);
    expect(sameIds(['first', 'second'], ['second', 'first'])).toBe(false);
    expect(sameSectionRevision([first, second], [first, second])).toBe(true);
    expect(sameSectionRevision([first, second], [{ ...first }, second])).toBe(false);
  });

  it('matches a placement only when both placement and component ids agree', () => {
    const template = createTemplate({
      content: [createLinkedComponentReference('component-1', 'placement-1')],
    });

    expect(hasLinkedPlacement(template, 'placement-1', 'component-1')).toBe(true);
    expect(hasLinkedPlacement(template, 'placement-1', 'component-2')).toBe(false);
  });
});
