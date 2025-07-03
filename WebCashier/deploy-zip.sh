#!/bin/bash

echo "📦 Azure ZIP Deployment (Alternative Approach)"
echo "=============================================="

# Configuration
RESOURCE_GROUP="WebCashier-RG"
APP_NAME="webcashier-zip-$(date +%s)"
LOCATION="Central US"  # Try a different region

echo "📋 Configuration:"
echo "   Resource Group: $RESOURCE_GROUP"
echo "   App Name: $APP_NAME"
echo "   Location: $LOCATION"

# Try creating a Basic tier (B1) which is low cost but not completely free
echo "🔍 Checking quotas for Basic tier..."

# First, let's try to create with a different approach - using az webapp up
echo "🚀 Attempting deployment with az webapp up..."
echo "   This command auto-creates resources and deploys in one step..."

# Build the app first
echo "🔨 Building application..."
dotnet publish -c Release -o ./publish

if [ $? -ne 0 ]; then
    echo "❌ Build failed!"
    exit 1
fi

echo "✅ Build successful!"

# Try the webapp up command which is more flexible with resource creation
cd publish
az webapp up \
    --name $APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --location "$LOCATION" \
    --sku F1 \
    --runtime "DOTNETCORE:8.0" \
    --verbose

DEPLOY_RESULT=$?
cd ..

if [ $DEPLOY_RESULT -eq 0 ]; then
    echo ""
    echo "🎉 DEPLOYMENT SUCCESSFUL!"
    echo "========================"
    echo "🌐 Your app should be available at: https://$APP_NAME.azurewebsites.net"
    echo "💳 Payment form: https://$APP_NAME.azurewebsites.net/Payment"
    echo ""
    
    # Test the URL
    echo "🔍 Testing deployment..."
    curl -s -o /dev/null -w "%{http_code}" "https://$APP_NAME.azurewebsites.net" | {
        read response
        if [ "$response" = "200" ]; then
            echo "✅ App is responding correctly!"
        else
            echo "⚠️  App returned status code: $response"
            echo "   It may take a few minutes to fully start up."
        fi
    }
else
    echo ""
    echo "❌ Deployment failed with az webapp up"
    echo ""
    echo "🔄 Trying alternative regions..."
    
    # Try different regions
    for region in "West US 2" "East US 2" "North Central US" "South Central US"; do
        echo "🔍 Trying region: $region"
        APP_NAME_REGION="webcashier-$(echo "$region" | tr '[:upper:] ' '[:lower:]-')-$(date +%s)"
        
        cd publish
        az webapp up \
            --name $APP_NAME_REGION \
            --resource-group $RESOURCE_GROUP \
            --location "$region" \
            --sku F1 \
            --runtime "DOTNETCORE:8.0" \
            --verbose
        
        if [ $? -eq 0 ]; then
            echo ""
            echo "🎉 SUCCESS in region: $region"
            echo "🌐 Your app is available at: https://$APP_NAME_REGION.azurewebsites.net"
            echo "💳 Payment form: https://$APP_NAME_REGION.azurewebsites.net/Payment"
            cd ..
            exit 0
        fi
        cd ..
        
        echo "❌ Failed in region: $region"
    done
    
    echo ""
    echo "❌ All Azure regions failed due to quota limitations"
    echo ""
    echo "🎯 Alternative free options available:"
    echo "   1. Run: ./deploy-render.sh (Render.com - excellent free tier)"
    echo "   2. Run: ./deploy-railway.sh (Railway.app - $5 monthly credit)"
    echo "   3. Set up GitHub Pages (static hosting)"
    echo ""
    echo "💡 For Azure, you may need to:"
    echo "   1. Wait 24-48 hours for quota to refresh"
    echo "   2. Request quota increase in Azure portal"
    echo "   3. Try a different Azure subscription"
fi
