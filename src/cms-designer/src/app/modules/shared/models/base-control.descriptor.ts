import { BaseDescriptor } from '.';

export interface BaseControlDescriptor extends BaseDescriptor {
    info?: string; // question: may info be html? now it can be markdown
    default?: string | boolean | number | Date;
}
