import { TestBed } from '@angular/core/testing';
import { LiveAnnouncer } from '@angular/cdk/a11y';
import { createBlock, createBlockState, createSection, createSchema } from '@app/testing';
import { ContextMenuHelper } from '@editor/helpers';
import { SectionChildrenListComponent } from './section-children-list.component';

describe('SectionChildrenListComponent keyboard reorder', () => {
  const announce = vi.fn().mockResolvedValue(undefined);

  beforeEach(() => {
    announce.mockClear();
    TestBed.configureTestingModule({
      imports: [SectionChildrenListComponent],
      providers: [
        { provide: ContextMenuHelper, useValue: { getSectionsActions: vi.fn().mockResolvedValue([]) } },
        { provide: LiveAnnouncer, useValue: { announce } },
      ],
    });
  });

  async function renderBlocks(selected: string[] = []) {
    const blocks = ['first', 'second', 'third'].map(id => createBlock({ id }));
    const section = createSection({ blocks });
    const fixture = TestBed.createComponent(SectionChildrenListComponent);
    fixture.componentRef.setInput('section', section);
    fixture.componentRef.setInput('blocksSchemas', { text: createSchema({ type: 'text' }) });
    fixture.componentRef.setInput('states', Object.fromEntries(blocks.map(block =>
      [block.id, createBlockState({ selected: selected.includes(block.id) })])));
    fixture.componentRef.setInput('selectMode', selected.length > 0);
    await fixture.whenStable();
    const reorder = vi.fn();
    fixture.componentInstance.reorderBlocks.subscribe(reorder);
    return { fixture, section, blocks, reorder };
  }

  it.each([
    { id: 'second', index: 1, key: 'ArrowUp', offset: -1, order: [1, 0, 2] },
    { id: 'first', index: 0, key: 'ArrowDown', offset: 1, order: [1, 0, 2] },
  ])('restores the moved nested-block handle after $key', async ({ id, index, key, offset, order }) => {
    const { fixture, section, blocks, reorder } = await renderBlocks();
    const handle = fixture.nativeElement.querySelector('[data-block-id="' + id + '"] button') as HTMLButtonElement;
    handle.focus();
    handle.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }));
    expect(reorder).toHaveBeenCalledWith({
      item: blocks[index], previousIndex: index, currentIndex: index + offset, parent: section,
    });
    fixture.componentRef.setInput('section', createSection({ ...section, blocks: order.map(i => blocks[i]) }));
    await fixture.whenStable();
    expect(document.activeElement).toBe(fixture.nativeElement.querySelector('[data-block-id="' + id + '"] button'));
  });

  it.each([[0, -1], [2, 1]])('does not move past boundary %s with offset %s', async (index, offset) => {
    const { fixture, reorder } = await renderBlocks();
    fixture.componentInstance.moveBlock(index, offset);
    expect(reorder).not.toHaveBeenCalled();
    expect(announce).not.toHaveBeenCalled();
  });

  it('does not reorder in read-only mode', async () => {
    const { fixture, reorder } = await renderBlocks();
    fixture.componentRef.setInput('readOnly', true);
    await fixture.whenStable();
    fixture.componentInstance.moveBlock(1, -1);
    expect(reorder).not.toHaveBeenCalled();
  });

  it('does not reorder an unselected block while a group is selected', async () => {
    const { fixture, reorder } = await renderBlocks(['first', 'third']);
    fixture.componentInstance.moveBlock(1, -1);
    expect(reorder).not.toHaveBeenCalled();
  });

  it('gathers a non-contiguous selected group through the shared reorder helper', async () => {
    const { fixture, blocks, section, reorder } = await renderBlocks(['first', 'third']);
    fixture.componentInstance.moveBlock(0, -1);
    expect(reorder).toHaveBeenCalledWith({ item: blocks[2], previousIndex: 2, currentIndex: 1, parent: section });
  });

  it('announces repeated group moves through the shared LiveAnnouncer', async () => {
    const { fixture } = await renderBlocks(['first', 'second']);
    fixture.componentInstance.moveBlock(0, 1);
    fixture.componentInstance.moveBlock(0, 1);
    expect(announce.mock.calls).toEqual([['Selected blocks moved down'], ['Selected blocks moved down']]);
    expect(fixture.nativeElement.querySelector('[aria-live]')).toBeNull();
  });
});
