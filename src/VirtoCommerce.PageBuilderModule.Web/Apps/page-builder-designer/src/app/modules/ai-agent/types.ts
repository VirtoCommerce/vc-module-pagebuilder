// postMessage contract with Virto OZ.
// Mirrors virto-oz/src/chat/types/schemas.ts — keep in sync when OZ changes.

export interface OzBladeContext {
    id: string;
    name: string;
    title: string;
}

export type OzContextType = 'list' | 'details';

export interface OzInitContextPayload {
    accessToken: string;
    userId: string;
    locale: string;
    blade: OzBladeContext;
    contextType?: OzContextType;
    items?: Record<string, unknown>[];
    suggestions?: unknown[];
}

export type OzUpdateContextPayload = Partial<OzInitContextPayload>;

export interface OzInitContextMessage {
    type: 'INIT_CONTEXT';
    payload: OzInitContextPayload;
}

export interface OzUpdateContextMessage {
    type: 'UPDATE_CONTEXT';
    payload: OzUpdateContextPayload;
}

export type OzParentToChatMessage = OzInitContextMessage | OzUpdateContextMessage;

// Chat -> Parent. MVP only handles CHAT_READY; others are logged.
export type OzChatToParentMessageType =
    | 'CHAT_READY'
    | 'CHAT_ERROR'
    | 'PREVIEW_CHANGES'
    | 'APPLY_CHANGES'
    | 'NAVIGATE_TO_APP'
    | 'RELOAD_BLADE'
    | 'EXPAND_IN_CHAT'
    | 'SHOW_MORE';

export interface OzChatMessage {
    type: OzChatToParentMessageType;
    payload?: unknown;
}
