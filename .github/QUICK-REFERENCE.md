# 🚀 Quick Reference Card

## Common Commands

### Git & Deployment

```bash
# Deploy with automatic patch version bump
git add .
git commit -m "fix: resolve login issue"
git push origin main

# Deploy with new feature (suggest minor bump)
git commit -m "feat: add export functionality"
git push origin main

# Breaking change (suggest major bump - use manual workflow)
git commit -m "feat!: redesign authentication system

BREAKING CHANGE: New auth requires users to re-login"
```

### Version Management

```bash
# Check current version
git describe --tags --abbrev=0

# List all versions
git tag -l

# View specific release
gh release view v1.0.0

# List all releases
gh release list

# Create manual tag (if needed)
git tag v1.0.0
git push origin v1.0.0
```

### GitHub CLI Workflow

```bash
# Trigger deployment with patch bump
gh workflow run azure-app-service.yml -f version_bump=patch

# Trigger deployment with minor bump
gh workflow run azure-app-service.yml -f version_bump=minor

# Trigger deployment with major bump
gh workflow run azure-app-service.yml -f version_bump=major

# View workflow runs
gh run list

# Watch current run
gh run watch
```

## Semantic Versioning Quick Guide

| Type | Format | When to Use | Example |
|------|--------|-------------|---------|
| 🟢 **Patch** | `v1.0.X` | Bug fixes, small improvements | v1.0.0 → v1.0.1 |
| 🔵 **Minor** | `v1.X.0` | New features (backward compatible) | v1.0.1 → v1.1.0 |
| 🔴 **Major** | `vX.0.0` | Breaking changes | v1.1.0 → v2.0.0 |

## Commit Message Conventions

```bash
# Feature
git commit -m "feat: add dark mode"
git commit -m "feat(auth): implement OAuth"

# Bug fix
git commit -m "fix: resolve login redirect"
git commit -m "fix(ui): correct button alignment"

# Performance
git commit -m "perf: optimize database queries"

# Breaking change
git commit -m "feat!: change API structure

BREAKING CHANGE: Endpoints now use /api/v2/"

# Other types
docs:     # Documentation only changes
style:    # Code style (formatting, missing semi colons, etc)
refactor: # Code change that neither fixes a bug nor adds a feature
test:     # Adding missing tests
chore:    # Changes to build process or auxiliary tools
```

## Deployment Checklist

### First-Time Setup
- [ ] Create Azure App Service (.NET 10)
- [ ] Create Azure SQL Database
- [ ] Download publish profile
- [ ] Add `AZURE_WEBAPP_PUBLISH_PROFILE` to GitHub Secrets
- [ ] Update `AZURE_WEBAPP_NAME` in workflow file
- [ ] Configure connection string in Azure
- [ ] Push to main branch
- [ ] Verify deployment in GitHub Actions
- [ ] Check app is live on Azure

### Regular Deployment
- [ ] Write code and tests
- [ ] Commit with conventional message
- [ ] Push to main branch
- [ ] Monitor GitHub Actions
- [ ] Verify release created
- [ ] Test live application

## Troubleshooting

### Issue: Deployment fails
```bash
# Check workflow logs
gh run list --workflow=azure-app-service.yml
gh run view [RUN_ID]

# Check Azure logs
az webapp log tail --name your-app-name --resource-group your-rg
```

### Issue: Version not incrementing
```bash
# Check current tags
git tag -l

# Create initial tag if none exist
git tag v1.0.0
git push origin v1.0.0

# Verify tag on remote
git ls-remote --tags origin
```

### Issue: Release not created
```bash
# Check workflow permissions
# Go to: Settings → Actions → General → Workflow permissions
# Ensure "Read and write permissions" is enabled

# Or check workflow file has:
# permissions:
#   contents: write
```

### Issue: Database connection fails
```powershell
# Test connection string locally
dotnet ef database update

# Check Azure firewall rules
az sql server firewall-rule list --resource-group your-rg --server your-server

# Allow Azure services
az sql server firewall-rule create \
  --resource-group your-rg \
  --server your-server \
  --name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0
```

## Environment Variables

### Local Development (.env or appsettings.Development.json)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=LearnStackDb;..."
  }
}
```

### Azure App Service (Configuration → Application Settings)
```
ASPNETCORE_ENVIRONMENT = Production
WEBSITE_TIME_ZONE = UTC
```

### Azure App Service (Configuration → Connection Strings)
```
DefaultConnection = Server=tcp:your-server.database.windows.net,1433;...
Type = SQLServer
```

## Useful Links

| Resource | URL |
|----------|-----|
| 🏠 **Repository** | https://github.com/kasuken/LearnStack |
| 📦 **Releases** | https://github.com/kasuken/LearnStack/releases |
| 🔧 **Actions** | https://github.com/kasuken/LearnStack/actions |
| 🐛 **Issues** | https://github.com/kasuken/LearnStack/issues |
| 📖 **Deployment Guide** | [DEPLOYMENT.md](.github/DEPLOYMENT.md) |
| 🏷️ **Release Guide** | [RELEASES.md](.github/RELEASES.md) |
| 🔄 **Pipeline Docs** | [PIPELINE.md](.github/PIPELINE.md) |
| 📝 **Changelog** | [CHANGELOG.md](../CHANGELOG.md) |

## Azure Portal Quick Links

```bash
# Open Azure Portal
open https://portal.azure.com

# Open your App Service
open https://portal.azure.com/#resource/subscriptions/{subscription-id}/resourceGroups/{rg}/providers/Microsoft.Web/sites/{app-name}

# View deployment logs
open https://{app-name}.scm.azurewebsites.net/api/deployments
```

## Status Checks

### GitHub Actions Status Badge
```markdown
![Deploy Status](https://github.com/kasuken/LearnStack/actions/workflows/azure-app-service.yml/badge.svg)
```

### Check Live App
```bash
curl https://learnstack-prod-001.azurewebsites.net/health
```

### Check Latest Release
```bash
curl -s https://api.github.com/repos/kasuken/LearnStack/releases/latest | jq .tag_name
```

## Performance Tips

### Local Development
```bash
# Use watch mode for hot reload
dotnet watch run --project LearnStack/LearnStack.csproj

# Build in Release mode
dotnet build -c Release
```

### Azure Optimization
- Enable **Always On** for production
- Use **Standard** or **Premium** tier for better performance
- Enable **Application Insights** for monitoring
- Configure **Autoscaling** based on metrics

## Maintenance Schedule

### Daily
- ✅ Monitor GitHub Actions for failed runs
- ✅ Check Application Insights for errors

### Weekly
- 🔄 Review open issues and PRs
- 📊 Check application performance metrics
- 🔐 Review security alerts

### Monthly
- 📦 Update NuGet packages
- 🔍 Review and update dependencies
- 📝 Update documentation
- 🧹 Clean up old releases (keep last 10-20)

---

## Need Help?

- 📖 Read the [full documentation](.github/)
- 🐛 [Open an issue](https://github.com/kasuken/LearnStack/issues/new)
- 💬 [Start a discussion](https://github.com/kasuken/LearnStack/discussions)
- 📧 Contact: @kasuken

---

*Last Updated: February 21, 2026*
