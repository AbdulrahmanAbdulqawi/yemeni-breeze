import { Injectable } from '@angular/core';
import { GalleryItemDto } from './models';

interface CachedPage {
  items: GalleryItemDto[];
  total: number;
}

/**
 * In-memory cache of loaded gallery pages, keyed by folder filter ("all" or a folder id).
 * Root-provided so it survives navigating away from /gallery and back within the same SPA
 * session — flipping between folder tabs or returning to the gallery is then instant instead
 * of re-fetching the list. Deliberately not persisted across a hard reload: the underlying
 * image bytes are already browser-disk-cached (immutable Cache-Control), so a fresh page load
 * re-fetching a small JSON list is an acceptable, low-complexity boundary.
 */
@Injectable({ providedIn: 'root' })
export class GalleryCacheService {
  private cache = new Map<string, CachedPage>();

  private key(folderId: number | null): string {
    return folderId === null ? 'all' : String(folderId);
  }

  get(folderId: number | null): CachedPage | undefined {
    return this.cache.get(this.key(folderId));
  }

  set(folderId: number | null, page: CachedPage): void {
    this.cache.set(this.key(folderId), page);
  }

  clear(): void {
    this.cache.clear();
  }
}
