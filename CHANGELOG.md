# Changelog

All notable changes to LearnStack will be documented in this file.

---

## [Unreleased]

### Added
- Released as open source under the GNU AGPL v3.0 (`AGPL-3.0-only`), with a Contributor License Agreement for contributions
- "Source code" link in the account menu, configurable with `SourceCodeUrl` (AGPL section 13)
- Issue forms, CODEOWNERS, Dependabot configuration, `.editorconfig`, `.gitattributes` and third-party notices

### Changed
- Deployment, release, pipeline and quick-reference guides moved from `.github/` to `docs/`

### Removed
- Accidentally tracked local files (Claude worktrees, Rider user settings, build error log)

---

## [1.4.1] - 2026-09-29

### Fixed
- Billing on a Stripe account shared with another product (such as Brainy)
  - Stripe delivers every event to every endpoint, signed with that endpoint's own secret, so a valid signature alone never proved an event was LearnStack's; events belonging to another product are now acknowledged and ignored
  - Checkout completions identify the user only from LearnStack's own `learnstack_user_id` metadata, never from `client_reference_id` alone, which previously failed the `UserPlan` foreign key for another product's checkout and made Stripe retry the delivery
  - Subscription events are ignored unless they carry LearnStack metadata or a configured LearnStack price, so another product's subscription on the same customer can no longer downgrade a LearnStack user
  - Invoice events must belong to a LearnStack subscription (metadata snapshot, stored subscription id, or a LearnStack price line) before they set or clear a grace period
  - Account deletion cancels only LearnStack's own subscriptions, and the duplicate-checkout guard no longer counts another product's live subscription

---

## [1.4.0] - 2026-09-28

### Added
- Stripe billing with per-plan resource limits
  - Two-tier Starter/Pro plans; Starter caps non-archived learning resources at 20, Pro is unlimited
  - `IEntitlementService` as the single authority for granting or denying paid-only actions, enforced server-side in `LearningResourceService.CreateAsync` rather than only in the UI
  - `IBillingProvider` abstraction with `NullBillingProvider` as the safe default (`Billing:Provider = None`), so the app runs fully without Stripe configured
  - `StripeBillingProvider` (Stripe.net) selected via `Billing:Provider = Stripe`, with fail-fast configuration validation
  - Verified inbound webhooks applied idempotently through a `ProcessedWebhookEvent` ledger keyed on the provider event id, via `/api/webhooks/billing`
  - Plan & usage account page for viewing usage and starting checkout/portal sessions
- Per-user timezone support for relative dates and Pulse analytics
  - Nullable IANA `TimeZoneId` on `ApplicationUser`, editable from Account/Manage and auto-detected from the browser on first render
  - `UserTimeZoneHelper` and `PulseAnalyticsHelper` to resolve time zones and bucket weekly completions against the user's local Monday-aligned week
  - Fixes wrong "Today"/"Yesterday" labels, misaligned Pulse buckets, wrong active-day counts, and drifting resource age emoji for non-UTC users
- Romanian translation (`SharedResource.ro.resx`), covering all 577 shared UI, marketing, account, onboarding and Identity strings
- Mobile-responsive layout for authenticated pages
  - New `wwwroot/responsive.css` layer loaded after MudBlazor; desktop layout untouched and additive only
  - Compacted app bar, stacked page headers, reflowed resource rows, wrapping filter/pagination bars, near-fullscreen dialogs, 16px inputs (stops iOS zoom-on-focus) and 40px minimum tap targets
- Improved new user onboarding
- Marketing screenshots of the app

### Changed
- Marketing message broadened beyond AI learning across all six locales, including hero, features, CTA, footer, testimonial roles, SEO metadata and JSON-LD structured data on `/` and `/welcome`
- Friend-facing surfaces now render `DisplayName` instead of the raw email local-part, falling back to the local-part only when no display name is set
- Data Protection keys persisted to SQL via `PersistKeysToDbContext` in Production instead of the container filesystem, so restarts, deployments, slot swaps and scale-out no longer invalidate auth cookies and antiforgery tokens

