export interface ContextMenuActionType {
    icon: string;
    title: string;
    action: string;
    selected?: boolean | (() => boolean);
    inactive?: boolean | (() => boolean);
}

export type ContextMenuAction = ContextMenuActionType | '|';
