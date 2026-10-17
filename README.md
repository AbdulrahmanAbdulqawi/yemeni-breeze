# Yemen Breeze — نسمات اليمن

Website for **Yemen Breeze**, a youth-led cultural initiative in Amsterdam.
*We CONNECT cultures. We SHOWCASE Yemen. We BUILD bridges.*

## Stack

| Part | Tech |
|---|---|
| `server/YemeniBreeze.Api` | ASP.NET Core (.NET 10) Web API, EF Core + SQLite, ASP.NET Identity + JWT |
| `client/` | Angular 20 (standalone + signals), Transloco i18n (EN / NL / AR with RTL), SCSS design tokens from the brand logo |
| `server/YemeniBreeze.Tests` | xUnit integration tests (registration/waitlist logic) |

## Features

- **Public site** — Home, Our Story, Events, Gallery, Contact; fully trilingual (English, Dutch, Arabic with right-to-left layout); per-page localized SEO meta.
- **Event registration with automatic waitlist** — capacity-aware: when confirmed seats reach capacity, new signups become *Waitlisted*. Duplicate emails per event are rejected. Cancelling a confirmed registration auto-promotes the earliest waitlisted signup that fits.
- **Emails (Resend or SMTP)** — branded, localized (EN/NL/AR, RTL for Arabic) confirmation email with the logo, an arch-framed **QR entry ticket** and a calendar (.ics) attachment, plus waitlist and promotion notifications, each with a plain-text version. Templates live in `server/YemeniBreeze.Api/Features/Email/RegistrationEmails.cs`.
- **Reliable email delivery** — emails are queued in the database (`OutgoingEmails`) and sent by a background worker, so a provider outage or an app restart doesn't lose a ticket. Rate limits and outages are retried with backoff (up to 6 tries over ~4.5 hours); rejected addresses fail right away. The admin registrations table shows each person's latest email status (hover a failure for the reason) and has a **Resend email** button.
- **Self-service cancellation** — every email has a private "Cancel your registration" link (`/registrations/:token`). Cancelling frees the seat, promotes the next waitlisted person, and emails both. People who cancelled can register again.
- **Day-before reminder** — from 09:00 Amsterdam time on the day before an event, confirmed registrants get a reminder with their ticket again (skipped for anyone who registered in the last 12 hours).
- **Abuse protection** — the public forms are rate-limited per IP (`RATE_LIMIT_PER_10_MIN`, default 10), registration has a hidden honeypot field, and email addresses are validated on the server. The provider is picked by env vars: `EMAIL_PROVIDER=resend` + `RESEND_API_KEY` for [Resend](https://resend.com), or `EMAIL_PROVIDER=smtp` + `EMAIL_SMTP_*` for any SMTP relay (Brevo by default). With `EMAIL_PROVIDER` blank, Resend is used when `RESEND_API_KEY` is set, otherwise SMTP. Without credentials for the chosen provider the app logs instead of sending (dev mode).
- **QR check-in** — `/admin/events/:id/checkin`: camera scanning (phone-friendly) plus name/email search fallback, live seats counter; check-in status in the registrations table and CSV.
- **Images** — server-side resize to WebP (1600px large + 480px thumb, EXIF stripped), drag-and-drop multi-upload, per-event photo albums, admin-managed hero/About images.
- **Motion** — the Home hero photo slowly zooms in and out (CSS, `pages/home/home.scss`); GSAP scroll reveals, stat count-ups and a 3D card tilt live in `shared/motion/`; lightboxes use Angular's `animate.enter`/`animate.leave` and routes cross-fade via View Transitions. Everything falls back to the static page under `prefers-reduced-motion`.
- **Admin dashboard** (`/admin`) — JWT login; events CRUD in all three languages; registrations per event with waitlist promotion, cancellation, and **CSV export**; gallery + branding management; contact-message inbox.

## Run locally

API (http://localhost:5239):

```bash
cd server/YemeniBreeze.Api
dotnet run --launch-profile http
```

Client (http://localhost:4200, proxies `/api` + `/uploads` to the API):

```bash
cd client
npm start
```

Default admin login (dev only — change via `Admin:Email` / `Admin:Password` config):
`admin@yemenbreeze.nl` / `ChangeMe!2026`

## Tests

```bash
cd server
dotnet test
```

## Deploy

**Local Docker:** `docker compose up -d --build` → site on port 8080 (nginx serves Angular, proxies `/api` and `/uploads` to the API container; SQLite DB and uploads persist in named volumes).

**Production (Coolify):** add a *Docker Compose* resource pointing at `docker-compose.coolify.yml`, assign the domain to the `web` service (Traefik handles TLS), and set the env vars listed in that file (`JWT_KEY`, `ADMIN_EMAIL`, `ADMIN_PASSWORD`, `SITE_ORIGIN`, `EMAIL_FROM_ADDRESS`, plus either `RESEND_API_KEY` or `EMAIL_SMTP_USER` + `EMAIL_SMTP_KEY`). Enable the GitHub webhook for push-to-deploy. Nightly backups: schedule `deploy/backup.sh` (SQLite snapshot + uploads tar, 14-day rotation).

## Brand

Design tokens live in `client/src/styles.scss`, sampled from the qamariya logo:
brown `#5B1F05`, maroon `#8F1B04`, cream `#F6E7BE`, accents red `#DA4B3A`, orange `#ED9E42`, yellow `#EDCB4E`, blue `#2B2BE0`, green `#3BAB4B`.
Fonts: Cinzel + Source Sans 3 (Latin), Amiri + Cairo (Arabic).
Logo assets: `client/public/assets/`.

## Content still needed

- Real event photos for the gallery and hero sections
- Final domain name (for SEO/OG tags + email sender)
- Decision on registration confirmation emails (needs SMTP/SendGrid) — not in v1
