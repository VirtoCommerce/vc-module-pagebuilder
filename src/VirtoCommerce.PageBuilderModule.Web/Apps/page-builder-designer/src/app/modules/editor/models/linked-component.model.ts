import { SectionModel, TemplateModel } from '@models/document';

export const LINKED_COMPONENT_REFERENCE_TYPE = 'componentRef' as const;

/**
 * Lightweight value persisted inside page JSON. Linked component content is
 * deliberately not embedded here; it is resolved only for preview/rendering.
 */
export interface LinkedComponentReferenceModel {
    id: string;
    type: typeof LINKED_COMPONENT_REFERENCE_TYPE;
    componentRef: string;
}

/** SectionModel-compatible view used by the existing editor tree. */
export type LinkedComponentReferenceSection = SectionModel & LinkedComponentReferenceModel;

export interface LinkedComponentUsagePage {
    id?: string | null;
    name: string;
    permalink?: string | null;
    cultureName?: string | null;
    status?: string | null;
}

export interface LinkedComponent {
    id: string;
    storeId: string;
    name: string;
    usageCount: number;
    usagePages: LinkedComponentUsagePage[];
    createdDate?: string | null;
    modifiedDate?: string | null;
    createdBy?: string | null;
    modifiedBy?: string | null;
}

export interface LinkedComponentInstanceView {
    reference: LinkedComponentReferenceSection;
    component: LinkedComponent | null;
    error: string | null;
    detailsLoading: boolean;
    detailsError: string | null;
}

export interface LinkedComponentSearchResult {
    totalCount: number;
    results: LinkedComponent[];
}

export interface CreateLinkedComponentRequest {
    storeId: string;
    name: string;
    content: TemplateModel;
}

export type LinkedComponentContentCache = Record<string, TemplateModel>;
export type LinkedComponentMetadataCache = Record<string, LinkedComponent>;
