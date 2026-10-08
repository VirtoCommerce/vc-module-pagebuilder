import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { NgvMarkdownComponent } from './ngv-markdown.component';

const editor = vi.hoisted(() => ({
    value: '',
    change: undefined as (() => void) | undefined,
    setValue: vi.fn(),
    clearHistory: vi.fn(),
}));

vi.mock('easymde', () => ({ default: class {
    codemirror = {
        on: (event: string, callback: () => void) => { if (event === 'change') editor.change = callback; },
        clearHistory: editor.clearHistory,
        refresh: vi.fn(),
    };
    value(value?: string) {
        if (value === undefined) return editor.value;
        editor.value = value;
        editor.setValue(value);
        editor.change?.();
        return value;
    }
    toTextArea() {}
} }));

describe('NgvMarkdownComponent document refresh', () => {
    let fixture: ComponentFixture<NgvMarkdownComponent>;

    beforeEach(async () => {
        editor.value = '';
        editor.change = undefined;
        vi.clearAllMocks();
        vi.stubGlobal('ResizeObserver', class { observe() {} disconnect() {} });
        TestBed.configureTestingModule({ imports: [NgvMarkdownComponent], providers: [provideHttpClient()] });
        fixture = TestBed.createComponent(NgvMarkdownComponent);
        fixture.componentRef.setInput('value', { markdown: 'Original document', html: null });
        await fixture.whenStable();
    });

    afterEach(() => { TestBed.resetTestingModule(); vi.unstubAllGlobals(); });

    it('updates a mounted editor after a document reload without emitting a local edit', async () => {
        const changed = vi.fn();
        fixture.componentInstance.valueChanged.subscribe(changed);
        fixture.componentRef.setInput('value', { markdown: 'Assistant document', html: null });
        await fixture.whenStable();
        expect(editor.value).toBe('Assistant document');
        expect(changed).not.toHaveBeenCalled();
        expect(editor.clearHistory).toHaveBeenCalledTimes(2);
    });

    it('retains the user edit and undo history when the same value flows back from state', async () => {
        const changed = vi.fn();
        fixture.componentInstance.valueChanged.subscribe(changed);
        editor.value = 'Local edit';
        editor.change?.();
        expect(changed).toHaveBeenCalledWith({ markdown: 'Local edit', html: '<p>Local edit</p>\n' });
        fixture.componentRef.setInput('value', { markdown: 'Local edit', html: null });
        await fixture.whenStable();
        expect(editor.setValue).toHaveBeenCalledTimes(1);
        expect(editor.clearHistory).toHaveBeenCalledTimes(1);
    });

    it.each([' ', '\n', 'Hello ', '#', '-', '*', '1.', 'Hello\n-', 'a_'])('retains literal typing and undo when HTML echoes back: %s', async markdown => {
        fixture.componentInstance.valueChanged.subscribe(value => {
            fixture.componentRef.setInput('value', { markdown: '', html: value.html });
        });
        editor.value = markdown;
        editor.change?.();
        await fixture.whenStable();
        expect(editor.value).toBe(markdown);
        expect(editor.setValue).toHaveBeenCalledTimes(1);
        expect(editor.clearHistory).toHaveBeenCalledTimes(1);

        fixture.componentRef.setInput('value', { markdown: '', html: '<p>External update</p>' });
        await fixture.whenStable();
        expect(editor.value).toBe('External update');
        expect(editor.clearHistory).toHaveBeenCalledTimes(2);
    });
});
