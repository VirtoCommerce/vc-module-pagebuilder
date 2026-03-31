import { Dictionary } from '@models/index';

// template ui state
export interface TemplateState {
    id: string;
    isLoading: boolean;
    hasChanges?: boolean;
    published?: boolean;
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
