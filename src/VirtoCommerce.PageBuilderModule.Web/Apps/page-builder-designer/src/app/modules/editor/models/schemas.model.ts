import { SectionsSchemasList, ObjectsSchemasList } from '@editor/models';

/**
 * @description describe all schemas
 */
export type SchemasList = {
    blocks: SectionsSchemasList;
    sections: SectionsSchemasList;
    objects: ObjectsSchemasList;
    shared: ObjectsSchemasList;
};
