# Yemeni Breeze — نسمات اليمن

Website for **Yemeni Breeze**, a youth-led cultural initiative in Amsterdam.
*We CONNECT cultures. We SHOWCASE Yemen. We BUILD bridges.*

## Stack

| Part | Tech |
|---|---|
| `server/YemeniBreeze.Api` | ASP.NET Core (.NET 10) Web API, EF Core + SQLite, ASP.NET Identity + JWT |
| `client/` | Angular 20 (standalone + signals), Transloco i18n (EN / NL / AR with RTL), SCSS design tokens from the brand logo |
| `server/YemeniBreeze.Tests` | xUnit integration tests (registration/waitlist logic) |

## Features

- **Public site** — Home, Our Story, Events, Gallery, Contact; fully trilingual (English, Dutch, Arabic with right-to-left layout).
- **Event registration with automatic waitlist** — capacity-aware: when confirmed seats reach capacity, new signups become *Waitlisted*. Duplicate emails per event are rejected.
- **Admin dashboard** (`/admin`) — JWT login; events CRUD in all three languages with image upload; registrations per event with waitlist promotion, cancellation, and **CSV export**; gallery management; contact-message inbox.

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
`admin@yemenibreeze.nl` / `ChangeMe!2026`

## Tests

```bash
cd server
dotnet test
```

## Deploy (Docker, hosting-agnostic)

```bash
docker compose up -d --build
```

Serves the site on port 8080 (nginx serves Angular, proxies `/api` and `/uploads` to the API container; SQLite DB and uploads persist in named volumes). Set `JWT_KEY`, `ADMIN_EMAIL`, `ADMIN_PASSWORD`, `SITE_ORIGIN` via environment/`.env` in production.

## Brand

Design tokens live in `client/src/styles.scss`, sampled from the qamariya logo:
brown `#5B1F05`, maroon `#8F1B04`, cream `#F6E7BE`, accents red `#DA4B3A`, orange `#ED9E42`, yellow `#EDCB4E`, blue `#2B2BE0`, green `#3BAB4B`.
Fonts: Cinzel + Source Sans 3 (Latin), Amiri + Cairo (Arabic).
Logo assets: `client/public/assets/`.

## Content still needed

- Real event photos for the gallery and hero sections
- Final domain name (for SEO/OG tags + email sender)
- Decision on registration confirmation emails (needs SMTP/SendGrid) — not in v1
