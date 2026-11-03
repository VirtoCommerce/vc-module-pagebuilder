import { inject } from '@angular/core';

import { ControlsFactory } from './controls.factory';
import { CheckboxComponent } from './checkbox/checkbox.component';
import { CollectionComponent } from './collection/collection.component';
import { NumberComponent } from './number/number.component';
import { ObjectComponent } from './object/object.component';
import { SearchComponent } from './search/search.component';
import { SelectComponent } from './select/select.component';
import { StringComponent } from './string/string.component';

export function registerControls(): () => void {
    const factory = inject(ControlsFactory);
    return () => {
        // Light controls — loaded eagerly
        factory.register('checkbox', CheckboxComponent);
        factory.register('list', CollectionComponent);
        factory.register('number', NumberComponent);
        factory.register('object', ObjectComponent);
        factory.register('slider', NumberComponent);
        factory.register('select', SelectComponent);
        factory.register('string', StringComponent);
        factory.register('search', SearchComponent);

        // Heavy controls — loaded lazily on first use
        factory.registerLazy('text', () =>
            import('./text/text.component').then(m => m.TextComponent));
        factory.registerLazy('calendar', () =>
            import('./calendar/calendar.component').then(m => m.CalendarComponent));
        factory.registerLazy('color', () =>
            import('./color/color.component').then(m => m.ColorComponent));
        factory.registerLazy('markdown', () =>
            import('./markdown/markdown.component').then(m => m.MarkdownComponent));
        factory.registerLazy('files', () =>
            import('./files/files.component').then(m => m.FilesComponent));
        factory.registerLazy('images', () =>
            import('./images/images.component').then(m => m.ImagesComponent));
    };
}
