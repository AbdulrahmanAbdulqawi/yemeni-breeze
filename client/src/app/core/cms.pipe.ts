import { Pipe, PipeTransform, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { ContentService } from './content.service';

/**
 * Like `| transloco`, but reads admin-editable copy from ContentService first
 * (see ContentBlock on the server) and falls back to the static i18n JSON — used
 * for the same key — when a key hasn't been seeded yet or has been cleared.
 */
@Pipe({ name: 'cms', pure: false })
export class CmsPipe implements PipeTransform {
  private content = inject(ContentService);
  private transloco = inject(TranslocoService);

  transform(key: string): string {
    const value = this.content.values()[key];
    return value ? value : this.transloco.translate(key);
  }
}
