#!/bin/bash
set -e

echo "🐧 Linux Azure Deployment Script"
echo "================================="

# Configuration
RESOURCE_GROUP="WebCashier-RG"
LOCATION="East US"
APP_NAME="webcashier-linux-$(date +%s)"
PLAN_NAME="WebCashier-Plan-Linux"

echo "🎯 Deployment Configuration:"
echo "   Resource Group: $RESOURCE_GROUP"
echo "   Location: $LOCATION"
echo "   App Name: $APP_NAME"
echo "   Runtime: DOTNETCORE:8.0 (Linux)"

# Test Azure CLI
echo "✅ Testing Azure CLI connection..."
if ! az account show >/dev/null 2>&1; then
    echo "❌ Not logged into Azure CLI"
    exit 1
fi
echo "✅ Azure CLI connection OK"

# Check resource group
echo "✅ Checking resource group..."
if ! az group show --name "$RESOURCE_GROUP" >/dev/null 2>&1; then
    echo "⚠️  Creating resource group..."
    az group create --name "$RESOURCE_GROUP" --location "$LOCATION"
fi
echo "✅ Resource group exists"

# Create Linux App Service plan (Free tier)
echo "⚠️  Creating Linux App Service plan (Free tier)..."
if ! az appservice plan show --name "$PLAN_NAME" --resource-group "$RESOURCE_GROUP" >/dev/null 2>&1; then
    az appservice plan create \
        --name "$PLAN_NAME" \
        --resource-group "$RESOURCE_GROUP" \
        --location "$LOCATION" \
        --is-linux \
        --sku F1
    echo "✅ App Service plan created"
else
    echo "✅ App Service plan already exists"
fi

# Create web app with Linux and .NET 8
echo "⚠️  Creating web app..."
az webapp create \
    --name "$APP_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --plan "$PLAN_NAME" \
    --runtime "DOTNETCORE:8.0"

echo "✅ Web app created: $APP_NAME"

# Configure app settings
echo "⚠️  Configuring app settings..."
az webapp config appsettings set \
    --name "$APP_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --settings \
        ASPNETCORE_ENVIRONMENT=Production \
        WEBSITES_PORT=5000

# Build and publish the app
echo "⚠️  Building application..."
dotnet publish -c Release -o ./publish

# Create deployment ZIP
echo "⚠️  Creating deployment package..."
cd ./publish
zip -r ../deploy.zip . >/dev/null 2>&1
cd ..

# Deploy using ZIP
echo "⚠️  Deploying application..."
az webapp deploy \
    --name "$APP_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --src-path ./deploy.zip \
    --type zip

# Get the URL
APP_URL="https://${APP_NAME}.azurewebsites.net"
echo ""
echo "🎉 Deployment completed!"
echo "📱 Your app is available at: $APP_URL"
echo "💳 Payment page: $APP_URL/Payment"
echo ""
echo "⏰ Note: It may take a few minutes for the app to start up completely."

# Test the deployment
echo "🔍 Testing deployment..."
sleep 30
if curl -s "$APP_URL" >/dev/null; then
    echo "✅ App is responding!"
else
    echo "⚠️  App may still be starting up. Please wait a few minutes and try: $APP_URL"
fi

echo "✅ Deployment script completed!"
