# WebCashier - Secure Payment Application

A secure payment processing web application built with ASP.NET Core MVC.

## 🚀 Live Demo
- **Azure Deployment**: Coming soon...
- **Local Development**: `https://localhost:7068`

## 🆓 Azure Free Tier Deployment

This application is configured to deploy to Azure's free tier:

### Prerequisites
1. Azure account (free tier available)
2. Azure CLI installed
3. Git repository (for CI/CD)

### Deployment Steps

#### Option 1: Azure App Service (Recommended)
```bash
# Login to Azure
az login

# Create resource group
az group create --name WebCashier-RG --location "East US"

# Create app service plan (Free tier)
az appservice plan create --name WebCashier-Plan --resource-group WebCashier-RG --sku F1 --is-linux

# Create web app
az webapp create --resource-group WebCashier-RG --plan WebCashier-Plan --name webcashier-app --runtime "DOTNETCORE:8.0"

# Deploy from local
az webapp deploy --resource-group WebCashier-RG --name webcashier-app --src-path ./publish --type zip
```

#### Option 2: GitHub Actions CI/CD
1. Fork this repository
2. Set up Azure App Service
3. Download publish profile from Azure portal
4. Add `AZURE_WEBAPP_PUBLISH_PROFILE` to GitHub secrets
5. Push to main branch - automatic deployment

### Free Tier Limitations
- **App Service Free (F1)**: 1 GB storage, 165 MB RAM, 60 minutes/day compute
- **Custom domains**: Not available on free tier
- **SSL**: Free SSL certificate provided by Azure

## 🔧 Local Development

### Prerequisites
- .NET 8.0 SDK
- Any IDE (Visual Studio, VS Code, Rider)

### Running Locally
```bash
cd WebCashier
dotnet restore
dotnet run
```

- **HTTP**: `http://localhost:5182`
- **HTTPS**: `https://localhost:7068`

## 📱 Features

- ✅ Secure HTTPS payment form
- ✅ Multiple payment methods
- ✅ Real-time form validation
- ✅ Responsive design
- ✅ Security best practices
- ✅ Azure-ready configuration

## 🛡️ Security

- HTTPS/TLS encryption
- Anti-forgery tokens
- Input validation
- Secure headers
- Production-ready configuration

## 📄 License

MIT License - feel free to use for your projects!
