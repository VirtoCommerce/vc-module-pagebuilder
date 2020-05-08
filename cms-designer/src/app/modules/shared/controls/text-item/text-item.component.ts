import { Component, OnInit, Input } from '@angular/core';
import { BaseControlDirective } from '../base-control.component';
import { TextControlDescriptor } from '@shared/models';
import { AppSettings } from '@app/services';

@Component({
    selector: 'app-text-item',
    templateUrl: './text-item.component.html',
    styleUrls: ['./text-item.component.scss']
})
export class TextItemComponent extends BaseControlDirective<TextControlDescriptor> {

    config = {
        toolbar: [
            {
                name: 'basicstyles',
                items: ['Bold', 'Italic', 'Underline', 'Strike']
            },
            { name: 'colors', items: ['TextColor'] },
            { name: 'align', items: ['JustifyLeft', 'JustifyCenter', 'JustifyRight'] },
            { name: 'lists', items: ['NumberedList', 'BulletedList', 'Outdent', 'Indent'] },
            { name: 'insert', items: ['Anchor'] },
            { name: 'link', items: ['Link', 'Unlink'] },
            { name: 'styles', items: ['Format', 'Styles'] },
            { name: 'tools', items: ['Maximize'] },
            { name: 'document', items: ['Source'] }
        ],
        extraPlugins: 'stylescombo,justify,colorbutton,colordialog',
        removeButtons: '',
        format_tags: 'p;h2;h3;h4',
        // contentsCss: AppSettings.contentCssPath,
        stylesSet: [
            { name: 'Normal', element: 'span', attributes: { class: 'section__descr--normal' } },
            { name: 'Medium size text', element: 'span', attributes: { class: 'section__descr--medium' } },
            { name: 'Gray color text', element: 'span', attributes: { class: 'section__descr--gray' } }
        ],
        colorButton_enableMore: true
    };

    constructor() {
        super();
    }

    registerOnChange(fn: any): void {
        this.onChange = (newValue) => {
            if (this.value !== newValue) {
                newValue = newValue.replace(new RegExp('<li>', 'g'), '<li><div>').replace(new RegExp('</li>', 'g'), '</div></li>');
                fn(newValue);
            }
        };
    }

    getConfig(): any {
        if (this.descriptor.settings && this.descriptor.settings.length > 0) {
            let config: any = {};
            config.toolbar = [this.descriptor.settings];
            config.extraPlugins = this.config.extraPlugins;

            if (this.descriptor.settings.indexOf('Format') > -1) {
                config.format_tags = 'normal;medium;large';
                config.format_normal = { name: 'Normal', element: 'div', attributes: { class: 'block__descr--normal' } };
                config.format_medium = { name: 'Medium', element: 'div', attributes: { class: 'block__descr--medium' } };
                config.format_large = { name: 'Large', element: 'div', attributes: { class: 'block__descr--large' } };
            }
            return config;
        }
        return this.config;
    }

    onReady(event) { }
}
