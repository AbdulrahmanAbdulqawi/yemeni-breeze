import { HttpClient, HttpEvent, HttpEventType } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map } from 'rxjs';
import {
  CheckinResult,
  ContactMessageDto,
  ContentBlockDto,
  ContentBlockInput,
  ContentDict,
  EventDto,
  EventInput,
  GalleryItemDto,
  LoginResponse,
  MediaFolderDto,
  RegisterRequest,
  RegistrationDto,
  RegistrationResult,
  RegistrationStatus,
  SiteSettings,
  TeamMemberDto,
  TeamMemberInput,
  UploadedFile,
  UploadProgress,
  UploadResult
} from './models';

function toUploadProgress(event: HttpEvent<UploadResult | UploadedFile>, isImage: boolean): UploadProgress {
  if (event.type === HttpEventType.UploadProgress) {
    const percent = event.total ? Math.round((event.loaded / event.total) * 100) : 0;
    // cap at 99 until the response lands — the server still has to resize/store
    return { percent: Math.min(percent, 99), done: false, result: null };
  }

  if (event.type === HttpEventType.Response && event.body) {
    const body = event.body;
    const result = isImage
      ? { url: (body as UploadResult).url, thumbUrl: (body as UploadResult).thumbUrl, mediaType: 'image' as const }
      : { url: (body as UploadedFile).url, thumbUrl: null, mediaType: (body as UploadedFile).kind };
    return { percent: 100, done: true, result };
  }

  return { percent: 0, done: false, result: null };
}

@Injectable({ providedIn: 'root' })
export class ApiService {
  private http = inject(HttpClient);

  // Public
  getEvents() {
    return this.http.get<EventDto[]>('/api/events');
  }

  getEvent(slug: string) {
    return this.http.get<EventDto>(`/api/events/${slug}`);
  }

  register(slug: string, request: RegisterRequest) {
    return this.http.post<RegistrationResult>(`/api/events/${slug}/register`, request);
  }

  getGallery() {
    return this.http.get<GalleryItemDto[]>('/api/gallery');
  }

  sendContact(request: { name: string; email: string; subject: string; message: string }) {
    return this.http.post<{ message: string }>('/api/contact', request);
  }

  // Auth
  login(email: string, password: string) {
    return this.http.post<LoginResponse>('/api/auth/login', { email, password });
  }

  // Admin
  adminGetEvents() {
    return this.http.get<EventDto[]>('/api/admin/events');
  }

  adminCreateEvent(input: EventInput) {
    return this.http.post<EventDto>('/api/admin/events', input);
  }

  adminUpdateEvent(id: number, input: EventInput) {
    return this.http.put<EventDto>(`/api/admin/events/${id}`, input);
  }

  adminDeleteEvent(id: number) {
    return this.http.delete(`/api/admin/events/${id}`);
  }

  adminGetRegistrations(eventId: number) {
    return this.http.get<RegistrationDto[]>(`/api/admin/events/${eventId}/registrations`);
  }

  adminSetRegistrationStatus(eventId: number, id: number, status: RegistrationStatus) {
    return this.http.post<RegistrationDto>(
      `/api/admin/events/${eventId}/registrations/${id}/status`, { status });
  }

  registrationsCsvUrl(eventId: number) {
    return `/api/admin/events/${eventId}/registrations/export.csv`;
  }

  adminAddGalleryItem(input: Omit<GalleryItemDto, 'id'>) {
    return this.http.post<GalleryItemDto>('/api/admin/gallery', input);
  }

  adminUpdateGalleryItem(id: number, input: Omit<GalleryItemDto, 'id'>) {
    return this.http.put<GalleryItemDto>(`/api/admin/gallery/${id}`, input);
  }

  adminDeleteGalleryItem(id: number) {
    return this.http.delete(`/api/admin/gallery/${id}`);
  }

  adminGetMessages() {
    return this.http.get<ContactMessageDto[]>('/api/admin/contact');
  }

  adminMarkMessageRead(id: number) {
    return this.http.post<ContactMessageDto>(`/api/admin/contact/${id}/read`, {});
  }

