import { ControlDescriptor } from '.';

export interface BlockSchema {
    name: string;
    icon: string;
    category?: string;
    type?: string;
    static?: string;
    hide?: boolean;
    displayField?: string;
    settings: ControlDescriptor[];
}

export interface BlocksSchema {
    [key: string]: BlockSchema;
}
