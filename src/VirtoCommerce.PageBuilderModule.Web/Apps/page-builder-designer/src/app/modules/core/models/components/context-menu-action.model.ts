export interface ContextMenuActionType {
    icon: string;
    title: string;
    action: string;
    imageUrl?: string | null;
    selected?: boolean | (() => boolean);
    inactive?: boolean | (() => boolean);
}

export type ContextMenuAction = ContextMenuActionType | '|';
