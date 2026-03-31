import { ChangeDetectionStrategy, Component } from '@angular/core';
import { CKEditor4, CKEditorModule } from 'ckeditor4-angular';
import { BaseControlDirective } from '@core/controls/base-control.directive';
import { TextDescriptor } from '@models/controls';

@Component({
  selector: 'app-text',
  templateUrl: './text.component.html',
  styleUrls: ['./text.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CKEditorModule]
})
export class TextComponent extends BaseControlDirective<TextDescriptor> {

  // CKEditor4 uses ambient const enums which are incompatible with isolatedModules.
  // Double cast is required because the enum type is nominal — string literals aren't
  // directly assignable even when the values match.
  editorType = 'classic' as unknown as CKEditor4.EditorType;

  private defaultConfig = {
    defaultLanguage: 'en',
    language: 'en',
    toolbar: [
      { name: 'basicstyles', items: ['Bold', 'Italic', 'Underline', 'Strike'] },
      { name: 'colors', items: ['TextColor'] },
      { name: 'align', items: ['JustifyLeft', 'JustifyCenter', 'JustifyRight'] },
      { name: 'lists', items: ['NumberedList', 'BulletedList', 'Outdent', 'Indent'] },
      { name: 'insert', items: ['Anchor'] },
      { name: 'link', items: ['Link', 'Unlink'] },
      { name: 'styles', items: ['Format', 'Styles'] },
      { name: 'tools', items: ['Maximize'] },
      { name: 'document', items: ['Source'] }
    ],
    extraPlugins: 'stylescombo,justify,colorbutton,colordialog,font',
    removeButtons: '',
    format_tags: 'p;h2;h3;h4',
    stylesSet: [
      { name: 'Normal', element: 'span', attributes: { class: 'section__descr--normal' } },
      { name: 'Medium size text', element: 'span', attributes: { class: 'section__descr--medium' } },
      { name: 'Gray color text', element: 'span', attributes: { class: 'section__descr--gray' } }
    ],
    colorButton_enableMore: true,
    autoParagraph: false,
    allowedContent: true
  };

  config = this.defaultConfig;

  override onValueChanged = (newValue: any) => {
    if (this.controlValue() !== newValue) {
      this.defaultValueChanged(newValue);
    }
  };

  protected override descriptorChanged(): void {
    this.config = { ...this.defaultConfig, ...this.descriptor?.config };
  }
}
