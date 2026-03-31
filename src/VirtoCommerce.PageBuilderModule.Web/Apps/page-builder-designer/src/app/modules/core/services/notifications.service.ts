import { Injectable, inject } from "@angular/core";
import { IndividualConfig, ToastrService } from "ngx-toastr";

@Injectable({
    providedIn: "root"
})
export class NotificationsService {

    private readonly toastr = inject(ToastrService);

    // todo: use options from config
    private _options = {
    };
    private _successOptions = {
        // timeOut: 0,
        // extendedTimeOut: 0
    };
    private _errorOptions = {};
    private _notifyOptions = {};

    successLeft(message: string, options: Partial<IndividualConfig> | undefined = undefined) {
        this.success(message, {
            ...this._successOptions,
            ...options,
            positionClass: "toast-bottom-left"
        });
    }

    successRight(message: string, options: Partial<IndividualConfig> | undefined = undefined) {
        this.success(message, {
            ...this._successOptions,
            ...options,
            positionClass: "toast-top-right"
        });
    }

    errorRight(message: string, options: Partial<IndividualConfig> | undefined = undefined) {
        this.error(message, {
            ...this._successOptions,
            ...options,
            positionClass: "toast-top-right"
        });
    }

    show(message: string, type: 'error'|'success'|'info'|'warning', position: 'tr'|'bl') {
        const options = {
            ...this._options,
            positionClass: position === 'tr' ? "toast-top-right" : "toast-bottom-left"
        };
        this.toastr[type](message, undefined, options);
    }

    demotr() {
        const troptions = {
            ...this._successOptions,
            positionClass: "toast-top-right"
        };
        this.toastr.success("This is a success message", "Success", troptions);
        this.toastr.error("This is an error message", "Error", troptions);
        this.toastr.warning("This is a warning message", "Warning", troptions);
        this.toastr.info("This is an info message", "Info", troptions);
    }

    demobl() {
        const bloptions = {
            ...this._successOptions,
            positionClass: "toast-bottom-left"
        };
        this.toastr.success("This is a success message", "Success", bloptions);
        this.toastr.error("This is an error message", "Error", bloptions);
        this.toastr.warning("This is a warning message", "Warning", bloptions);
        this.toastr.info("This is an info message", "Info", bloptions);
    }

    private success(message: string, options: Partial<IndividualConfig>) {
        this.toastr.success(message, undefined, {
            ...options
        });
    }

    private error(message: string, options: Partial<IndividualConfig>) {
        this.toastr.error(message, undefined, {
            ...options
        });
    }
}
