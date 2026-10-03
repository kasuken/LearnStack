# 🔄 CI/CD Pipeline Overview

## Deployment & Release Flow

```mermaid
graph TB
    A[🚀 Push to main branch] --> B{GitHub Actions Triggered}
    B --> C[📥 Checkout Code]
    C --> D[🏷️ Calculate Next Version]
    D --> E[📦 Get Latest Tag]
    E --> F{First Release?}
    F -->|Yes| G[Start at v1.0.0]
    F -->|No| H[Increment Version]
    H --> I{Bump Type}
    I -->|Patch| J[v1.0.0 → v1.0.1]
    I -->|Minor| K[v1.0.0 → v1.1.0]
    I -->|Major| L[v1.0.0 → v2.0.0]
    G --> M[🔧 Setup .NET 10]
    J --> M
    K --> M
    L --> M
    M --> N[📦 Restore Dependencies]
    N --> O[🔨 Build Application]
    O --> P[🧪 Run Tests]
    P --> Q[📤 Publish App]
    Q --> R[☁️ Deploy to Azure]
    R --> S{Deployment Success?}
    S -->|No| T[❌ Workflow Failed]
    S -->|Yes| U[📝 Generate Release Notes]
    U --> V[🏷️ Create Git Tag]
    V --> W[📦 Create GitHub Release]
    W --> X[✅ Success!]
    X --> Y[🌐 Live on Azure]
    X --> Z[📋 Release Published]
    
    style A fill:#4CAF50
    style X fill:#4CAF50
    style T fill:#f44336
    style R fill:#2196F3
    style W fill:#FF9800
```

## Workflow Stages

### 1. 🎯 Trigger
- **Automatic**: Push to `main` branch
- **Manual**: Workflow dispatch with version bump selection

### 2. 🏷️ Versioning
```
Latest Tag: v1.2.3
Bump Type: patch
New Version: v1.2.4
```

### 3. 🔨 Build
```bash
dotnet restore → dotnet build → dotnet test → dotnet publish
```

### 4. 🚀 Deploy
```
Artifact → Azure App Service → Production Environment
```

### 5. 📦 Release
```
Create Tag → Generate Notes → Publish Release → Notify
```

## Version Decision Tree

```mermaid
graph LR
    A[Code Change] --> B{Change Type?}
    B -->|Bug Fix| C[Patch v1.0.X]
    B -->|New Feature| D[Minor v1.X.0]
    B -->|Breaking Change| E[Major vX.0.0]
    
    C --> F[Auto: Push to main]
    D --> G[Manual: Select 'minor']
    E --> H[Manual: Select 'major']
    
    F --> I[v1.0.1]
    G --> J[v1.1.0]
    H --> K[v2.0.0]
    
    style C fill:#90EE90
    style D fill:#87CEEB
    style E fill:#FFB6C1
```

## Timeline Example

```
Day 1: v1.0.0 - Initial Release
       └─ feat: Initial app with auth & resources
       
Day 2: v1.0.1 - Patch
       └─ fix: Login redirect issue
       
Day 5: v1.0.2 - Patch
       └─ fix: UI alignment bugs
       
Week 2: v1.1.0 - Minor
        └─ feat: Add export functionality
        └─ feat: Dark mode support
        
Week 3: v1.1.1 - Patch
        └─ fix: Export CSV formatting
        
Month 2: v2.0.0 - Major
         └─ BREAKING: New API structure
         └─ feat: Mobile responsive redesign
         └─ feat: Real-time collaboration
```

## Release Timeline

```mermaid
gantt
    title LearnStack Release Cycle
    dateFormat YYYY-MM-DD
    section Major Releases
    v1.0.0 Initial Release           :milestone, m1, 2026-02-21, 0d
    v2.0.0 Breaking Changes          :milestone, m2, 2026-05-21, 0d
    
    section Minor Releases
    v1.1.0 New Features              :milestone, m3, 2026-03-07, 0d
    v1.2.0 More Features             :milestone, m4, 2026-03-21, 0d
    
    section Patches
    Bug Fixes & Updates              :active, 2026-02-21, 90d
```

## Environment Flow

```mermaid
graph LR
    A[👨‍💻 Developer] -->|Push Code| B[GitHub]
    B -->|Trigger| C[GitHub Actions]
    C -->|Build & Test| D[Build Artifacts]
    D -->|Deploy| E[☁️ Azure App Service]
    E -->|Live| F[🌐 Production]
    C -->|Create| G[📦 GitHub Release]
    G -->|Notify| H[📧 Subscribers]
    
    style A fill:#4CAF50
    style E fill:#2196F3
    style F fill:#FF9800
    style G fill:#9C27B0
```

## Permissions & Secrets

```mermaid
graph TB
    A[GitHub Repository] --> B[Settings]
    B --> C[Secrets]
    C --> D[AZURE_WEBAPP_PUBLISH_PROFILE]
    C --> E[GITHUB_TOKEN]
    
    B --> F[Actions]
    F --> G[Permissions]
    G --> H[contents: write]
    G --> I[packages: write]
    
    D --> J[Azure Deployment]
    E --> K[Release Creation]
    H --> K
    
    style D fill:#2196F3
    style E fill:#4CAF50
    style H fill:#FF9800
```

## Success Metrics

After each deployment, you get:

| Metric | Description |
|--------|-------------|
| ✅ Build Status | Pass/Fail status of the build |
| ⏱️ Build Time | Total time from trigger to completion |
| 📦 Artifact Size | Size of the published application |
| 🏷️ Version Tag | New semantic version created |
| 🌐 Deployment URL | Live application endpoint |
| 📋 Release Notes | Auto-generated changelog |

## Monitoring

Track your deployments:

1. **GitHub Actions**: [Actions Tab](https://github.com/kasuken/LearnStack/actions)
2. **Releases**: [Releases Page](https://github.com/kasuken/LearnStack/releases)
3. **Azure Portal**: App Service → Deployment Center
4. **Application Insights**: Azure → App Service → Application Insights

---

## Quick Reference

### Manual Deployment Commands

```bash
# Trigger from Actions tab, or:
gh workflow run azure-app-service.yml -f version_bump=patch
gh workflow run azure-app-service.yml -f version_bump=minor
gh workflow run azure-app-service.yml -f version_bump=major
```

### Check Latest Version

```bash
git describe --tags --abbrev=0
```

### View All Releases

```bash
gh release list
```

---

*Powered by GitHub Actions · Deployed to Azure App Service*
