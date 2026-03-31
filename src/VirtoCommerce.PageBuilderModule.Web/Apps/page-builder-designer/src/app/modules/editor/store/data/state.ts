import { SchemasList } from '@editor/models';
import { TemplateModelsList } from '@models/document';

export interface EditorDataState {
    templates: TemplateModelsList;
    schemas: SchemasList | null;
}

export const initialState: EditorDataState = {
    templates: {},
    schemas: null,
}
