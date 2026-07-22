import { Component, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { Lang, LanguageService } from '../core/language.service';

@Component({
  selector: 'app-public-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TranslocoPipe],
  templateUrl: './public-layout.html',
  styleUrl: './public-layout.scss'
})
export class PublicLayout {
  readonly lang = inject(LanguageService);
  readonly menuOpen = signal(false);
  readonly languages: { code: Lang; label: string }[] = [
    { code: 'en', label: 'EN' },
    { code: 'nl', label: 'NL' },
    { code: 'ar', label: 'ع' }
  ];

  setLang(code: Lang) {
    this.lang.set(code);
  }

  toggleMenu() {
    this.menuOpen.update(v => !v);
  }

  closeMenu() {
    this.menuOpen.set(false);
  }
}
