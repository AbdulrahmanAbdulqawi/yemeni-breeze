export type EventStatus = 'Draft' | 'Published' | 'Past';
export type RegistrationStatus = 'Confirmed' | 'Waitlisted' | 'Cancelled';

export interface EventDto {
  id: number;
  slug: string;
  titleEn: string;
  titleNl: string;
  titleAr: string;
  descriptionEn: string;
  descriptionNl: string;
  descriptionAr: string;
  date: string;
  startTime: string;
  endTime: string;
  location: string;
  capacity: number;
  imageUrl: string | null;
  status: EventStatus;
  isRegistrationOpen: boolean;
  confirmedCount: number;
  waitlistedCount: number;
  spotsLeft: number;
}

export interface EventInput {
  slug: string;
  titleEn: string;
  titleNl: string;
  titleAr: string;
  descriptionEn: string;
  descriptionNl: string;
  descriptionAr: string;
  date: string;
  startTime: string;
  endTime: string;
  location: string;
  capacity: number;
  imageUrl: string | null;
  status: EventStatus;
  isRegistrationOpen: boolean;
}

export interface RegisterRequest {
  fullName: string;
  email: string;
  phone?: string;
  guestsCount: number;
  note?: string;
  language?: string;
}

export interface RegistrationResult {
  id: number;
  status: RegistrationStatus;
  eventSlug: string;
}

export interface RegistrationDto {
  id: number;
  eventId: number;
  fullName: string;
  email: string;
  phone: string | null;
  guestsCount: number;
  note: string | null;
  status: RegistrationStatus;
  createdAt: string;
  checkedInAt: string | null;
}

export interface CheckinResult {
  alreadyCheckedIn: boolean;
  registration: RegistrationDto;
}

export type MediaType = 'image' | 'video' | 'file';

export interface MediaFolderDto {
  id: number;
  name: string;
  eventId: number | null;
  sortOrder: number;
  itemCount: number;
}

export interface GalleryItemDto {
  id: number;
  eventId: number | null;
  folderId: number | null;
  imageUrl: string;
  thumbUrl: string | null;
  mediaType: MediaType;
  captionEn: string;
  captionNl: string;
  captionAr: string;
  sortOrder: number;
}

export interface UploadResult {
  url: string;
  thumbUrl: string;
}

export interface UploadedFile {
  url: string;
  kind: MediaType;
  contentType: string;
}

/** Emitted while a single file uploads: progress ticks, then one `done` with the stored media. */
export interface UploadProgress {
  percent: number;
  done: boolean;
  result: { url: string; thumbUrl: string | null; mediaType: MediaType } | null;
}

export type SiteSettings = Record<string, string>;

export interface ContactMessageDto {
  id: number;
  name: string;
  email: string;
  subject: string;
  message: string;
  createdAt: string;
  isRead: boolean;
}

export interface LoginResponse {
  token: string;
  email: string;
  expiresAt: string;
}

export type ContentDict = Record<string, string>;

export interface ContentBlockDto {
  key: string;
  valueEn: string;
  valueNl: string;
  valueAr: string;
}

export interface ContentBlockInput {
  en: string;
  nl: string;
  ar: string;
}

export interface TeamMemberDto {
  id: number;
  name: string;
  roleEn: string;
  roleNl: string;
  roleAr: string;
  photoUrl: string | null;
  bioEn: string | null;
  bioNl: string | null;
  bioAr: string | null;
  slug: string | null;
  sortOrder: number;
}

export interface TeamMemberInput {
  name: string;
  roleEn: string;
  roleNl: string;
  roleAr: string;
  photoUrl: string | null;
  bioEn: string | null;
  bioNl: string | null;
  bioAr: string | null;
  sortOrder: number;
}
