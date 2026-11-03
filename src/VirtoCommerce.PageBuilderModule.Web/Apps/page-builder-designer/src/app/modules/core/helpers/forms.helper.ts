import { ObjectsSchemasList } from '@editor/models';
import { UntypedFormArray, UntypedFormControl, UntypedFormGroup } from '@angular/forms';
import { SectionPropertyDescriptor, ObjectDescriptor, ControlDescriptor, CollectionDescriptor } from '@models/controls';

export function generateForm(model: any, properties: SectionPropertyDescriptor[]): UntypedFormGroup {
    const result = new UntypedFormGroup({});
    if (properties) {
        properties.filter(p => !!p.id).forEach(p => {
            const value = model[p.id];
            result.addControl(p.id, new UntypedFormControl(value));
        });
    }
    return result;
}

export function generateFormArray(items: any[], properties: SectionPropertyDescriptor[]): UntypedFormArray {
    return new UntypedFormArray(items.map(item => generateForm(item, properties)));
}

export function mergeDescriptors(objects: ObjectsSchemasList, descriptor: ObjectDescriptor | CollectionDescriptor): ControlDescriptor[] {
    const element = descriptor.element || [];
    const shared = (descriptor.elementDescriptor ? objects?.[descriptor.elementDescriptor]?.settings : []) || [];
    return [...shared.filter(x => !element.find(y => y.id === x.id)), ...element];
}
