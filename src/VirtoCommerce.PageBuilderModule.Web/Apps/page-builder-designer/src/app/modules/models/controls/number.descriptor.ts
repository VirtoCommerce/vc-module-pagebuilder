import { BaseControlDescriptor } from "./base-control.descriptor";

export interface NumberDescriptor extends BaseControlDescriptor {
    min?: number;
    max?: number;
    step?: number | string;
    thumb?: boolean;
}
