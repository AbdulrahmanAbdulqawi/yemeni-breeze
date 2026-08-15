# CLAUDE.md

Guidance for AI assistants working in this repository.

**Yemeni Breeze (نسمات اليمن)** is the website for a youth-led Yemeni cultural initiative in
Amsterdam: a trilingual (EN / NL / AR) public marketing site plus an admin dashboard for events,
registrations with an automatic waitlist, QR check-in, media, and editable page copy.

Two deployables, one repo:

| Path | What it is |
|---|---|
| `server/YemeniBreeze.Api` | ASP.NET Core (.NET 10) minimal-API app — EF Core + SQLite, ASP.NET Identity + JWT, image processing, S3/disk storage, SMTP email |
| `client` | Angular 20 SPA — standalone components, signals, Transloco i18n with RTL |
| `server/YemeniBreeze.Tests` | xUnit integration tests driving the real app through `WebApplicationFactory` |

---

## Commands

There is **no `.sln` file**, so `dotnet` commands need an explicit project path.

```bash
# API on http://localhost:5239 (Development, seeds DB on startup)
dotnet run --project server/YemeniBreeze.Api --launch-profile http

# Tests (spins up the whole API in-process against in-memory SQLite)
dotnet test server/YemeniBreeze.Tests
dotnet test server/YemeniBreeze.Tests --filter FullyQualifiedName~Waitlist

# EF Core migrations
dotnet ef migrations add <Name> --project server/YemeniBreeze.Api
# Migrations are applied automatically at startup by SeedData.RunAsync — never run `database update` in prod.
```

```bash
cd client
npm install
npm start            # dev server on :4200, proxies /api + /uploads to :5239 (proxy.conf.json)
npm run build        # runs the i18n check first via the `prebuild` hook, then `ng build`
npm run i18n:check   # translation parity/usage check on its own — cheap, run it after touching i18n
npm test             # Karma + Jasmine (no spec files exist yet; schematics are configured skipTests)
```

```bash
docker compose up -d --build   # full stack on :8080 (nginx serves the SPA, proxies to the api container)
```

Local dev needs the .NET 10 SDK and Node 22+. `npm run build` will **fail the build** on a missing or
untranslated i18n key — that is intentional, fix the key rather than bypassing the hook.

---

## Backend architecture

### Vertical slices, minimal APIs

Every feature is a folder under `Features/` exposing one static `Map…Endpoints(this IEndpointRouteBuilder)`
extension method, registered in `Program.cs`. There are no controllers, no MediatR, no repository layer —
handlers take `AppDbContext` and services by parameter injection and talk to EF Core directly.

```
Features/
  Auth/          login → JWT
  Events/        public list/detail + admin CRUD
  Registrations/ public signup, admin status changes, check-in, CSV export
  Gallery/       public list + paginated page, admin CRUD + bulk ops
  Media/         media folders (albums)
  Content/       admin-editable copy (ContentBlock)
  Team/          team roster + public bio pages
  Settings/      key/value site settings (hero/about images)
  Contact/       public contact form + admin inbox
  Uploads/       upload endpoints + ImageService (resize pipeline)
  Storage/       StorageService (S3 or local disk)
  Email/         EmailService (SMTP, QR ticket, .ics)
```

Conventions inside a slice:

- DTOs and inputs are `record`s declared at the top of the endpoints file (`EventDto`, `EventInput`, …).
  Entities are never returned directly except for `ContactMessage`.
- Public routes are `/api/…`; admin routes are `/api/admin/…` and are grouped with
  `app.MapGroup("/api/admin/x").RequireAuthorization()`.
- Mapping helpers are `ToDto` — sometimes an extension (`Event.ToDto()`, `TeamMember.ToDto()`),
  sometimes a private static. Follow whatever the slice already does.
- Errors are `Results.BadRequest(new { message = "…" })` / `Conflict` / `NotFound`; the client surfaces
  `message` in toasts.

### Data

`Data/AppDbContext.cs` extends `IdentityDbContext<IdentityUser>`; all model configuration lives in
`OnModelCreating` (max lengths, unique indexes, delete behaviour). Entities are POCOs in
`Domain/Entities.cs`.

Notable constraints: `Event.Slug` unique; `Registration (EventId, Email)` unique; `Registration.TicketCode`
unique; `TeamMember.Slug` unique. Deleting an event cascades its registrations but **nulls out**
`GalleryItem.EventId`/`FolderId` (media survives).

