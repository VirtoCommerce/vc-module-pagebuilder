import { BaseHandler } from "./base.handler";
import { BaseMessage } from "../models";
import { BlockViewModel } from "../block.view-model";
import { ServiceLocator } from '../service-locator';

export class RequestSettingsHandler extends BaseHandler {
    readonly key = 'settings';

    execute(msg: BaseMessage, list: BlockViewModel[]) {
        ServiceLocator.getHttp().get('/themes/settings.json').then(response => {
            const settings = JSON.parse(response);
            ServiceLocator.getMessages().settings(settings);
        }).catch(error => {
            console.log(error);
            ServiceLocator.getMessages().settings({});
        });
    }
}