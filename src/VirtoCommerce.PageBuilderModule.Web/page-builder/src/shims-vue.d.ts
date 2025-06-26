/* eslint-disable */
import {CoreBladeAdditionalSettings} from "@vc-shell/framework";
import {Ref, Plugin} from "vue";
import {DynamicGridSchema, DynamicDetailsSchema} from "@vc-shell/framework";
import * as vue from "vue";
import * as vueRouter from "vue-router";
import * as veeValidate from "vee-validate";
import * as vueI18n from "vue-i18n";
import moment from "moment";
import type {Component} from "vue";

declare module "*.vue" {
  import type {DefineComponent} from "vue";
  const component: DefineComponent<{}, {}, any>;
  export default component;
}

declare module "@vue/runtime-core" {
  interface ComponentCustomProperties extends _ComponentCustomProperties {
    $mergeLocaleMessage: Composer<{}, {}, {}, string, never, string>["mergeLocaleMessage"];
    $hasAccess: (permissions: string | string[] | undefined) => boolean;
    $isPhone: Ref<boolean>;
    $isTablet: Ref<boolean>;
    $isMobile: Ref<boolean>;
    $isDesktop: Ref<boolean>;
    $isTouch: boolean;
    $t: (key: string, ...args: any[]) => string;
  }
 
  interface ComponentOptionsBase extends CoreBladeAdditionalSettings {}
}

export {};
