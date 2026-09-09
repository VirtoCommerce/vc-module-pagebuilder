import { PageHistoryState } from './page-history.model';
import { Dictionary } from '@models/index';

/** Where a page stands on the production branch. Null where the installation has none. */
export interface ProductionStatus {
    /** The page exists on the production branch. */
    published: boolean;
    /** Production holds something other than what the base branch does. */
    behind: boolean;
    /** A promotion pull request for the page is open and has not merged yet. */
    pending: boolean;
}

// template ui state
export interface TemplateState {
    id: string;
    isLoading: boolean;
    hasChanges?: boolean;
    published?: boolean;
    /**
     * The page is on its way to production but not there yet: a pull request for it is open, waiting
     * on its checks. Publishing again would achieve nothing.
     */
    pending?: boolean;
    /**
     * Where the page stands on production, or null/undefined where the installation has no
     * production branch — in which case promotion is not an action that exists here.
     */
    production?: ProductionStatus | null;
    /** Versions of this page, once the panel has asked for them. */
    history?: PageHistoryState;
    error?: string;
    sections: SectionStatesList;
}

export interface SectionState {
    expanded: boolean;
    canHaveChildren?: boolean;
    selected: boolean;
    selectable: boolean;
    isDragging: boolean;
    blocks: BlockStatesList;
}

export interface BlockState {
    selected: boolean;
    selectable: boolean;
    isDragging: boolean;
}

export type TemplateStatesList = Dictionary<TemplateState>;
export type SectionStatesList = Dictionary<SectionState>;
export type BlockStatesList = Dictionary<BlockState>;
