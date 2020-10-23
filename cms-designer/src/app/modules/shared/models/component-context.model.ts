import { BlockValuesModel } from './block-values.model';

export interface ComponentContext {
    block: BlockValuesModel;
    page: BlockValuesModel[];
    settings: BlockValuesModel;

    filter: string;
    mode: string;
}
