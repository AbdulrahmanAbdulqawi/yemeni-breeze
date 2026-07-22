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

export interface GalleryItemDto {
  id: number;
  eventId: number | null;
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