`Data/SeedData.cs` runs on every startup: `Database.MigrateAsync()`, then idempotently seeds the admin
Identity user, four historical events, a media folder per event, and any `ContentBlock` keys missing from
`Data/ContentDefaults.cs`. **Seeding never overwrites existing rows**, so admin edits survive restarts —
preserve that property when adding seed data (there is a test for it).

Enums serialize as strings (`JsonStringEnumConverter` in `Program.cs`), which is why the client models
use string unions like `'Confirmed' | 'Waitlisted' | 'Cancelled'`.

### Auth

Single-role model: one seeded admin user, JWT bearer, 8-hour expiry, no roles or claims-based policies —
`RequireAuthorization()` means "logged in as the admin". Credentials come from `Admin:Email` /
`Admin:Password` config.

### Registration & waitlist rules (`Features/Registrations`)

The core business logic of the app; change it carefully and extend the tests in
`RegistrationWaitlistTests` / `V2FeatureTests`.

- Capacity is counted in **seats**, i.e. `Sum(GuestsCount)` of `Confirmed` registrations — not row count.
- A signup that would exceed capacity becomes `Waitlisted`; otherwise `Confirmed`.
- Email is lowercased and must be unique per event among non-cancelled registrations (409 otherwise).
- `GuestsCount` must be 1–10; registration must be open and the event `Published`.
- Cancelling a `Confirmed` registration runs `PromoteWaitlisted`, promoting the **earliest-registered**
  waitlisted entries that still fit — it may promote several, or none if the freed seats are too few.
- Every status transition into `Confirmed` fires an email (ticket, or "you're off the waitlist").
- Check-in is idempotent: it returns `alreadyCheckedIn` instead of erroring, and only `Confirmed`
  registrations can check in.

### Media pipeline (`Features/Uploads` + `Features/Storage`)

`ImageService.ProcessAsync` buffers the raw bytes, then stores **three objects per image** under one GUID
stem:

| Key | Purpose |
|---|---|
| `{guid}.webp` | 1600px-wide WebP q82, EXIF stripped — `ImageUrl` |
| `{guid}-thumb.webp` | 480px-wide WebP q78 — `ThumbUrl` |
| `{guid}-original` | untouched original bytes, for full-quality download |

Only the large URL is stored on entities that have a single image column (`Event.ImageUrl`,
`TeamMember.PhotoUrl`); the other two are **derived by convention** via
`ImageService.ThumbUrlFromUrl` / `OriginalUrlFromUrl` (mirrored client-side in `core/media-url.ts`).

> **Whenever you delete or replace an image, delete all three keys.** Forgetting the thumb or original
> leaks storage — that bug has been fixed twice already, and `V6MediaOriginalTests` guards it.

`StorageService` writes to an S3-compatible bucket when `Storage:*` config is present, otherwise to
`wwwroot/uploads`. The bucket is private; everything is read back through the `/api/media/{key}` proxy
(immutable cache headers, path-traversal guarded). Two quirks are load-bearing, don't "clean them up":
Hetzner needs `ForcePathStyle` + checksum `WHEN_REQUIRED` and a seekable buffered stream, and the local-disk
branch writes a `.contenttype` sidecar for extensionless keys (the `-original` ones).

Limits: 15 MB per image, 200 MB per video/PDF, 20 files per batch, 210 MB request body (Kestrel **and**
`client/nginx.conf` — raise both together).

### Email (`Features/Email/EmailService.cs`)

Fire-and-forget from handlers (`_ = emails.SendRegistrationEmailAsync(...)`) and **never throws** — it
catches and logs. Without `Email:SmtpHost`/`Email:SmtpKey` it logs what it would have sent, so dev works
unconfigured. Templates are inline HTML strings, localized from `Registration.Language` (`en`/`nl`/`ar`,
with `dir="rtl"` for Arabic), and confirmation mail embeds a QR PNG (`cid:ticket-qr`) plus an `.ics`
attachment.

---

## Frontend architecture

Angular 20, **standalone components only** (no NgModules), signals for state, zone-based change detection
with event coalescing. Everything below `src/app`:

- `core/` — singletons: `ApiService` (the **only** place HTTP calls live), `AuthService`, `LanguageService`,
  `ContentService`, `SeoService`, `GalleryCacheService`, the auth interceptor/guard, and pipes.
