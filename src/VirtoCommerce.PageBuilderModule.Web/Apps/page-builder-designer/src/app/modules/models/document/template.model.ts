import { SectionModel } from './section.model';
import { Dictionary } from '../dictionary.model';

/**
 * @description describe templates, like static page, catalog, product, cart, etc.
 */
export interface TemplateModel {
    version?: number;
    settings: SectionModel;
    content: SectionModel[];
}

export type TemplateModelsList = Dictionary<TemplateModel>;
