# 📚 LearnStack Documentation Hub

Welcome to the LearnStack documentation and automation directory!

## 📖 Documentation Files

### 🚀 Deployment & Operations
- **[DEPLOYMENT.md](DEPLOYMENT.md)** - Complete Azure App Service deployment guide
- **[RELEASES.md](RELEASES.md)** - Versioning, releases, and semantic versioning guide
- **[PIPELINE.md](PIPELINE.md)** - CI/CD pipeline architecture and visual workflows
- **[QUICK-REFERENCE.md](QUICK-REFERENCE.md)** - Quick reference card for common tasks

### 📝 Project Documentation
- **[../CHANGELOG.md](../CHANGELOG.md)** - Version history and notable changes

## 🔧 GitHub Actions Workflows

### Workflow Files (`.github/workflows/`)

#### `azure-app-service.yml`
Automated CI/CD pipeline for Azure deployment with semantic versioning.

**Triggers:**
- Automatic: Push to `main` branch
- Manual: Workflow dispatch with version bump selection

**Features:**
- ✅ Builds .NET 10 Blazor application
- 🧪 Runs automated tests
- 🚀 Deploys to Azure App Service
- 🏷️ Creates semantic versioned releases
- 📦 Publishes GitHub releases with auto-generated notes

**Configuration Required:**
- `AZURE_WEBAPP_PUBLISH_PROFILE` secret
- `AZURE_WEBAPP_NAME` environment variable

## 🎯 Quick Start

### First Time Setup

1. **Read the deployment guide:**
   ```bash
   cat .github/DEPLOYMENT.md
   ```

2. **Configure Azure:**
   - Create App Service
   - Download publish profile
   - Add to GitHub secrets

3. **Deploy:**
   ```bash
   git push origin main
   ```

### Daily Usage

Need quick help? Check the [Quick Reference Card](QUICK-REFERENCE.md)!

```bash
# Deploy with automatic versioning
git commit -m "feat: new feature"
git push origin main

# Manual deployment with version control
gh workflow run azure-app-service.yml -f version_bump=minor
```

## 📊 Documentation Map

```
LearnStack/
├── README.md                          # Main project README
├── CHANGELOG.md                       # Version history
└── .github/
    ├── README.md                      # This file
    ├── DEPLOYMENT.md                  # Azure deployment guide
    ├── RELEASES.md                    # Versioning & releases
    ├── PIPELINE.md                    # CI/CD architecture
    ├── QUICK-REFERENCE.md            # Quick reference card
    ├── instructions/
    │   └── copilot.instructions.md   # Copilot customization
    └── workflows/
        └── azure-app-service.yml     # Main CI/CD workflow
```

## 🎨 Visual Guides

### Deployment Flow
See [PIPELINE.md](PIPELINE.md) for visual diagrams of:
- Complete CI/CD pipeline flowchart
- Version decision tree
- Release timeline
- Environment flow
- Success metrics

### Version Strategy
See [RELEASES.md](RELEASES.md) for:
- Semantic versioning rules
- Release note templates
- Commit message conventions
- Best practices

## 🔗 Quick Links

| Documentation | Purpose |
|--------------|---------|
| [🚀 Deployment](DEPLOYMENT.md) | How to deploy to Azure |
| [🏷️ Releases](RELEASES.md) | Version management |
| [🔄 Pipeline](PIPELINE.md) | CI/CD architecture |
| [⚡ Quick Ref](QUICK-REFERENCE.md) | Common commands |
| [📝 Changelog](../CHANGELOG.md) | Version history |

## 📋 Common Tasks

### Deployment Tasks
```bash
# Standard deployment
git push origin main

# Force version bump
gh workflow run azure-app-service.yml -f version_bump=major

# Check deployment status
gh run list --workflow=azure-app-service.yml
```

### Version Management
```bash
# Current version
git describe --tags --abbrev=0

# All releases
gh release list

# View specific release
gh release view v1.0.0
```

### Troubleshooting
```bash
# View workflow logs
gh run view [RUN_ID]

# Check Azure logs
az webapp log tail --name your-app-name --resource-group your-rg
```

See [QUICK-REFERENCE.md](QUICK-REFERENCE.md) for complete command reference.

## 🛠️ Maintenance

### Regular Updates
- Review and update docs quarterly
- Keep workflow actions up to date
- Update examples with current best practices

### When to Update Each Doc

| File | Update When |
|------|-------------|
| DEPLOYMENT.md | Azure setup changes |
| RELEASES.md | Versioning strategy changes |
| PIPELINE.md | Workflow architecture changes |
| QUICK-REFERENCE.md | New common tasks added |
| CHANGELOG.md | Every release |

## 🤝 Contributing

### Documentation Improvements
Found an error or unclear section? Please:
1. Open an issue describing the problem
2. Or submit a PR with improvements
3. Or start a discussion with suggestions

### Adding New Workflows
When adding new GitHub Actions workflows:
1. Add the workflow file to `workflows/`
2. Document it in this README
3. Add usage instructions to QUICK-REFERENCE.md
4. Update PIPELINE.md if it changes architecture

## 📞 Support

### Documentation Support
- 📖 Read the relevant guide above
- 🔍 Search [existing issues](https://github.com/kasuken/LearnStack/issues)
- 💬 [Start a discussion](https://github.com/kasuken/LearnStack/discussions)
- 🐛 [Report doc issues](https://github.com/kasuken/LearnStack/issues/new)

### Deployment Support
- Check [DEPLOYMENT.md](DEPLOYMENT.md) troubleshooting section
- Review [PIPELINE.md](PIPELINE.md) for architecture
- See [QUICK-REFERENCE.md](QUICK-REFERENCE.md) for common fixes

## 🎓 Learning Resources

### GitHub Actions
- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [Workflow syntax](https://docs.github.com/en/actions/reference/workflow-syntax-for-github-actions)

### Azure App Service
- [Azure App Service docs](https://docs.microsoft.com/en-us/azure/app-service/)
- [Deploy .NET apps](https://docs.microsoft.com/en-us/azure/app-service/quickstart-dotnetcore)

### Semantic Versioning
- [SemVer Specification](https://semver.org/)
- [Conventional Commits](https://www.conventionalcommits.org/)

## 📊 Documentation Status

| Document | Status | Last Updated |
|----------|--------|--------------|
| DEPLOYMENT.md | ✅ Complete | 2026-02-21 |
| RELEASES.md | ✅ Complete | 2026-02-21 |
| PIPELINE.md | ✅ Complete | 2026-02-21 |
| QUICK-REFERENCE.md | ✅ Complete | 2026-03-11 |
| CHANGELOG.md | ✅ Complete | 2026-03-03 |

---

## 🌟 Tips

💡 **Bookmark these docs** - You'll reference them often!

💡 **Start with QUICK-REFERENCE** - It has the most common commands

💡 **Use the visual diagrams** - PIPELINE.md has helpful flowcharts

💡 **Follow conventional commits** - Makes releases easier

💡 **Test locally first** - Before pushing to main

---

*Documentation maintained by the LearnStack team*

**Questions?** Open an issue or start a discussion!
