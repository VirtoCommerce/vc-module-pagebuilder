export interface ItemsGroup<T> {
    icon: string;
    name: string;
    items: T[];
    noname: boolean;
    sort?: number;
}
