import { HttpErrorResponse } from '@angular/common/http';
import { TemplateEntryList } from './template-entry-list.model';
import { TemplateEntryStateList } from './template-entry-state.model';

export interface TemplateSelectorState {
    // filter: string | null; // todo: maybe filter should be unique for each templates list
    isLoading: boolean;
    templates: TemplateEntryList;
    states: TemplateEntryStateList;
    error: HttpErrorResponse | null;
}
