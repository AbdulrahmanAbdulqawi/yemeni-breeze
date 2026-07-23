import { Routes } from '@angular/router';
import { adminGuard } from './core/admin.guard';
import { PublicLayout } from './layout/public-layout';

export const routes: Routes = [
  {
    path: '',
    component: PublicLayout,
    children: [
      { path: '', loadComponent: () => import('./pages/home/home').then(m => m.Home) },
      { path: 'about', loadComponent: () => import('./pages/about/about').then(m => m.About) },
      { path: 'events', loadComponent: () => import('./pages/events/events-list').then(m => m.EventsList) },
      { path: 'events/:slug', loadComponent: () => import('./pages/events/event-detail').then(m => m.EventDetail) },
      { path: 'gallery', loadComponent: () => import('./pages/gallery/gallery-page').then(m => m.GalleryPage) },
      { path: 'contact', loadComponent: () => import('./pages/contact/contact-page').then(m => m.ContactPage) }
    ]
  },
  {
    path: 'admin/login',
    loadComponent: () => import('./admin/login').then(m => m.AdminLogin)
  },
  {
    path: 'admin',
    canActivate: [adminGuard],
    loadComponent: () => import('./admin/admin-layout').then(m => m.AdminLayout),
    children: [
      { path: '', redirectTo: 'events', pathMatch: 'full' },
      { path: 'events', loadComponent: () => import('./admin/admin-events').then(m => m.AdminEvents) },
      { path: 'events/new', loadComponent: () => import('./admin/admin-event-form').then(m => m.AdminEventForm) },
      { path: 'events/:eventId/registrations', loadComponent: () => import('./admin/admin-registrations').then(m => m.AdminRegistrations) },
      { path: 'events/:eventId/checkin', loadComponent: () => import('./admin/admin-checkin').then(m => m.AdminCheckin) },
      { path: 'events/:id', loadComponent: () => import('./admin/admin-event-form').then(m => m.AdminEventForm) },
      { path: 'media', loadComponent: () => import('./admin/media/media-studio').then(m => m.MediaStudio) },
      { path: 'gallery', redirectTo: 'media', pathMatch: 'full' },
      { path: 'messages', loadComponent: () => import('./admin/admin-messages').then(m => m.AdminMessages) }
    ]
  },
  { path: '**', redirectTo: '' }
];
