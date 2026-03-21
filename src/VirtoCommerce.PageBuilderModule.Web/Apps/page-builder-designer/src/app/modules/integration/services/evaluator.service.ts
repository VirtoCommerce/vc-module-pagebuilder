import { Injectable } from '@angular/core';
import { appHelpers } from '../helpers';

@Injectable({
    providedIn: 'root'
})
export class EvaluatorService {
    evaluate(obj: any, context: any): any {
        return this.evaluateObject(obj, context);
    }

    evaluateProperty(config: any, propertyName: string, context: any): any {
        const propertyValue = config[propertyName];
        return this.evaluateObject(propertyValue, context);
    }

    private evaluateObject(value: any, context: any): any {
        if (!value && value !== false && value !== 0 && value !== '') {
            return null;
        }
        if (typeof value === 'string') {
            return appHelpers.template(value, context);
        } else {
            if (Array.isArray(value)) {
                // todo: not tested
                return value.map(v => this.evaluateObject(v, context));
            }
            if (typeof value === 'object') {
                const result: any = {};
                for (const key of Object.keys(value)) {
                    result[key] = this.evaluateObject(value[key], context);
                }
                return result;
            }
            return value;
        }
    }
}
