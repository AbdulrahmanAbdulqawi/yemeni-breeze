import { DOCUMENT } from '@angular/common';
import { Injectable, inject, signal } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { EventDto, GalleryItemDto, TeamMemberDto } from './models';

export type Lang = 'en' | 'nl' | 'ar';
const LANG_KEY = 'yb_lang';

@Injectable({ providedIn: 'root' })
export class LanguageService {
  private transloco = inject(TranslocoService);
  private document = inject(DOCUMENT);

  readonly current = signal<Lang>('en');

  init() {
    const saved = localStorage.getItem(LANG_KEY) as Lang | null;
    this.set(saved && ['en', 'nl', 'ar'].includes(saved) ? saved : 'en');
  }

  set(lang: Lang) {
    this.current.set(lang);
    localStorage.setItem(LANG_KEY, lang);
    this.transloco.setActiveLang(lang);
    this.document.documentElement.lang = lang;
    this.document.documentElement.dir = lang === 'ar' ? 'rtl' : 'ltr';
  }

  /** Pick the localized field from a DB entity holding En/Nl/Ar columns. */
  pick(
    entity: EventDto | GalleryItemDto | TeamMemberDto,
    base: 'title' | 'description' | 'caption' | 'role' | 'bio'
  ): string {
    const suffix = this.current() === 'nl' ? 'Nl' : this.current() === 'ar' ? 'Ar' : 'En';
    const record = entity as unknown as Record<string, string>;
    return record[base + suffix] || record[base + 'En'] || '';
  }
}
