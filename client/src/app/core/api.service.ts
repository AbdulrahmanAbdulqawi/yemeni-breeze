import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import {
  CheckinResult,
  ContactMessageDto,
  EventDto,
  EventInput,
  GalleryItemDto,
  LoginResponse,
  RegisterRequest,
  RegistrationDto,
  RegistrationResult,
  RegistrationStatus,
  SiteSettings,
  UploadResult
} from './models';

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
}
