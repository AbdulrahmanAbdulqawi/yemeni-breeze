export type EventStatus = 'Draft' | 'Published' | 'Past';
export type RegistrationStatus = 'Confirmed' | 'Waitlisted' | 'Cancelled' | 'Pending' | 'Rejected';

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
  /** Signups wait for an admin to accept or reject them; no email until then. */
  requiresApproval: boolean;
  /** People (seats) waiting for review. */
  pendingCount: number;
  /** "What to expect" items, one per line. */
  whatToExpectEn: string;
  whatToExpectNl: string;
  whatToExpectAr: string;
  /** Optional; when set, ticket emails include a donation message with this link. */
  donationUrl: string | null;
  /** Private; only filled in on admin requests. Sent only in the emails of people who have a spot. */
  exactAddress: string | null;
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
  requiresApproval: boolean;
  whatToExpectEn: string;
  whatToExpectNl: string;
  whatToExpectAr: string;
  donationUrl: string | null;
  exactAddress: string | null;
}

export interface RegisterRequest {
  fullName: string;
  email: string;
  phone?: string;
  guestsCount: number;
  note?: string;
  language?: string;
  /** Honeypot: a hidden field only bots fill in. */
  website?: string;
  country: string;
  reason: string;
  /** Required when guestsCount > 1. */
  guestNames?: string | null;
  isVegetarian?: boolean | null;
  dietaryRestrictions?: string;
  wantsUpdates: boolean;
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
  lastEmailKind: EmailKind | null;
  lastEmailStatus: EmailStatus | null;
  lastEmailAt: string | null;
  lastEmailError: string | null;
  country: string | null;
  reason: string | null;
  guestNames: string | null;
  isVegetarian: boolean | null;
  dietaryRestrictions: string | null;
  wantsUpdates: boolean;
}

export type EmailKind = 'Confirmed' | 'Promoted' | 'Waitlisted' | 'Cancelled' | 'Reminder' | 'Rejected' | 'Feedback';

/** Answers to the after-event feedback form. Lists hold option codes. */
export interface FeedbackAnswers {
  expectations: string[];
  expectationsOther: string | null;
  metExpectations: string | null;
  improvements: string | null;
  favouritePart: string | null;
  joinCommunity: string | null;
  contributions: string[];
  contributionsOther: string | null;
  dishes: string | null;
  regionalFood: string | null;
  music: string[];
  dances: string[];
  dancesOther: string | null;
  traditions: string | null;
  missing: string | null;
  engagement: string | null;
  ideas: string | null;
  anythingElse: string | null;
}

/** The feedback page, reached via the private link in the feedback email. */
export interface FeedbackForm {
  firstName: string;
  language: string;
  event: { titleEn: string; titleNl: string; titleAr: string; date: string };
  answers: FeedbackAnswers | null;
  isComplete: boolean;
}

export interface FeedbackRow {
  id: number;
  registrationId: number;
  fullName: string;
  email: string;
  phone: string | null;
  answers: FeedbackAnswers;
  isComplete: boolean;
  updatedAt: string;
}

export interface FeedbackSummary {
  confirmed: number;
  checkedIn: number;
  emailsSent: number;
  emailsPending: number;
  responses: number;
  completed: number;
  wantToJoin: number;
  rows: FeedbackRow[];
}

export type FeedbackAudience = 'checkedIn' | 'confirmed';

/** Text in the site's three languages. Dutch and Arabic may be empty: English is the fallback. */
export interface LocalizedText {
  en: string;
  nl: string;
  ar: string;
}

export type VolunteerFieldType = 'short' | 'long' | 'email' | 'phone' | 'one' | 'many' | 'yesno' | 'date';

/**
 * One question on the Volunteer form. `lock` marks the questions that can't be removed:
 * 'contact' (name, email) and 'roles' (what they want to help with).
 */
export interface VolunteerFormField {
  id: string;
  type: VolunteerFieldType;
  lock: 'contact' | 'roles' | null;
  required: boolean;
  label: LocalizedText;
  help: LocalizedText;
  options: { id: string; label: LocalizedText }[];
}

/** An answer to one of the form's other questions, with the question's wording when it was sent. */
export interface VolunteerAnswer {
  fieldId: string;
  type: VolunteerFieldType;
  question: LocalizedText;
  value: string | null;
  choices: LocalizedText[] | null;
}

/**
 * Someone who wants to volunteer: they signed up on the website, said yes or maybe in an
 * event's feedback, or both. People are matched by email, so each shows once.
 */
export interface Volunteer {
  email: string;
  fullName: string;
  phone: string | null;
  language: string;
  contributions: string[];
  contributionsOther: string | null;
  latestAt: string;
  contacted: boolean;
  signup: { id: number; signedUpAt: string; answers: VolunteerAnswer[] } | null;
  feedback: {
    responseId: number;
    interest: 'yes' | 'maybe';
    event: { id: number; titleEn: string; titleNl: string; titleAr: string; date: string };
    eventsCount: number;
    ideas: string | null;
    anythingElse: string | null;
    repliedAt: string;
  } | null;
}
export type EmailStatus = 'Pending' | 'Sent' | 'Failed' | 'Skipped';

/** The "manage my registration" page, reached via the private link in emails. */
export interface ManagedRegistration {
  fullName: string;
  guestsCount: number;
  status: RegistrationStatus;
  canCancel: boolean;
  event: {
    slug: string;
    titleEn: string;
    titleNl: string;
    titleAr: string;
    date: string;
    startTime: string;
    endTime: string;
    location: string;
  };
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

export interface GalleryPageDto {
  items: GalleryItemDto[];
  total: number;
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
