import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { AssetsService } from '@core/services';
import { MarkdownComponent } from './markdown.component';

describe('MarkdownComponent HTML form feedback', () => {
    const rangeMethods = ['getBoundingClientRect', 'getClientRects'] as const;
    const originalRangeDescriptors = rangeMethods.map(method => Object.getOwnPropertyDescriptor(Range.prototype, method));

    afterEach(() => {
        TestBed.resetTestingModule();
        rangeMethods.forEach((method, index) => {
            const descriptor = originalRangeDescriptors[index];
            if (descriptor) {
                Object.defineProperty(Range.prototype, method, descriptor);
            } else {
                Reflect.deleteProperty(Range.prototype, method);
            }
        });
        vi.restoreAllMocks();
        vi.unstubAllGlobals();
    });

    it('retains Enter, blank lines and undo when an equal HTML string is not written back by the form', async () => {
        vi.stubGlobal('ResizeObserver', class { observe() {} disconnect() {} });
        // JSDOM has no layout, but the real CodeMirror editor measures ranges.
        Object.defineProperty(Range.prototype, 'getBoundingClientRect', { configurable: true, value: () => new DOMRect() });
        Object.defineProperty(Range.prototype, 'getClientRects', { configurable: true, value: () => [] });
        TestBed.configureTestingModule({ imports: [MarkdownComponent], providers: [
            provideHttpClient(), { provide: AssetsService, useValue: {} },
        ] });
        const fixture = TestBed.createComponent(MarkdownComponent);
        fixture.componentRef.setInput('descriptor', { resultType: 'html' });
        let formValue = '';
        fixture.componentRef.setInput('controlValue', formValue);
        fixture.componentInstance.valueChanged.subscribe(value => {
            // ControlHolder only writes a changed scalar back into the input.
            if (formValue !== value) {
                formValue = value;
                fixture.componentRef.setInput('controlValue', value);
            }
        });
        await fixture.whenStable();
        const editor = fixture.nativeElement.querySelector('.CodeMirror').CodeMirror;
        let expected = '';
        for (const inserted of ['Hello', '\n', '\n', 'world']) {
            expected += inserted;
            editor.replaceRange(inserted, { line: editor.lastLine(), ch: editor.getLine(editor.lastLine()).length }, undefined, '+input');
            await fixture.whenStable();
            expect(editor.getValue()).toBe(expected);
            expect(editor.historySize().undo).toBeGreaterThan(0);
        }
        expect(editor.getValue()).toBe('Hello\n\nworld');
        editor.replaceRange('\n', { line: 1, ch: 0 }, undefined, '+input');
        await fixture.whenStable();
        expect(editor.getValue()).toBe('Hello\n\n\nworld');
        expect(formValue).toBe('<p>Hello</p>\n<p>world</p>\n');
        editor.undo();
        await fixture.whenStable();
        editor.redo();
        await fixture.whenStable();
        expect(editor.getValue()).toBe('Hello\n\n\nworld');
        fixture.componentRef.setInput('controlValue', '<p>Server update</p>');
        await fixture.whenStable();
        expect(editor.getValue()).toBe('Server update');
    });
});
