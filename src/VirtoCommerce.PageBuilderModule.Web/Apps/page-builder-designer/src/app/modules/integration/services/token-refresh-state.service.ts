import { Injectable } from '@angular/core';
import { Subject, Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class TokenRefreshStateService {
  private _inProgress = false;
  private readonly _refreshedSource = new Subject<void>();
  readonly refreshed$: Observable<void> = this._refreshedSource.asObservable();

  get inProgress(): boolean {
    return this._inProgress;
  }

  start(): void {
    this._inProgress = true;
  }

  complete(): void {
    this._inProgress = false;
    this._refreshedSource.next();
  }

  fail(): void {
    this._inProgress = false;
  }
}
