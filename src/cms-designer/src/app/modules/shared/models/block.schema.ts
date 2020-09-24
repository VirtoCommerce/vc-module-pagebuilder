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
    excludeShared?: boolean | string[]; // false - not use shared settings, string[] - list of settings id to exclude from result shared list
    includeShared?: string | string[]; // name or list of names to add settings from namedSettingsList
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
