export interface BaseControlDescriptor {
    id: string; // property name
    type: string; // control type
    label?: string;
    default?: any; // default value when block created
    preview?: any; // value when block under preview

    autofocus?: boolean;
    sort?: number;

    actions?: {
        [key: string]: {
            label?: string;
            icon?: string;
            execute?: string;
        };
    };

    info?: string;
    placeholder?: string;
    hint?: string;

    hidden?: boolean;
    visibility?: string; // java-script for property visibility

    tab?: string;
    group?: string;

    displayPropertyName?: string | string[];
}