  adminDeleteMessage(id: number) {
    return this.http.delete(`/api/admin/contact/${id}`);
  }

  adminUpload(file: File) {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<UploadResult>('/api/admin/uploads', form);
  }

  adminUploadBatch(files: File[]) {
    const form = new FormData();
    for (const file of files) form.append('files', file);
    return this.http.post<UploadResult[]>('/api/admin/uploads/batch', form);
  }

  adminUploadFile(file: File) {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<UploadedFile>('/api/admin/uploads/file', form);
  }

  /**
   * Uploads one file, emitting progress percentages then the final result.
   * Images go through the resize pipeline; everything else is stored as-is.
   */
  adminUploadWithProgress(file: File) {
    const isImage = file.type.startsWith('image/');
    const url = isImage ? '/api/admin/uploads' : '/api/admin/uploads/file';
    const form = new FormData();
    form.append('file', file);

    return this.http
      .post<UploadResult | UploadedFile>(url, form, { reportProgress: true, observe: 'events' })
      .pipe(map(event => toUploadProgress(event, isImage)));
  }

  // Folders
  getFolders() {
    return this.http.get<MediaFolderDto[]>('/api/folders');
  }

  adminCreateFolder(input: { name: string; eventId: number | null; sortOrder: number }) {
    return this.http.post<MediaFolderDto>('/api/admin/folders', input);
  }

  adminUpdateFolder(id: number, input: { name: string; eventId: number | null; sortOrder: number }) {
    return this.http.put<MediaFolderDto>(`/api/admin/folders/${id}`, input);
  }

  adminDeleteFolder(id: number) {
    return this.http.delete(`/api/admin/folders/${id}`);
  }

  // Bulk gallery actions
  adminBulkMove(ids: number[], folderId: number | null) {
    return this.http.post<{ moved: number }>('/api/admin/gallery/bulk-move', { ids, folderId });
  }

  adminBulkDelete(ids: number[]) {
    return this.http.post<{ deleted: number }>('/api/admin/gallery/bulk-delete', { ids });
  }

  adminBulkUpdateCaptions(
    ids: number[],
    captions: { captionEn?: string | null; captionNl?: string | null; captionAr?: string | null }
  ) {
    return this.http.post<GalleryItemDto[]>('/api/admin/gallery/bulk-update', { ids, ...captions });
  }

  getSettings() {
    return this.http.get<SiteSettings>('/api/settings');
  }

  adminUpdateSettings(settings: SiteSettings) {
    return this.http.put<SiteSettings>('/api/admin/settings', settings);
  }

  adminCheckin(eventId: number, ticketCode: string) {
    return this.http.post<CheckinResult>(`/api/admin/events/${eventId}/checkin`, { ticketCode });
  }

  adminCheckinById(eventId: number, registrationId: number) {
    return this.http.post<CheckinResult>(
      `/api/admin/events/${eventId}/registrations/${registrationId}/checkin`, {});
  }

  // Content blocks
  getContent(lang: string) {
    return this.http.get<ContentDict>(`/api/content?lang=${lang}`);
  }

  adminGetContent() {
    return this.http.get<ContentBlockDto[]>('/api/admin/content');
  }

  adminUpdateContent(values: Record<string, ContentBlockInput>) {
    return this.http.put<ContentBlockDto[]>('/api/admin/content', values);
  }

  // Team
  getTeam() {
    return this.http.get<TeamMemberDto[]>('/api/team');
  }

  getTeamMember(slug: string) {
    return this.http.get<TeamMemberDto>(`/api/team/${slug}`);
  }

  adminGetTeam() {
    return this.http.get<TeamMemberDto[]>('/api/admin/team');
  }

  adminCreateTeamMember(input: TeamMemberInput) {
    return this.http.post<TeamMemberDto>('/api/admin/team', input);
  }

  adminUpdateTeamMember(id: number, input: TeamMemberInput) {
    return this.http.put<TeamMemberDto>(`/api/admin/team/${id}`, input);
  }

  adminDeleteTeamMember(id: number) {
    return this.http.delete(`/api/admin/team/${id}`);
  }
}