- `layout/public-layout.*` — public shell (nav, footer, language switcher); initializes `SeoService`.
- `pages/` — public routes. These use `templateUrl` + `styleUrl` (separate `.html`/`.scss` files).
- `admin/` — admin routes plus `admin/ui/` primitives (`PageHeader`, `Spinner`, `EmptyState`,
  `ToastService`, `ConfirmService`, `LangSelect`). These are **single-file components** with inline
  `template:` and `styles:`.
- `shared/event-card.ts` — the one cross-cutting public component (inline template).

Routing (`app.routes.ts`) is lazy everywhere via `loadComponent`. `withComponentInputBinding()` is enabled,
so route params bind straight to `input()` signals (e.g. `AdminCheckin.eventId`). Admin routes sit behind
`adminGuard`; `authInterceptor` attaches the bearer token and logs out + redirects on a 401 from `/api/admin`.

State style: `signal()` for local state, `computed()` for derivations, `toSignal()` for one-shot HTTP reads.
Components subscribe to `ApiService` observables directly; there is no store.

### Client models

`core/models.ts` is a hand-maintained mirror of the server DTOs. **Changing a server DTO means editing
`models.ts` and `api.service.ts` in the same change** — nothing generates or validates this.

---

## The three parallel content systems (read this before touching copy)

Text on the public site comes from three different places, and they interlock:

1. **Static UI strings** — `client/public/i18n/{en,nl,ar}.json`, rendered with `| transloco`.
2. **Admin-editable copy** — `ContentBlock` rows on the server, rendered with `| cms`. `CmsPipe` looks the
   key up in `ContentService` and **falls back to the transloco key of the same name** when the block is
   missing or empty. So an editable string needs its key in *both* systems.
3. **Per-entity translated columns** — `TitleEn/Nl/Ar`, `DescriptionEn/Nl/Ar`, `CaptionEn/Nl/Ar`,
   `RoleEn/Nl/Ar`, `BioEn/Nl/Ar`. Read them via `LanguageService.pick(entity, 'title')`, which falls back
   to English.

Adding an admin-editable string therefore touches four files: `Data/ContentDefaults.cs` (default EN/NL/AR),
`client/src/app/admin/content/content-fields.ts` (so it appears on the admin Content page, grouped, with
`multiline`/`shared` flags), all three `i18n/*.json` files (fallback), and the template that renders it.
New `ContentDefaults` keys need **no migration** — `SeedData` inserts missing keys on the next startup.

### Language & RTL

`LanguageService` is the single source of truth: it persists to `localStorage.yb_lang`, calls
`transloco.setActiveLang`, and sets `<html lang>` and `<html dir>`. Arabic swaps the font pairing via a
`[dir='rtl']` block in `styles.scss`.

CSS rules for RTL correctness:

- Use **logical properties** (`margin-inline-start`, `inset-inline-end`, `text-align: start`, `padding-block`)
  rather than left/right.
- `dir="rtl"` lives on `<html>`, outside every component, so component styles must use
  `:host-context([dir='rtl'])` — a plain `[dir='rtl'] &` selector will never match under Angular's view
  encapsulation. Existing files carry this comment; keep it.
- Directional interactions (lightbox arrow keys, swipe) invert in Arabic — see `gallery-page.ts`.

### Styling

Design tokens are CSS custom properties in `client/src/styles.scss` (`--yb-*`: brand browns, cream, gold,
qamariya accent colours, fonts, radii) plus shared classes (`.btn`, `.card`, `.section`, `.field`, `.badge`,
`.yb-table`, `.page-hero`, `.arch`). The admin dashboard layers its own `--ad-*` tokens scoped to
`.admin-shell` in `admin/admin-theme.scss` so the marketing styling stays untouched. Prefer an existing
token or shared class over new one-off values; production budgets cap component styles at 8 kB.

---

## Testing

`server/YemeniBreeze.Tests` only — there are no frontend specs. Tests use a shared `ApiFactory`
(`RegistrationWaitlistTests.cs`) that swaps the DbContext onto an in-memory SQLite connection kept open for
the fixture's lifetime; `SeedData` still runs, so the seeded admin (`admin@yemenibreeze.nl` /
`ChangeMe!2026`) and demo events exist in every test class. `public partial class Program;` at the bottom of
`Program.cs` exists solely so `WebApplicationFactory<Program>` can find the entry point — don't remove it.

