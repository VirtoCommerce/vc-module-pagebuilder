import { Component, OnInit, Input, Output, EventEmitter } from '@angular/core';
import { FormGroup, FormBuilder, Validators } from '@angular/forms';
import { PresetsModel } from '@themes/models';
import { AppSettings } from '@app/services';

@Component({
    selector: 'app-presets-editor',
    templateUrl: './presets-editor.component.html',
    styleUrls: ['./presets-editor.component.scss']
})
export class PresetsEditorComponent implements OnInit {

    @Input() data: PresetsModel;
    @Input() selected: string;
    @Output() removePresetEvent = new EventEmitter<string>();
    @Output() savePresetEvent = new EventEmitter<string>();
    @Output() selectPresetEvent = new EventEmitter<string>();
    @Output() applyThemeEvent = new EventEmitter();
    @Output() cancelPresetEvent = new EventEmitter();

    form: FormGroup;
    savingPreset = false;
    newPresetName = 'Preset name';

    constructor(private fb: FormBuilder) { }

    ngOnInit() {
        this.form = this.fb.group({
            name: ['', Validators.required]
        });
    }

    getThemeThumb(key: string): string {
        // /en-US/themes/assets/thumb_dark.png
        const result = <string>this.data.presets[key].previewImage;
        if (!!result) {
            return result;
        }
        return `url(${AppSettings.storeBaseUrl}themes/assets/thumb_${key.replace(' ', '_')}.jpg)`;
    }

    selectPreset(name: string) {
        this.selectPresetEvent.emit(name);
    }

    savePreset() {
        const name = this.form.get('name').value;
        this.savePresetEvent.emit(name);
        this.savingPreset = false;
    }

    removePreset(name: string) {
        this.removePresetEvent.emit(name);
    }

    applyPreset() {
        this.applyThemeEvent.emit();
    }

    cancel() {
        this.cancelPresetEvent.emit();
    }
}