### Fixed
- **New accounts were locked out after their first session.** `RequireConfirmedAccount` was enabled while registration signs users in directly without sending a confirmation email, so every account created since could never sign in again and reported only "Invalid email or password". Confirmation is now disabled until a real email sender is configured, and the login page surfaces an unconfirmed account as its own distinct message instead of a credential failure
- Foreign key conflict when deleting an account with linked resources; new `AccountDeletionService` removes all dependents and the user inside a single transaction
- OpenGraph fetcher hardened against redirect SSRF and oversized responses: manual redirect following with per-hop revalidation (max 5 hops), ports restricted to 80/443, 1 MB body cap, and a `ConnectCallback` that closes the DNS-rebinding TOCTOU gap
- Bumped `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` to 10.0.12 to avoid the critical cookie-forging vulnerability in 10.0.0–10.0.6 ([GHSA-9mv3-2cwr-p262](https://github.com/advisories/GHSA-9mv3-2cwr-p262))
- Resources and Pulse pages loaded twice (once while prerendering, again on circuit connect); sign-in now routes straight to `/resources`
- Oversized Blazor circuit state: resource collections no longer transferred through persistent component state, so thumbnail data cannot exceed the SignalR startup message limit
- Missing user display name migration
- Empty fourth stat card on Content Ideas, and email overlapping the last-active date on the account profile card

---

## [1.3.0]

### Added
- Installable PWA support (manifest and icons only; no offline/service worker)
  - Web app manifest with standalone display, theme colors, and 192/512 any + maskable icons
  - Favicons and Apple touch icon wired in the document head for Add to Home Screen

---

## [1.2.0] - 2026-03-28

### Added
- Aria-labels on interactive buttons across `Login`, `Register`, `ContentIdeas`, `Friends`, `Resources`, and `ResourceCard` components for improved screen-reader accessibility
- Authentication middleware registered in `Program.cs`

### Changed
- `OpenGraphService` refactored with stricter URL validation and structured logging
- `ContentIdeaService` and `LearningResourceService` cleaned up for consistency
- Resource files (`SharedResource.*.resx`) updated and reorganized
- `.gitignore` cleaned up

### Fixed
- `InputModel` properties in `Login` and `Register` pages now initialized to avoid potential null-reference exceptions

---

## [1.1.0] - 2026-03-25

### Added
- Onboarding experience for first-time users
  - `OnboardingDialog` component built with the MudBlazor `MudStepper` horizontal stepper
  - 5-step tour covering: Welcome, Resource Library, Friends, Shared Collections, and Content Ideas
  - `OnboardingCompletedAt` property on `ApplicationUser` to track whether onboarding has been completed
  - Database migration (`AddOnboardingCompletedAt`) to add the new column to the identity schema
  - `MainLayout` updated to show the onboarding dialog after the ToS check on first login
  - Localized onboarding strings added to all supported language resource files (en, de, es, fr, it)

---

## [1.0.0] - 2026-03-25

### Added
- Friends management system
  - `FriendInvitation` and `LearnerFriendship` data models
  - `Friends` page with UI for viewing and managing friend connections
  - Invite link generation and acceptance flow via the new `FriendAccept` page
  - `FriendResources` page for browsing a friend's public learning resources
  - `IsPublic` property on `LearningResource` to optionally share resources with friends
  - Database migration (`AddFriendsAndPublicResources`) to support friend invitations and friendships
  - `FriendshipService` and `IFriendshipService` for all friendship operations registered in DI
  - Localized friendship strings added to all supported language resource files (en, de, es, fr, it)

### Changed
- All data services (`ContentIdeaService`, `FriendshipService`, `LearningResourceService`, `SharedResourceGroupService`) refactored to use `IDbContextFactory<ApplicationDbContext>` for better database connection lifecycle management and resource disposal

---

## [0.1.3] - 2026-03-24

### Fixed
- Unreadable text in data grids and dropdowns in dark mode (`ContentIdeas`, `Resources` pages, `ResourceCard`, `ResourceForm`)
- Dark mode CSS variables properly mapped to MudBlazor palette tokens; dark class toggling corrected; hardcoded colors replaced with theme-aware values

---

## [0.1.2] - 2026-03-24

### Added
- `TosDialog` component for displaying and accepting the Terms of Service
- `TosAcceptedAt` property on `ApplicationUser` to track when each user accepted the ToS
- Database migration (`AddTosAcceptedAt`) to add the new column to the identity schema
- `tos.html` static page with full Terms of Service content
- Localized Terms of Service strings added to all supported language resource files (en, de, es, fr, it)
- `MainLayout` updated to show the ToS dialog on first login until acceptance is recorded
- GitHub Copilot agent file (`CSharpExpert.agent.md`) and frontend-design skill for development tooling

---

## [0.1.1] - 2026-03-24

### Added
- Localized `NotFound` page content in `Routes.razor` (replaced static text with localization strings)
- Localized all form labels and helper texts in `ContentIdeaForm`, `ResourceForm`, and `SharedGroupForm`
- Localized language names in `LanguageSelector`
- Localized theme option names in `ThemeSelector`
- Added new localization strings to `SharedResource.resx` for all UI elements introduced in this release
- GitHub Copilot agent and skill files (`.github/agents/`, `.github/skills/`) for development tooling

### Changed
- `CultureController` now normalizes culture input and defaults to English when an unsupported culture is provided
- `Program.cs` updated to configure request localization options with explicit default and supported cultures

---

## [0.1.0] - 2026-03-18

### Added
- `ThemeSelector` component for user-controlled theme switching
- Dark mode support in `MainLayout` with theme preference persisted to local storage

### Changed
- `MainLayout` refactored to handle dynamic theme changes at runtime

---

## [0.0.9] - 2026-03-06

### Added
- Full multi-language (i18n) support for English, German (de), Spanish (es), French (fr), and Italian (it)
- `SharedResource` resource files with translations for all supported languages
- `CultureMiddleware` and `CultureController` for culture detection and switching
- `LanguageSelector` component for in-app language selection
- Localized all authentication and account management pages (Login, Register, Manage, Passkeys, 2FA, etc.)
- Localized marketing pages (`Home`, `LandingPage`)
- Localized application pages: Resources, ContentIdeas, SharedGroups, SharedView, Error, NotFound
- Localized shared components: `ResourceCard`, `ResourceForm`, `ContentIdeaCard`, `ContentIdeaForm`, `SharedGroupForm`

### Changed
- `App.razor` and `Routes.razor` updated to support culture-aware routing
- `Program.cs` updated to register localization services and middleware
- Removed invisible characters from `using` directives in `ContentIdeaForm` and `ResourceForm`

---

## [0.0.8] - 2026-03-03

### Added
- Archive feature for learning resources (archive/unarchive, filter, and display)
- New "IsArchived" property and migration for LearningResource
- Improved tab functionality and resource loading on Resources page
- Enhanced account management layouts and navigation (AccountLayout, ManageLayout, ManageNavMenu)
- More robust statistics and export options for resources

### Changed
- Modernized and unified UI for account and resource management
- Improved sidebar and top bar navigation for account settings
- Refined card, tab, and chip styling for consistency

### Fixed
- Various UI/UX bugs in resource and account management

## [0.0.5] - 2026-02-27

### Added
- New account settings shell with sticky top bar and sidebar navigation
- Shared, consistent design system for Account/Manage pages

### Changed
- Modernized UI across core pages, dialogs, and error states
- Replaced Bootstrap-based Account/Manage pages with custom layout and styles
- Unified form, card, and navigation styling for consistency
- Updated MudBlazor typography configuration for v8 compatibility

### Fixed
- Resource metadata button alignment in the add resource dialog
- Logout form missing antiforgery token and returnUrl binding
- Logout redirect validation for minimal API LocalRedirect
- Account navigation not working in SSR/static mode
- MudBlazor v8 typography type/name and value mismatches

## 2026-02-21

### Added
- 🎉 Initial release of LearnStack
- 📚 Learning resource management system
  - Add, edit, delete learning resources
  - Track URLs for blog posts, videos, podcasts, courses
  - Status tracking (To Learn, In Progress, Completed)
  - Priority levels (High, Medium, Low)
  - Tags and search functionality
  - Notes and key learnings capture
- 💡 Content idea planning system
  - Create and manage content ideas
  - Link ideas to source resources
  - Track idea status (Idea, In Progress, Published)
  - Content outlines and notes
- 🔐 User authentication with ASP.NET Identity
  - Email/password authentication
  - Passkey support
  - User registration and login
- 🎨 Modern UI with MudBlazor components
  - Responsive design
  - Card-based layouts
  - Intuitive navigation
- 🗄️ SQL Server database with EF Core
  - Migrations support
  - User-specific data isolation
- 🚀 Azure App Service deployment
  - Automated CI/CD pipeline
  - GitHub Actions workflow
- 🏷️ Semantic versioning with automated releases
  - Automatic version bumping
  - GitHub release generation
  - Release notes automation
