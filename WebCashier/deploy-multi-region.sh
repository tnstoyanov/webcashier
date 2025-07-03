#!/bin/bash

# Try Multiple Regions for Free App Service Deployment
echo "🌍 Trying Multiple Azure Regions for Free App Service"
echo "====================================================="

RESOURCE_GROUP="WebCashier-RG"
APP_NAME="webcashier-free-$(date +%s)"
PLAN_NAME="WebCashier-FreePlan-$(date +%s)"

# List of regions to try (some may have better free tier availability)
REGIONS=("westus2" "centralus" "eastus2" "westeurope" "northeurope" "southeastasia" "australiaeast")

echo "📋 Configuration:"
echo "   Resource Group: $RESOURCE_GROUP"
echo "   App Name: $APP_NAME"
echo "   Plan Name: $PLAN_NAME"
echo ""

# Test Azure CLI
echo "✅ Testing Azure CLI..."
if ! az account show > /dev/null 2>&1; then
    echo "❌ Please login to Azure CLI first: az login"
    exit 1
fi
echo "✅ Azure CLI OK"

# Ensure resource group exists
echo "✅ Checking resource group..."
if ! az group show --name "$RESOURCE_GROUP" > /dev/null 2>&1; then
    echo "⚠️  Creating resource group in eastus..."
    az group create --name "$RESOURCE_GROUP" --location "eastus"
fi

# Try each region
for REGION in "${REGIONS[@]}"; do
    echo ""
    echo "🌍 Trying region: $REGION"
    echo "--------------------------------"
    
    REGION_PLAN_NAME="${PLAN_NAME}-${REGION}"
    REGION_APP_NAME="${APP_NAME}-${REGION}"
    
    # Try to create App Service plan in this region
    echo "⚠️  Creating App Service plan in $REGION..."
    
    az appservice plan create \
        --name "$REGION_PLAN_NAME" \
        --resource-group "$RESOURCE_GROUP" \
        --location "$REGION" \
        --sku F1 \
        --is-linux false > /dev/null 2>&1
    
    if [ $? -eq 0 ]; then
        echo "✅ Success! App Service plan created in $REGION"
        
        # Create web app
        echo "🚀 Creating web app..."
        az webapp create \
            --resource-group "$RESOURCE_GROUP" \
            --plan "$REGION_PLAN_NAME" \
            --name "$REGION_APP_NAME" \
            --runtime "DOTNETCORE:8.0"
        
        if [ $? -eq 0 ]; then
            echo "✅ Web app created successfully!"
            
            # Deploy the app
            echo "📦 Deploying application..."
            
            # Ensure we have the publish folder
            if [ ! -d "./publish" ]; then
                echo "🔨 Building application..."
                dotnet publish -c Release -o ./publish
            fi
            
            # Create deployment zip
            cd publish
            zip -r ../deploy.zip . > /dev/null 2>&1
            cd ..
            
            # Deploy
            az webapp deployment source config-zip \
                --resource-group "$RESOURCE_GROUP" \
                --name "$REGION_APP_NAME" \
                --src deploy.zip
            
            if [ $? -eq 0 ]; then
                echo ""
                echo "🎉 DEPLOYMENT SUCCESSFUL!"
                echo "=========================="
                echo "🌐 Your app is available at:"
                echo "   https://$REGION_APP_NAME.azurewebsites.net"
                echo "💳 Payment form:"
                echo "   https://$REGION_APP_NAME.azurewebsites.net/Payment"
                echo ""
                echo "📊 App details:"
                echo "   Region: $REGION"
                echo "   Plan: $REGION_PLAN_NAME"
                echo "   App: $REGION_APP_NAME"
                exit 0
            else
                echo "❌ Deployment failed in $REGION"
            fi
        else
            echo "❌ Web app creation failed in $REGION"
        fi
    else
        echo "❌ No free tier quota available in $REGION"
    fi
done

echo ""
echo "❌ Could not find any region with available free tier quota"
echo "💡 Alternative options:"
echo "   1. Request quota increase: https://portal.azure.com"
echo "   2. Try Azure Container Instances: ./deploy-aci-free.sh"
echo "   3. Use Azure Static Web Apps (for static sites)"
echo "   4. Deploy to other free platforms (Heroku, Railway, etc.)"
