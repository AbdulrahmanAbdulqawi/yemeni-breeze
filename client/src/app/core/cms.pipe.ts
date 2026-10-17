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
    return value ? tidy(value) : this.transloco.translate(key);
  }
}

/**
 * Pasted copy often carries leading/trailing blank lines or runs of empty lines; with
 * `white-space: pre-line` those would show as gaps, so keep at most one empty line in a row.
 */
function tidy(text: string): string {
  return text.trim().replace(/\n[ \t]*(\n[ \t]*){2,}/g, '\n\n');
}
