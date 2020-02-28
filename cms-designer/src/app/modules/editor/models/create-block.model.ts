import { BlockSchema } from '@shared/models';

export interface CreateBlockModel {
    items: BlockSchema[];
    groups: { name: string, items: BlockSchema[] }[];
}
