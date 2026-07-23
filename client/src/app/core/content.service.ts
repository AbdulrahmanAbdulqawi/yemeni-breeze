import { Injectable, inject } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap } from 'rxjs';
import { ApiService } from './api.service';
import { LanguageService } from './language.service';
import { ContentDict } from './models';

/** Admin-editable page copy (see ContentBlock on the server), refetched whenever the active language changes. */
@Injectable({ providedIn: 'root' })
export class ContentService {
  private api = inject(ApiService);
  private lang = inject(LanguageService);

  readonly values = toSignal(
    toObservable(this.lang.current).pipe(switchMap(lang => this.api.getContent(lang))),
    { initialValue: {} as ContentDict }
  );
}
