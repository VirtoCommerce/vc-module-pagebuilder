import {
  Component,
  ElementRef,
  DestroyRef,
  inject,
  afterNextRender,
  effect,
  input,
  output,
} from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { MarkdownModel } from './markdown.model';
import EasyMDE from 'easymde';
import TurndownService from 'turndown';
import { marked } from 'marked';

import { MARKDOWN_DATA_SERVICE, IMarkdownDataService } from './ngv-markdown-data.service';

// https://github.com/Ionaru/easy-markdown-editor

@Component({
  selector: 'ngv-markdown',
  imports: [],
  templateUrl: './ngv-markdown.component.html',
  styleUrls: ['./ngv-markdown.component.scss']
})
export class NgvMarkdownComponent {

  private readonly elementRef = inject(ElementRef);
  private readonly http = inject(HttpClient);
  private readonly dataService = inject<IMarkdownDataService>(MARKDOWN_DATA_SERVICE, { optional: true });
  private readonly destroyRef = inject(DestroyRef);

  private easyMDE: EasyMDE | null = null;
  private applyingExternalValue = false;
  private lastEmittedValue: MarkdownModel | null = null;
  private resizeObserver: ResizeObserver | null = null;
  private readonly turndown = new TurndownService({
    headingStyle: 'atx',
    // hr	Any Thematic break	* * *
    // bulletListMarker	-, +, or *	*
    // codeBlockStyle	indented or fenced	indented
    // fence	``` or ~~~	```
    emDelimiter: '*',	// _ or *	_
    // strongDelimiter	** or __	**
    // linkStyle	inlined or referenced	inlined
    // linkReferenceStyle	full, collapsed, or shortcut	full
    // preformattedCode	false or true	false
  });

  readonly styles = input<string[] | string>([]);
  readonly value = input.required<MarkdownModel>();
  readonly options = input<any>(null);
  readonly uploader = input<((file: File) => Observable<{ url: string, name: string }>) | null>(null);

  readonly valueChanged = output<MarkdownModel>();

  constructor() {
    effect(() => this.setValue(this.value()));
    afterNextRender(() => {
      const element = document.createElement('textarea');
      this.elementRef.nativeElement.appendChild(element);
      this.easyMDE = new EasyMDE({
        element,
        status: ["lines", "words"],
        toolbar: [
          'bold',
          'italic',
          'heading',
          '|',
          'quote',
          'unordered-list',
          'ordered-list',
          '|',
          'link',
          'image',
          // '|',
          // 'preview',
          // 'side-by-side',
          // 'fullscreen',
          '|',
          'guide',
          // 'strikethrough',
          // 'code',
          // 'table',
          // 'redo',
          // 'undo',
          // 'heading-bigger',
          // 'heading-smaller',
          // 'heading-1',
          // 'heading-2',
          // 'heading-3',
          // 'clean-block',
          // 'horizontal-rule',
        ],
        spellChecker: false,
        ...this.options() || {}
      });
      this.setValue(this.value());
      this.prepareEditor();
      this.handlePasteValue();
      this.handleChangeValue();
      this.handleResizeElement();
      this.prepareStyles();
    });

    this.destroyRef.onDestroy(() => {
      this.easyMDE?.toTextArea();
      this.easyMDE = null;
      this.resizeObserver?.disconnect();
      this.resizeObserver = null;
    });
  }

  private setValue(value: MarkdownModel): void {
    if (this.easyMDE) {
      if (this.lastEmittedValue && (value?.markdown
        ? value.markdown === this.lastEmittedValue.markdown
        : (value?.html || null) === (this.lastEmittedValue.html || null))) {
        return;
      }
      this.lastEmittedValue = null;
      const mdValue = value?.markdown || this.turndown.turndown(value?.html || '');
      if (this.easyMDE.value() === mdValue) {
        return;
      }
      this.applyingExternalValue = true;
      try {
        this.easyMDE.value(mdValue);
        this.easyMDE.codemirror.clearHistory();
      } finally {
        this.applyingExternalValue = false;
      }
    }
  }

  private handlePasteValue() {
    this.easyMDE?.codemirror.on(<any><unknown>"paste", (_: any, event: ClipboardEvent) => {
      if (event.clipboardData) {
        if (this.tryToPasteHtml(event.clipboardData) || this.tryToPasteImage(event.clipboardData)) {
          event.preventDefault();
        }
      }
    });
  }

  private tryToPasteHtml(clipboard: DataTransfer): boolean {
    const html = clipboard.getData('text/html');
    if (html) {
      const htmlWithLocalImages = this.getImagesFromHtml(html);
      const result = this.turndown.turndown(htmlWithLocalImages || '');
      if (result) {
        this.easyMDE?.codemirror.replaceSelection(result);
        return true;
      }
    }
    return false;
  }

  private tryToPasteImage(clipboard: DataTransfer): boolean {
    for (const image of Array.from(clipboard.items)) {
      if (image && image.type.indexOf('image') === 0) {
        const file = image.getAsFile();
        const uploader = this.getUploader();
        if (!!uploader && file) {
          uploader(file).subscribe(result => {
            this.easyMDE?.codemirror.replaceSelection(`![${result.name}](${result.url})`);
          });
        }
      }
    }
    return false;
  }

  private getImagesFromHtml(html: string): string | null {
    return html;
  }

  private getUploader() {
    return this.dataService ? this.dataService.saveFile.bind(this.dataService) : this.uploader();
  }

  private handleChangeValue() {
    this.easyMDE?.codemirror.on("change", () => {
      if (this.applyingExternalValue) {
        return;
      }
      const markdown: string | null = this.easyMDE?.value() || null;
      const html = markdown ? marked(markdown) as unknown as string : null;
      this.lastEmittedValue = { markdown, html };
      this.valueChanged.emit(this.lastEmittedValue);
    });
  }

  private handleResizeElement() {
    this.resizeObserver = new ResizeObserver(() => {
      this.easyMDE?.codemirror.refresh();
    });
    this.resizeObserver.observe(this.elementRef.nativeElement);
  }

  private _styles: string[] = [];

  private prepareStyles() {
    const stylesValue = this.styles();
    if (!!stylesValue) {
      const items = Array.isArray(stylesValue) ? stylesValue : [stylesValue];
      const result = items.map((item, index) => {
        const currentIndex = index;
        this.http.get(item, { responseType: 'text' }).subscribe(css => {
          this._styles[currentIndex] = css;
        });
        return '';
      });
      this._styles = result;
    } else {
      this._styles = [];
    }
  }

  private prepareEditor() {
    const cm = <any>this.easyMDE?.codemirror;
    if (cm) {
      cm.sideBySideRenderingFunction = () => {
        const wrapper = cm.getWrapperElement();
        const preview = <any>wrapper.nextSibling;
        if (preview && this.easyMDE?.isSideBySideActive()) {
          const value = this.easyMDE?.value();
          const html = marked(value || '') as unknown as string;
          preview.innerHTML = html;
          setTimeout(() => {
            this._styles.forEach(item => {
              const style = document.createElement('style');
              style.attributes.setNamedItem(document.createAttribute('scoped'));
              style.innerHTML = item;
              preview.insertBefore(style, preview.firstChild);
            });
          });
        }
      }
    }
  }
}
