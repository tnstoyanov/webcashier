# 🚀 Phase 2: Azure Deployment Guide

## Prerequisites

### 1. Azure Account Setup
- **Free Account**: Visit [azure.microsoft.com/free](https://azure.microsoft.com/free)
- **$200 Credit**: Get $200 free credit for 30 days
- **Always Free Services**: Many services remain free after trial

### 2. Complete Azure Login
```bash
az login
# Select your subscription when prompted
# Or set default: az account set --subscription "your-subscription-id"
```

## 🆓 Free Tier Services We'll Use

| Service | Free Tier Limits | Perfect For |
|---------|------------------|-------------|
| **App Service** | F1: 1GB storage, 60min/day | Our web app |
| **Azure SQL** | 32GB database | Future database needs |
| **Application Insights** | 1GB/month telemetry | Monitoring |
| **Key Vault** | 10,000 operations | Secrets management |

## 🚀 Quick Deployment Options

### Option A: One-Click Script
```bash
# After completing az login
cd /Users/tonystoyanov/Documents/cashier/WebCashier
./deploy-azure.sh
```

### Option B: Manual Steps
```bash
# 1. Create resource group
az group create --name WebCashier-RG --location "East US"

# 2. Create app service plan (FREE TIER)
az appservice plan create \
    --name WebCashier-Plan \
    --resource-group WebCashier-RG \
    --sku F1 \
    --is-linux

# 3. Create web app
az webapp create \
    --resource-group WebCashier-RG \
    --plan WebCashier-Plan \
    --name webcashier-yourname \
    --runtime "DOTNETCORE:8.0"

# 4. Deploy
dotnet publish -c Release -o ./publish
cd publish && zip -r ../deploy.zip . && cd ..
az webapp deploy \
    --resource-group WebCashier-RG \
    --name webcashier-yourname \
    --src-path deploy.zip \
    --type zip
```

### Option C: GitHub Actions (Recommended for CI/CD)
1. Push code to GitHub
2. Set up App Service in Azure
3. Download publish profile
4. Add as GitHub secret
5. Auto-deploy on push

## 🎯 Expected Results

✅ **Your app will be live at**: `https://yourappname.azurewebsites.net`
✅ **HTTPS enabled** by default
✅ **Payment form** accessible at `/Payment`
✅ **Free SSL certificate** included
✅ **Custom domain** (paid plans only)

## 💰 Cost Breakdown

- **Development**: 100% FREE
- **Production (Light)**: FREE (with daily limits)
- **Production (Heavy)**: $5-10/month for Basic tier

## 🔧 Post-Deployment

### Monitor Your App
```bash
# View logs
az webapp log tail --resource-group WebCashier-RG --name yourappname

# App insights
az monitor app-insights component show --app yourappname
```

### Scale Up When Ready
```bash
# Upgrade to paid tier for unlimited usage
az appservice plan update --name WebCashier-Plan --resource-group WebCashier-RG --sku B1
```

## 🆘 Troubleshooting

### Common Issues:
1. **App name taken**: Add timestamp or your name
2. **Region unavailable**: Try "West US" or "Central US"
3. **Quota exceeded**: Check Azure portal for limits

### Support Resources:
- [Azure Free Account FAQ](https://azure.microsoft.com/free/free-account-faq/)
- [App Service Documentation](https://docs.microsoft.com/azure/app-service/)
- [Pricing Calculator](https://azure.microsoft.com/pricing/calculator/)

---

**Ready to deploy?** Complete the `az login` process and run the deployment script!
