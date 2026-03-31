import { Dictionary } from '@models/index';

export interface GroupStateModel {
    opened: boolean;
}

export type GroupsStateModel = Dictionary<GroupStateModel>;
