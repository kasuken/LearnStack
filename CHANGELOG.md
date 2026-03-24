# Changelog

All notable changes to LearnStack will be documented in this file.

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