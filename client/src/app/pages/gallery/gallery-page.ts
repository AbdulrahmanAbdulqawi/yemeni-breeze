import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiService } from '../../core/api.service';
import { LanguageService } from '../../core/language.service';
import { GalleryItemDto } from '../../core/models';

@Component({
  selector: 'app-gallery-page',
  imports: [TranslocoPipe],
  templateUrl: './gallery-page.html',
  styleUrl: './gallery-page.scss'
})
export class GalleryPage {
  private api = inject(ApiService);
  readonly lang = inject(LanguageService);

  readonly items = toSignal(this.api.getGallery(), { initialValue: [] as GalleryItemDto[] });
}
