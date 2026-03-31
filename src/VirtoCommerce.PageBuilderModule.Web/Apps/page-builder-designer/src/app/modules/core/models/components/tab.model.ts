import { BaseControlDescriptor } from '@models/controls';

export interface TabModel {
    label?: string;
    groups: {
        name: string;
        descriptors: BaseControlDescriptor[];
    }[];
    ungrouped: BaseControlDescriptor[];
}

export interface TabsModel {
    [key: string]: TabModel;
}
