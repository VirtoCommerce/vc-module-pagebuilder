import { ObjectsSchemasList } from '@editor/models';
import { TemplateModel, SectionModel, SectionSchema } from '@models/index';
import { AssetFile } from './integration';

export interface ControlContext {
    block: SectionModel;
    template: TemplateModel;
    page: SectionModel[];
    settings: SectionModel;
    objects: ObjectsSchemasList;
    index: number;
    item: any;
    element?: any; // element in the collection
    parent?: ControlContext
    file?: AssetFile;
    utils: any;

    schema?: SectionSchema;
    sectionSchema?: SectionSchema;
    blockSchema?: SectionSchema;

    // filter: string | null; note! was used to filter by tabs in old version. should not be used in new version
    mode: string;
    __searchQuery: string | null;
}
