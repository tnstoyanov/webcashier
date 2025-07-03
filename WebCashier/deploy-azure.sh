#!/bin/bash

# Azure Deployment Script for WebCashier
# Phase 2: Deploy to Azure Free Tier

echo "🚀 Starting Azure deployment for WebCashier..."

# Configuration variables
RESOURCE_GROUP="WebCashier-RG"
LOCATION="East US"
APP_SERVICE_PLAN="WebCashier-Plan"
WEB_APP_NAME="webcashier-$(date +%s)"  # Unique name with timestamp
RUNTIME="DOTNETCORE:8.0"

echo "📋 Deployment Configuration:"
echo "   Resource Group: $RESOURCE_GROUP"
echo "   Location: $LOCATION"
echo "   App Name: $WEB_APP_NAME"
echo "   Runtime: $RUNTIME"
echo ""

# Check if logged in
echo "🔐 Checking Azure login status..."
az account show > /dev/null 2>&1
if [ $? -ne 0 ]; then
    echo "❌ Not logged in to Azure. Please run 'az login' first."
    exit 1
fi

echo "✅ Azure login confirmed"

# Create resource group
echo "🏗️  Creating resource group..."
az group create \
    --name $RESOURCE_GROUP \
    --location "$LOCATION" \
    --output table

# Create App Service plan (Free tier)
echo "📦 Creating App Service plan (Free tier)..."
az appservice plan create \
    --name $APP_SERVICE_PLAN \
    --resource-group $RESOURCE_GROUP \
    --sku F1 \
    --is-linux \
    --output table

# Create web app
echo "🌐 Creating web app..."
az webapp create \
    --resource-group $RESOURCE_GROUP \
    --plan $APP_SERVICE_PLAN \
    --name $WEB_APP_NAME \
    --runtime "$RUNTIME" \
    --output table

# Configure app settings for production
echo "⚙️  Configuring app settings..."
az webapp config appsettings set \
    --resource-group $RESOURCE_GROUP \
    --name $WEB_APP_NAME \
    --settings ASPNETCORE_ENVIRONMENT=Production \
    --output table

# Enable HTTPS only
echo "🔒 Enabling HTTPS only..."
az webapp update \
    --resource-group $RESOURCE_GROUP \
    --name $WEB_APP_NAME \
    --https-only true \
    --output table

# Deploy the application
echo "🚀 Deploying application..."
cd /Users/tonystoyanov/Documents/cashier/WebCashier
dotnet publish -c Release -o ./publish

# Create deployment package
echo "📦 Creating deployment package..."
cd publish
zip -r ../deploy.zip .
cd ..

# Deploy to Azure
echo "☁️  Uploading to Azure..."
az webapp deploy \
    --resource-group $RESOURCE_GROUP \
    --name $WEB_APP_NAME \
    --src-path deploy.zip \
    --type zip

# Get the URL
echo ""
echo "🎉 Deployment completed!"
echo "🌍 Your application is available at:"
echo "   https://$WEB_APP_NAME.azurewebsites.net"
echo "   https://$WEB_APP_NAME.azurewebsites.net/Payment"
echo ""
echo "💰 Free tier limitations:"
echo "   - 60 minutes/day compute time"
echo "   - 1 GB storage"
echo "   - 165 MB RAM"
echo ""
echo "🔧 Management commands:"
echo "   View logs: az webapp log tail --resource-group $RESOURCE_GROUP --name $WEB_APP_NAME"
echo "   Restart app: az webapp restart --resource-group $RESOURCE_GROUP --name $WEB_APP_NAME"
echo "   Delete resources: az group delete --name $RESOURCE_GROUP"
