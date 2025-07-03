#!/bin/bash

echo "🚀 Super Simple Azure Deployment"
echo ""

# Basic settings
RG="WebCashier-RG"
PLAN="WebCashier-Plan"  
APP="webcashier-simple"
LOCATION="East US"

echo "Step 1: Creating App Service Plan..."
az appservice plan create \
    --name $PLAN \
    --resource-group $RG \
    --sku F1 \
    --location "$LOCATION" || echo "Plan might already exist"

echo ""
echo "Step 2: Creating Web App..."
az webapp create \
    --resource-group $RG \
    --plan $PLAN \
    --name $APP \
    --runtime "DOTNETCORE:8.0" || echo "App might already exist"

echo ""
echo "Step 3: Building application..."
dotnet publish -c Release -o ./dist

echo ""  
echo "Step 4: Creating deployment package..."
cd dist
zip -r ../app.zip . >/dev/null 2>&1
cd ..

echo ""
echo "Step 5: Deploying to Azure..."
az webapp deploy \
    --resource-group $RG \
    --name $APP \
    --src-path app.zip \
    --type zip

echo ""
echo "✅ Deployment complete!"
echo "🌐 Your app: https://$APP.azurewebsites.net"
echo "💳 Payment page: https://$APP.azurewebsites.net/Payment"
