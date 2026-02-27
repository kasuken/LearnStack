# Changelog

All notable changes to LearnStack will be documented in this file.


---

## [1.0.0] - 2026-02-21

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

### Technical Stack
- .NET 10 with Blazor Server
- MudBlazor 8.15.0
- Entity Framework Core 10.0.3
- SQL Server
- ASP.NET Core Identity

---

## Version History Format

### Version Number Format
```
v{MAJOR}.{MINOR}.{PATCH}

Example: v1.2.3
- MAJOR: 1 (Breaking changes)
- MINOR: 2 (New features, backward compatible)
- PATCH: 3 (Bug fixes)
```

### Change Categories

Use these categories in your changelog entries:

- **Added** - New features
- **Changed** - Changes in existing functionality
- **Deprecated** - Soon-to-be removed features
- **Removed** - Removed features
- **Fixed** - Bug fixes
- **Security** - Security vulnerability fixes

### Example Entry

```markdown
## [1.1.0] - 2026-03-01

### Added
- Export resources to CSV format
- Dark mode support
- Search with keyboard shortcuts (Ctrl+K)

### Changed
- Improved resource card layout
- Updated navigation menu design

### Fixed
- Login redirect issue after registration
- Resource filter not persisting
- Tag autocomplete performance

### Security
- Updated authentication token expiration
```

---

## Future Releases

### Planned for v1.1.0
- 🔍 Advanced search and filtering
- 📊 Learning statistics and analytics
- 📤 Export/import functionality
- 🌙 Dark mode support
- ⌨️ Keyboard shortcuts

### Planned for v1.2.0
- 🔗 URL metadata extraction (Open Graph)
- 🖼️ Thumbnail previews
- 🏷️ Enhanced tag management
- 📱 Mobile-optimized views

### Planned for v2.0.0
- 🤝 Collaboration features
- 🔌 Browser extension
- 📱 Progressive Web App (PWA)
- 🤖 AI-powered content suggestions
- 🌐 Multi-language support

---

## How to Update This File

### For Maintainers

1. Update the `[Unreleased]` section as you work
2. When releasing, move items to a new version section
3. Update the version links at the bottom
4. Follow the format consistently

### Commit Message to Changelog Mapping

| Commit Type | Changelog Section |
|-------------|------------------|
| `feat:` | Added |
| `fix:` | Fixed |
| `perf:` | Changed |
| `refactor:` | Changed |
| `docs:` | Changed (Documentation) |
| `style:` | Changed |
| `test:` | Added/Changed |
| `chore:` | (Usually not in changelog) |
| `BREAKING CHANGE:` | Changed/Removed + note |

---

## Links

- [Repository](https://github.com/kasuken/LearnStack)
- [Releases](https://github.com/kasuken/LearnStack/releases)
- [Issues](https://github.com/kasuken/LearnStack/issues)
- [Deployment Guide](.github/DEPLOYMENT.md)
- [Release Guide](.github/RELEASES.md)

[Unreleased]: https://github.com/kasuken/LearnStack/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/kasuken/LearnStack/releases/tag/v1.0.0