Style: `[Fact]`, one class per feature wave (`V2FeatureTests`, `V4ContentAndTeamTests`, …), method names as
sentences — `Cancelling_Confirmed_Promotes_Earliest_Fitting_Waitlisted`. Tests drive real HTTP through
`HttpClient`, authenticate by POSTing to `/api/auth/login`, and seed fixtures by resolving `AppDbContext`
from a scope. Storage-related tests assert against the real disk-backed `StorageService`.

Add tests for: capacity/waitlist changes, anything that deletes media, auth boundaries on new admin
endpoints, and new public endpoints' shape.

---

## Recipes

**New API endpoint** → create/extend `Features/<Name>/<Name>Endpoints.cs` with a `Map…Endpoints` extension,
call it from `Program.cs`, add the method to `client/src/app/core/api.service.ts`, add types to
`core/models.ts`, add a test.

**New entity field** → edit `Domain/Entities.cs`, configure it in `AppDbContext.OnModelCreating` if it needs
a length/index, `dotnet ef migrations add <Name> --project server/YemeniBreeze.Api`, extend the DTO + input
record, extend `models.ts` and the admin form. Translated fields come in threes (`…En/Nl/Ar`).

**New UI string** → add the key to **all three** `client/public/i18n/*.json` files (`npm run i18n:check`
enforces parity, presence, and non-emptiness for statically-referenced keys), then use `| transloco`.

**New admin page** → single-file component under `client/src/app/admin/`, lazy route in `app.routes.ts`, nav
entry in `admin-layout.ts`, and compose with `app-page-header` / `app-spinner` / `app-empty-state` /
`ToastService` / `ConfirmService` rather than bespoke chrome.

---

## Gotchas

- `/api/gallery` (unpaginated) and `/api/gallery/page` (paginated, `take` clamped to 100, default 50) both
  exist on purpose: the event-detail album still relies on the full array. Don't delete the former.
- `GalleryCacheService` caches loaded gallery pages per folder for the SPA session. Admin mutations don't
  invalidate it — a hard reload does.
- `SettingsEndpoints` validates keys against an allowlist (`heroImageUrl`, `aboutImageUrl`); a new site
  setting must be added there or the PUT 400s.
- A `TeamMember` only gets a `Slug` — and therefore a public `/team/:slug` page — once any bio field is
  filled in; clearing all bios clears the slug.
- Creating an event auto-creates a matching `MediaFolder`, and renaming the event renames the folder.
  Deleting a folder keeps its media as "Unfiled".
- `EventStatus.Draft` is hidden from all public endpoints; `Past` is public.
- `SeoService` only manages titles for the static pages in its `PAGE_KEYS` map — event detail and admin set
  their own.
- `CmsPipe` and `LocalizedDatePipe` are `pure: false` on purpose (they react to language changes).
- `.claude/launch.json` hardcodes a Windows path from the original author's machine; it isn't used by the
  build and isn't portable.
- The repo has no CI workflows; dependency bumps arrive as Renovate PRs.

---

## Configuration & deployment

Config is standard ASP.NET (`appsettings.json` → env vars with `__` as the separator).

| Key | Env var in compose | Notes |
|---|---|---|
| `ConnectionStrings:Default` | `ConnectionStrings__Default` | SQLite file path |
| `Jwt:Key` / `Issuer` / `Audience` | `JWT_KEY` | ≥32 chars in production |
| `Admin:Email` / `Admin:Password` | `ADMIN_EMAIL` / `ADMIN_PASSWORD` | seeds the single admin user |
| `Cors:Origins:0` | `SITE_ORIGIN` | public site origin |
| `Email:Smtp*`, `Email:From*`, `Email:PublicBaseUrl` | `EMAIL_*` | Brevo SMTP; unset ⇒ log-only |
| `Storage:Endpoint/Region/Bucket/AccessKey/SecretKey` | `STORAGE_*` | Hetzner S3; unset ⇒ local disk |

`docker-compose.yml` runs the stack locally on `:8080`. `docker-compose.coolify.yml` is the production
descriptor (Traefik terminates TLS, no host ports). SQLite and uploads live in named volumes; `deploy/backup.sh`
snapshots both nightly with 14-day rotation.

Deployment specifics, brand palette, and the feature tour live in `README.md` — update it alongside this file
when either changes.
