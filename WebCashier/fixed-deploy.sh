#!/bin/bash

# Robust Azure Deployment Script with Error Handling
set -e  # Exit on any error

echo "🔧 Azure CLI Fix & Deploy Script"
echo "================================="

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo -e "${GREEN}✅ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠️  $1${NC}"
}

print_error() {
    echo -e "${RED}❌ $1${NC}"
}

# Configuration
RESOURCE_GROUP="WebCashier-RG"
LOCATION="East US"
APP_SERVICE_PLAN="WebCashier-Plan"
WEB_APP_NAME="webcashier-fixed-$(date +%M%S)"
RUNTIME="DOTNETCORE:8.0"

echo ""
echo "🎯 Deployment Configuration:"
echo "   Resource Group: $RESOURCE_GROUP"
echo "   Location: $LOCATION"
echo "   App Name: $WEB_APP_NAME"
echo "   Runtime: $RUNTIME"
echo ""

# Test Azure CLI
print_status "Testing Azure CLI connection..."
if ! az account show --query name --output tsv > /dev/null 2>&1; then
    print_error "Azure CLI not logged in. Please run 'az login' first."
    exit 1
fi
print_status "Azure CLI connection OK"

# Check if resource group exists, create if not
print_status "Checking resource group..."
if ! az group show --name $RESOURCE_GROUP > /dev/null 2>&1; then
    print_warning "Creating resource group $RESOURCE_GROUP..."
    az group create --name $RESOURCE_GROUP --location "$LOCATION" --output none
    print_status "Resource group created"
else
    print_status "Resource group exists"
fi

# Check if app service plan exists, create if not
print_status "Checking App Service plan..."
if ! az appservice plan show --name $APP_SERVICE_PLAN --resource-group $RESOURCE_GROUP > /dev/null 2>&1; then
    print_warning "Creating App Service plan (Free tier)..."
    az appservice plan create \
        --name $APP_SERVICE_PLAN \
        --resource-group $RESOURCE_GROUP \
        --sku F1 \
        --is-linux \
        --output none
    print_status "App Service plan created"
else
    print_status "App Service plan exists"
fi

# Create web app
print_status "Creating web app..."
az webapp create \
    --resource-group $RESOURCE_GROUP \
    --plan $APP_SERVICE_PLAN \
    --name $WEB_APP_NAME \
    --runtime "$RUNTIME" \
    --output none

print_status "Web app created: $WEB_APP_NAME"

# Configure app settings
print_status "Configuring app settings..."
az webapp config appsettings set \
    --resource-group $RESOURCE_GROUP \
    --name $WEB_APP_NAME \
    --settings ASPNETCORE_ENVIRONMENT=Production \
    --output none

# Enable HTTPS only
print_status "Enabling HTTPS only..."
az webapp update \
    --resource-group $RESOURCE_GROUP \
    --name $WEB_APP_NAME \
    --https-only true \
    --output none

# Build and package application
print_status "Building application..."
dotnet publish -c Release -o ./deploy-dist --verbosity quiet

print_status "Creating deployment package..."
cd deploy-dist
zip -r ../webapp.zip . > /dev/null 2>&1
cd ..

# Deploy application
print_status "Deploying to Azure (this may take 2-3 minutes)..."
az webapp deploy \
    --resource-group $RESOURCE_GROUP \
    --name $WEB_APP_NAME \
    --src-path webapp.zip \
    --type zip \
    --output none

# Cleanup
rm -f webapp.zip
rm -rf deploy-dist

echo ""
echo "🎉 Deployment completed successfully!"
echo ""
echo "🌐 Your application URLs:"
echo "   Home: https://$WEB_APP_NAME.azurewebsites.net"
echo "   Payment: https://$WEB_APP_NAME.azurewebsites.net/Payment"
echo ""
echo "⏱️  Note: It may take 1-2 minutes for the app to start up"
echo ""
echo "🔧 Management commands:"
echo "   View logs: az webapp log tail --resource-group $RESOURCE_GROUP --name $WEB_APP_NAME"
echo "   Restart: az webapp restart --resource-group $RESOURCE_GROUP --name $WEB_APP_NAME"
echo "   Delete: az group delete --name $RESOURCE_GROUP --yes"
echo ""
print_status "Phase 2 deployment complete! 🚀"
