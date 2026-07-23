const MEDIA_PREFIX = '/api/media/';

/**
 * Client-side mirror of the server's `ImageService.OriginalUrlFromUrl` convention:
 * every processed image is stored as "{name}.webp" alongside its untouched original
 * at "{name}-original" (no DB column for it — see the V6 plan). Only meaningful for
 * a *large* image URL (not the "-thumb" variant, not video/file media).
 */
export function originalUrl(imageUrl: string): string {
  if (!imageUrl.startsWith(MEDIA_PREFIX)) return imageUrl;
  const key = imageUrl.slice(MEDIA_PREFIX.length);
  const dot = key.lastIndexOf('.');
  const stem = dot === -1 ? key : key.slice(0, dot);
  return `${MEDIA_PREFIX}${stem}-original`;
}
