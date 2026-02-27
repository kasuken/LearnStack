# Changelog

All notable changes to LearnStack will be documented in this file.

---

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