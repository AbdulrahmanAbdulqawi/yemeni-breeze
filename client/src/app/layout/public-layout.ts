import { Component, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { CmsPipe } from '../core/cms.pipe';
import { LanguageService } from '../core/language.service';
import { LanguageSwitcher } from '../core/language-switcher';
import { SeoService } from '../core/seo.service';

@Component({
  selector: 'app-public-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TranslocoPipe, CmsPipe, LanguageSwitcher],
  templateUrl: './public-layout.html',
  styleUrl: './public-layout.scss'
})
export class PublicLayout {
  readonly lang = inject(LanguageService);

  constructor() {
    inject(SeoService).init();
  }
  readonly menuOpen = signal(false);

  toggleMenu() {
    this.menuOpen.update(v => !v);
  }

  closeMenu() {
    this.menuOpen.set(false);
  }
}
