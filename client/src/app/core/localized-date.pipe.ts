import { Pipe, PipeTransform, inject } from '@angular/core';
import { LanguageService } from './language.service';

const LOCALES = { en: 'en', nl: 'nl', ar: 'ar-u-nu-latn' } as const;

@Pipe({ name: 'localizedDate', pure: false })
export class LocalizedDatePipe implements PipeTransform {
  private lang = inject(LanguageService);

  transform(value: string | Date | null | undefined, style: 'full' | 'long' | 'medium' = 'long'): string {
    if (!value) return '';
    const date = typeof value === 'string' ? new Date(value) : value;
    return new Intl.DateTimeFormat(LOCALES[this.lang.current()], { dateStyle: style }).format(date);
  }
}
