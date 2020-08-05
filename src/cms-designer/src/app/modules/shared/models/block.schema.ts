import { ControlDescriptor } from '.';

interface BaseBlockSchema {
    contentType?: string | string[];
    settings: ControlDescriptor[];
}

export interface BlockSchema extends BaseBlockSchema {
    name: string;
    icon: string;
    category?: string;
    type?: string;
    static?: string;
    hide?: boolean;
    displayField?: string;
    excludeShared?: boolean | string[];
    includeShared?: string | string[];
}

export interface SharedBlockSchema extends BaseBlockSchema {
    namedSettings: { [key: string]: ControlDescriptor[]; }
}

interface BlocksSchemaUnderlay {
    [key: string]: BlockSchema;
}

export type BlocksSchema = BlocksSchemaUnderlay & {
    shared?: SharedBlockSchema;
}
