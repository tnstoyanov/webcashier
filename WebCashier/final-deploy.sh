#!/bin/bash

# Final Azure Deployment Script for WebCashier
echo "Starting Azure deployment for WebCashier..."

# Variables
RESOURCE_GROUP="WebCashier-RG"
APP_NAME="webcashier-$(date +%s)"  # Unique name with timestamp
LOCATION="eastus"
PLAN_NAME="WebCashier-Plan"

# Check if logged in
echo "Checking Azure login status..."
az account show > /dev/null 2>&1
if [ $? -ne 0 ]; then
    echo "Not logged in to Azure. Please run 'az login' first."
    exit 1
fi

echo "✓ Azure CLI is authenticated"

# Create resource group (if it doesn't exist)
echo "Creating resource group: $RESOURCE_GROUP"
az group create --name $RESOURCE_GROUP --location $LOCATION

# Create App Service plan (Free tier)
echo "Creating App Service plan: $PLAN_NAME"
az appservice plan create \
    --name $PLAN_NAME \
    --resource-group $RESOURCE_GROUP \
    --sku F1 \
    --is-linux false

if [ $? -ne 0 ]; then
    echo "❌ Failed to create App Service plan"
    exit 1
fi

echo "✓ App Service plan created successfully"

# Create web app
echo "Creating web app: $APP_NAME"
az webapp create \
    --name $APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --plan $PLAN_NAME \
    --runtime "DOTNET|8.0"

if [ $? -ne 0 ]; then
    echo "❌ Failed to create web app"
    exit 1
fi

echo "✓ Web app created successfully"

# Build the application
echo "Building the application..."
dotnet publish -c Release -o ./publish

if [ $? -ne 0 ]; then
    echo "❌ Failed to build the application"
    exit 1
fi

echo "✓ Application built successfully"

# Create zip file for deployment
echo "Creating deployment package..."
cd publish
zip -r ../deploy.zip .
cd ..

if [ $? -ne 0 ]; then
    echo "❌ Failed to create deployment package"
    exit 1
fi

echo "✓ Deployment package created"

# Deploy to Azure
echo "Deploying to Azure..."
az webapp deploy \
    --resource-group $RESOURCE_GROUP \
    --name $APP_NAME \
    --src-path deploy.zip \
    --type zip

if [ $? -ne 0 ]; then
    echo "❌ Failed to deploy to Azure"
    exit 1
fi

echo "✅ Deployment completed successfully!"

# Get the app URL
APP_URL="https://$APP_NAME.azurewebsites.net"
echo ""
echo "🎉 Your app is live at: $APP_URL"
echo ""
echo "You can also manage your app in the Azure portal:"
echo "https://portal.azure.com/#@/resource/subscriptions/$(az account show --query id -o tsv)/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Web/sites/$APP_NAME"

# Clean up
echo "Cleaning up temporary files..."
rm -f deploy.zip
rm -rf publish

echo ""
echo "Deployment summary:"
echo "- Resource Group: $RESOURCE_GROUP"
echo "- App Service Plan: $PLAN_NAME (Free tier)"
echo "- Web App: $APP_NAME"
echo "- URL: $APP_URL"
echo ""
echo "✅ Deployment completed successfully!"
