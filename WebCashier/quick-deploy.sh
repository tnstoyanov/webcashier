#!/bin/bash

# Quick Azure deployment - simplified version
echo "🚀 Quick Azure deployment for WebCashier..."

# Use existing resource group
RESOURCE_GROUP="WebCashier-RG"
WEB_APP_NAME="webcashier-quick-$(date +%H%M)"

echo "Creating web app: $WEB_APP_NAME"

# Create web app directly (this is faster)
az webapp create \
    --resource-group $RESOURCE_GROUP \
    --plan WebCashier-Plan \
    --name $WEB_APP_NAME \
    --runtime "DOTNETCORE:8.0" \
    --output table

echo ""
echo "🎉 App created! URL: https://$WEB_APP_NAME.azurewebsites.net"
echo "Now deploying code..."

# Build and deploy
dotnet publish -c Release -o ./publish
cd publish
zip -r ../deploy.zip .
cd ..

az webapp deploy \
    --resource-group $RESOURCE_GROUP \
    --name $WEB_APP_NAME \
    --src-path deploy.zip \
    --type zip

echo ""
echo "✅ Deployment complete!"
echo "🌐 Live at: https://$WEB_APP_NAME.azurewebsites.net/Payment"
