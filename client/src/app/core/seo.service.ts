import { Injectable, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { NavigationEnd, Router } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { combineLatest, filter, map, startWith, switchMap } from 'rxjs';

const PAGE_KEYS: Record<string, string> = {
  '': 'home',
  about: 'about',
  events: 'events',
  gallery: 'gallery',
  contact: 'contact'
};

@Injectable({ providedIn: 'root' })
export class SeoService {
  private router = inject(Router);
  private title = inject(Title);
  private meta = inject(Meta);
  private transloco = inject(TranslocoService);

  /** Call once from the public layout: keeps title/description in sync with route + language. */
  init() {
    const navigations = this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map(e => e.urlAfterRedirects),
      startWith(this.router.url)
    );

    navigations.pipe(
      map(url => PAGE_KEYS[url.split('?')[0].split('#')[0].replace(/^\//, '')]),
      filter((page): page is string => !!page), // event detail + admin manage their own titles
      // selectTranslate waits for the language file and re-emits on language switch
      switchMap(page => combineLatest([
        this.transloco.selectTranslate(`seo.${page}.title`),
        this.transloco.selectTranslate(`seo.${page}.description`)
      ]))
    ).subscribe(([pageTitle, description]) => this.setTags(pageTitle, description));
  }

  /** For dynamic pages (event detail). */
  setTags(pageTitle: string, description: string) {
    const full = `${pageTitle} — Yemeni Breeze`;
    this.title.setTitle(full);
    this.meta.updateTag({ name: 'description', content: description });
    this.meta.updateTag({ property: 'og:title', content: full });
    this.meta.updateTag({ property: 'og:description', content: description });
  }
}
