export interface PreviewOutboundMessage extends Record<string, unknown> {
  type: string;
}

export interface PreviewLoadedMessage {
  source: 'preview';
  type: 'loaded';
  data?: Record<string, unknown>;
}

export interface PreviewSelectMessage {
  source: 'preview';
  type: 'select';
  data: {
    sectionId: string;
  };
}

export interface PreviewHoverMessage {
  source: 'preview';
  type: 'hover';
  data: {
    sectionId: string | null;
  };
}

export type PreviewInboundMessage = PreviewLoadedMessage | PreviewSelectMessage | PreviewHoverMessage;

export function isPreviewOutboundMessage(value: unknown): value is PreviewOutboundMessage {
  return isRecord(value) && typeof value['type'] === 'string' && value['type'].trim().length > 0;
}

export function isPreviewInboundMessage(value: unknown): value is PreviewInboundMessage {
  if (!isRecord(value) || value['source'] !== 'preview') {
    return false;
  }

  switch (value['type']) {
    case 'loaded':
      return value['data'] === undefined || isRecord(value['data']);
    case 'select':
      return hasSectionId(value['data'], false);
    case 'hover':
      return hasSectionId(value['data'], true);
    default:
      return false;
  }
}

function hasSectionId(value: unknown, allowNull: boolean): boolean {
  if (!isRecord(value)) {
    return false;
  }

  const sectionId = value['sectionId'];
  return (allowNull && sectionId === null) || (typeof sectionId === 'string' && sectionId.trim().length > 0);
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}
