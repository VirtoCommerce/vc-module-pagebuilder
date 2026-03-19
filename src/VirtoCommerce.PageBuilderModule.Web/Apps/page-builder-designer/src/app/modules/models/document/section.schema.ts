import { SectionPropertyDescriptor } from "../controls";
import { SectionModel } from "./section.model";

export interface SectionSchema {
    icon: string;
    type: string;
    name: string;
    static?: boolean | string;
    displayField?: string;
    sort?: number;
    blocks?: string[];
    inline?: boolean; // used for settings groups, when false, group displayed as a overlap panel
    settings: SectionPropertyDescriptor[];
    default?: SectionModel;
    preview?: SectionModel;

    targetTemplates?: string[]; // list of template names where this section can be used

    group?: string;
    groupIcon?: string;
    groupSort?: number;

    includeShared?: string[]; // list of names to add settings from Shared
    excludeShared?: string[] | true; // true - not use shared settings, string[] - list of settings id to exclude from result shared list
}
