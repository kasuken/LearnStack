# 🏷️ Release & Versioning Guide

## Overview

LearnStack uses **Semantic Versioning** (SemVer) for all releases. Every successful deployment to Azure App Service automatically creates a GitHub release with an incremented version.

## Semantic Versioning Format

```
v{MAJOR}.{MINOR}.{PATCH}
```

### When to bump each number:

- **MAJOR** (`v2.0.0`): Breaking changes, major rewrites, incompatible API changes
- **MINOR** (`v1.1.0`): New features, enhancements (backward compatible)
- **PATCH** (`v1.0.1`): Bug fixes, small improvements, security patches

## Automatic Release Process

### 🔄 Automatic Workflow (Default)

When you push to `main` branch:

```bash
git add .
git commit -m "feat: add new feature"
git push origin main
```

The workflow automatically:
1. ✅ Calculates next **patch** version (e.g., `v1.0.0` → `v1.0.1`)
2. 🔨 Builds the application
3. 🧪 Runs tests
4. 🚀 Deploys to Azure App Service
5. 🏷️ Creates a Git tag
6. 📦 Creates a GitHub release with auto-generated notes

### 🎯 Manual Workflow (Custom Version Bump)

For more control over versioning:

1. Go to **Actions** → **Deploy to Azure App Service**
2. Click **Run workflow** button
3. Select **branch**: `main`
4. Choose **version bump type**:
   - `patch` - Bug fixes (v1.0.0 → v1.0.1)
   - `minor` - New features (v1.0.0 → v1.1.0)
   - `major` - Breaking changes (v1.0.0 → v2.0.0)
5. Click **Run workflow**

## Release Notes

Each release automatically includes:

- 📝 **Version number** and release name
- 📅 **Deployment timestamp** (UTC)
- 🔗 **Commit SHA** with link
- 📊 **Comparison link** (changes since last release)
- 🌐 **Live URL** of the deployed application
- 📋 **Auto-generated changelog** from commit messages

## Example Releases

### Patch Release (v1.0.1)
```
✅ Fix login bug
✅ Update dependencies
✅ Improve error messages
```

### Minor Release (v1.1.0)
```
✨ Add export functionality
✨ Add dark mode support
⚡ Improve performance
🐛 Fix minor bugs
```

### Major Release (v2.0.0)
```
💥 BREAKING: New authentication system
💥 BREAKING: Changed API endpoints
✨ Complete UI redesign
✨ Add mobile support
```

## Commit Message Convention

For better auto-generated release notes, use conventional commits:

```bash
# Features
git commit -m "feat: add user profile page"
git commit -m "feat(auth): implement OAuth login"

# Bug Fixes
git commit -m "fix: resolve login redirect issue"
git commit -m "fix(ui): correct button alignment"

# Performance
git commit -m "perf: optimize database queries"

# Documentation
git commit -m "docs: update API documentation"

# Chores
git commit -m "chore: update dependencies"
```

### Commit Types:
- `feat:` New feature → suggests **minor** bump
- `fix:` Bug fix → suggests **patch** bump
- `perf:` Performance improvement → **patch** bump
- `docs:` Documentation → **patch** bump
- `style:` Code style changes → **patch** bump
- `refactor:` Code refactoring → **patch** bump
- `test:` Adding tests → **patch** bump
- `chore:` Maintenance → **patch** bump
- `BREAKING CHANGE:` Breaking change → requires **major** bump

## Version History

View all releases at:
```
https://github.com/kasuken/LearnStack/releases
```

## Troubleshooting

### Release Creation Failed

**Problem**: GitHub release creation fails

**Solution**:
- Ensure the workflow has `contents: write` permission (already configured)
- Check that `GITHUB_TOKEN` has proper permissions
- Verify no duplicate tags exist

### Version Not Incrementing

**Problem**: Version stays the same

**Solution**:
- Ensure `fetch-depth: 0` is set in checkout (already configured)
- Check that tags are being pushed to the repository
- Manually create the first tag if none exist:
  ```bash
  git tag v1.0.0
  git push origin v1.0.0
  ```

### Creating First Release

If this is your first deployment and no tags exist:

```bash
# Create initial tag
git tag v1.0.0
git push origin v1.0.0

# Next deployment will auto-increment to v1.0.1
```

## Best Practices

### 1. **Consistent Commit Messages**
Use conventional commits for better auto-generated changelogs

### 2. **Test Before Merge**
Always test in a development environment before merging to `main`

### 3. **Plan Major Releases**
For breaking changes, use manual workflow with `major` bump

### 4. **Document Changes**
Update `version.json` with notable changes for reference

### 5. **Release Cadence**
- **Patches**: As needed (bug fixes)
- **Minor**: Weekly or bi-weekly (new features)
- **Major**: Quarterly or when necessary (breaking changes)

## Advanced: Pre-releases

To create pre-release versions (beta, alpha):

1. Create a separate branch (e.g., `develop`)
2. Modify workflow to detect branch and add suffix
3. Example: `v1.1.0-beta.1`, `v2.0.0-alpha.3`

## Questions?

For questions or issues with versioning and releases:
- Open an issue: [Create Issue](https://github.com/kasuken/LearnStack/issues/new)
- Check existing releases: [View Releases](https://github.com/kasuken/LearnStack/releases)

---

*Automated with ❤️ using GitHub Actions*
