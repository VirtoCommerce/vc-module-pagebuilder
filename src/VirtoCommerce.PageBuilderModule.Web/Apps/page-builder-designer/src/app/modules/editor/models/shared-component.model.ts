import { SectionModel, TemplateModel } from '@models/document';

export const SHARED_COMPONENT_REFERENCE_TYPE = 'componentRef' as const;

/**
 * Lightweight value persisted inside page JSON. Shared component content is
 * deliberately not embedded here; it is resolved only for preview/rendering.
 */
export interface SharedComponentReferenceModel {
    id: string;
    type: typeof SHARED_COMPONENT_REFERENCE_TYPE;
    componentRef: string;
}

/** SectionModel-compatible view used by the existing editor tree. */
export type SharedComponentReferenceSection = SectionModel & SharedComponentReferenceModel;

export interface SharedComponentUsagePage {
    id?: string | null;
    name: string;
    permalink?: string | null;
    cultureName?: string | null;
    status?: string | null;
}

export interface SharedComponent {
    id: string;
    storeId: string;
    name: string;
    usageCount: number;
    usagePages: SharedComponentUsagePage[];
    createdDate?: string | null;
    modifiedDate?: string | null;
    createdBy?: string | null;
    modifiedBy?: string | null;
}

export interface SharedComponentInstanceView {
    reference: SharedComponentReferenceSection;
    component: SharedComponent | null;
    error: string | null;
    detailsLoading: boolean;
    detailsError: string | null;
}

export interface SharedComponentSearchResult {
    totalCount: number;
    results: SharedComponent[];
}

export interface CreateSharedComponentRequest {
    storeId: string;
    name: string;
    content: TemplateModel;
}

export type SharedComponentContentCache = Record<string, TemplateModel>;
export type SharedComponentMetadataCache = Record<string, SharedComponent>;
