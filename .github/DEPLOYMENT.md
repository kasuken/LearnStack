# 🚀 Azure App Service Deployment Guide

This guide will help you deploy LearnStack to Azure App Service using GitHub Actions with automated versioning and releases.

## 📚 Documentation

- **[Release & Versioning Guide](RELEASES.md)** - Semantic versioning and release management
- **[CI/CD Pipeline Overview](PIPELINE.md)** - Visual workflow diagrams and architecture
- **[Changelog](../CHANGELOG.md)** - Project history and version notes

## Prerequisites

- Azure subscription
- Azure App Service created
- GitHub repository with admin access

## Setup Steps

### 1️⃣ Create Azure App Service

1. Go to [Azure Portal](https://portal.azure.com)
2. Create a new **App Service**:
   - **Runtime Stack**: .NET 10
   - **Operating System**: Linux (recommended) or Windows
   - **Region**: Choose your preferred region
   - **Pricing Tier**: B1 or higher recommended

### 2️⃣ Configure Database Connection

In your Azure App Service:

1. Go to **Configuration** → **Connection strings**
2. Add a new connection string:
   - **Name**: `DefaultConnection`
   - **Value**: Your SQL Database connection string
   - **Type**: SQLServer

Example:
```
Server=tcp:your-server.database.windows.net,1433;Initial Catalog=LearnStackDb;Persist Security Info=False;User ID=your-username;Password=your-password;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

### 3️⃣ Get Publish Profile

1. In your App Service, click **Get publish profile** (top menu)
2. Save the downloaded `.PublishSettings` file
3. Copy the entire contents of this file

### 4️⃣ Configure GitHub Secrets

1. Go to your GitHub repository
2. Navigate to **Settings** → **Secrets and variables** → **Actions**
3. Click **New repository secret**
4. Add the following secret:
   - **Name**: `AZURE_WEBAPP_PUBLISH_PROFILE`
   - **Value**: Paste the entire contents of the publish profile file

### 5️⃣ Update Workflow Configuration

Edit `.github/workflows/azure-app-service.yml`:

1. Replace `your-app-name` with your actual Azure App Service name:
   ```yaml
   AZURE_WEBAPP_NAME: 'your-actual-app-name'
   ```

### 6️⃣ Deploy!

1. Commit and push your changes to the `main` branch
2. GitHub Actions will automatically:
   - Build your application
   - Run tests
   - Publish the app
   - Deploy to Azure App Service

Monitor the deployment in the **Actions** tab of your GitHub repository.

## 🏷️ Semantic Versioning

### Automatic Version Bumping

Every successful deployment automatically creates a GitHub release with semantic versioning (MAJOR.MINOR.PATCH):

- **Patch** (default): Bug fixes and minor updates → `v1.0.0` → `v1.0.1`
- **Minor**: New features (backward compatible) → `v1.0.1` → `v1.1.0`
- **Major**: Breaking changes → `v1.1.0` → `v2.0.0`

### How It Works

1. **Automatic**: Push to `main` → auto-increments **patch** version
2. **Manual**: Choose version bump type when triggering manually

### Version History

View all releases in the **Releases** section of your GitHub repository.

Each release includes:
- 📦 Version number
- 📝 Deployment date and time
- 🔗 Commit SHA and comparison link
- 🌐 Live deployment URL

## Manual Deployment

You can also trigger deployment manually with custom version bumping:

1. Go to **Actions** tab in GitHub
2. Select **Deploy to Azure App Service** workflow
3. Click **Run workflow**
4. Select the branch
5. **Choose version bump type**: `major`, `minor`, or `patch`
6. Click **Run workflow**

## Troubleshooting

### Database Migrations

After first deployment, you may need to run migrations:

1. Go to Azure Portal → Your App Service
2. Open **SSH** or **Console**
3. Run: 
   ```bash
   dotnet ef database update --project /home/site/wwwroot/LearnStack.dll
   ```

Alternatively, enable automatic migrations in `Program.cs`.

### Connection String Issues

Ensure your Azure SQL Database:
- Allows Azure Services to connect
- Has correct firewall rules
- Connection string is properly configured in App Service

### Deployment Fails

Check:
- Publish profile is correctly added to GitHub secrets
- App Service name matches in the workflow file
- Sufficient App Service plan tier (B1 or higher)

## Environment Variables

Add these in Azure App Service **Configuration** → **Application settings**:

| Key | Value | Description |
|-----|-------|-------------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Sets production environment |
| `WEBSITE_TIME_ZONE` | `UTC` (or your timezone) | Sets application timezone |

## Stripe Billing

Billing is off by default (`Billing__Provider` = `None`), so the app runs without Stripe. To take payments for the Pro plan:

### In the Stripe Dashboard

1. **Product catalog**: create a *Pro* product with two recurring prices, one monthly and one yearly. Copy both price ids (`price_...`).
2. **Developers → Webhooks**: add an endpoint at `https://<your-app>/api/webhooks/billing` that listens for:
   - `checkout.session.completed`
   - `customer.subscription.created`
   - `customer.subscription.updated`
   - `customer.subscription.deleted`
   - `invoice.paid`
   - `invoice.payment_failed`

   Copy the endpoint's signing secret (`whsec_...`).
3. **Settings → Billing → Customer portal**: allow customers to update payment methods and cancel subscriptions, and add both Pro prices under *Subscriptions → Customers can switch plans* so users can move between monthly and yearly.
4. **Settings → Billing → Subscriptions and emails**: choose what happens after all payment retries fail. *Cancel the subscription* or *Mark as unpaid* both downgrade the user to Starter. With *Leave as-is*, the user loses Pro access 7 days after the last failed retry.

### In Azure App Service → Configuration → Application settings

Store the secrets as Key Vault references where possible.

| Key | Value |
|-----|-------|
| `Billing__Provider` | `Stripe` |
| `Billing__ApiKey` | Secret key (`sk_live_...`, or `sk_test_...` for a staging slot) |
| `Billing__WebhookSigningSecret` | Webhook signing secret (`whsec_...`) |
| `Billing__ProMonthlyPriceId` | Monthly Pro price id |
| `Billing__ProYearlyPriceId` | Yearly Pro price id |
| `Billing__CheckoutSuccessUrl` | `https://<your-app>/Account/Manage/Plan` |
| `Billing__CheckoutCancelUrl` | `https://<your-app>/Account/Manage/Plan` |
| `Billing__PortalReturnUrl` | `https://<your-app>/Account/Manage/Plan` |

The app adds `session_id={CHECKOUT_SESSION_ID}` to the success URL and `checkout=cancelled` to the cancel URL itself, so the Plan page can confirm an upgrade the moment the user returns. If any Stripe setting is missing, startup fails with an error that lists every missing key.

### Testing locally

```bash
stripe listen --forward-to http://localhost:5164/api/webhooks/billing
```

Use the `whsec_...` secret that `stripe listen` prints as `Billing:WebhookSigningSecret` in user secrets, then pay with the test card `4242 4242 4242 4242`.

## SSL/HTTPS

Azure App Service provides free SSL certificates:

1. Go to **Custom domains**
2. Add your domain
3. Enable **HTTPS Only** in **TLS/SSL settings**

## Monitoring

Enable Application Insights for monitoring:

1. Go to **Application Insights** in App Service
2. Turn on Application Insights
3. View logs, performance, and errors

## Cost Optimization

- Use **B1 Basic** tier for development/testing
- Scale to **S1 Standard** or higher for production
- Enable **Always On** for production apps
- Consider **App Service Plan** sharing across multiple apps

---

## 🎉 Success!

Your LearnStack application should now be live at:
`https://your-app-name.azurewebsites.net`

Need help? [Open an issue](https://github.com/kasuken/LearnStack/issues)
