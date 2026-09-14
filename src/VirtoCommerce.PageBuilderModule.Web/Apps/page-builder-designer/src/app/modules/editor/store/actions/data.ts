import { HttpErrorResponse } from '@angular/common/http';
import { createAction, props } from "@ngrx/store";
import { TemplateEntry, TemplateEntryInfo } from '@shared/models';
import { SchemasList } from '@editor/models';
import { TemplateModel } from '@models/document';

export const raiseLoadData = createAction('[template editor] raise load data');

export const loadTemplateModel = createAction('[template editor] load template model', props<{ templateKey: string }>());
export const loadTemplateModelSuccess = createAction('[template editor] load template model success', props<{ template: TemplateModel, templateKey: string }>());
// the builder http client turns some failures into an empty result, so the reason is not always an http response
export const loadTemplateModelFails = createAction('[template editor] load template model fails', props<{ error: HttpErrorResponse | Error, templateKey: string }>());

export const reloadTemplateModel = createAction('[template editor] reload template model', props<{ templateKey: string }>());
export const reloadTemplateModelSuccess = createAction('[template editor] reload template model success', props<{ template: TemplateModel, templateKey: string }>());
export const reloadTemplateModelFails = createAction('[template editor] reload template model fails', props<{ error: HttpErrorResponse, templateKey: string }>());

export const getTemplatePublishStatus = createAction('[template editor] get template publish status', props<{ templateKey: string }>());
export const getTemplatePublishStatusSuccess = createAction('[template editor] get template publish status success', props<{ templateKey: string, hasChanges: boolean, published: boolean }>());
export const getTemplatePublishStatusFails = createAction('[template editor] get template publish status fails', props<{ error: HttpErrorResponse, templateKey: string }>());

export const loadTemplateSchemas = createAction('[template editor] load template schemas');
export const loadTemplateSchemasSuccess = createAction('[template editor] load template schemas success', props<{ schemas: SchemasList | null }>());
export const loadTemplateSchemasFails = createAction('[template editor] load template schemas fails', props<{ error: HttpErrorResponse | Error }>());

export const useSchemasAction = createAction('[template editor] merge schemas', props<{ schemas: SchemasList }>());

export const updateTemplateAction = createAction('[template editor] update template', props<{ template: TemplateModel, templateKey: string }>());

export const saveTemplates = createAction('[template editor] save templates', props<{ templates: { entry: TemplateEntry , content: TemplateModel, info: TemplateEntryInfo }[] }>());
export const saveTemplateSuccess = createAction('[template editor] save template success', props<{ templateKey: string, parentKey?: string, template: TemplateModel }>());
export const saveTemplateFails = createAction('[template editor] save template fails', props<{ error: HttpErrorResponse }>());

export const validateItemUnderEdit = createAction('[template editor] validate item under edit');
