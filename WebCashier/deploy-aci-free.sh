#!/bin/bash

# Azure Container Instances Free Deployment Script
echo "🐳 Deploying WebCashier to Azure Container Instances (Free Tier)"
echo "================================================================"

# Configuration
RESOURCE_GROUP="WebCashier-RG"
CONTAINER_NAME="webcashier-free-$(date +%s)"
IMAGE_NAME="webcashier:latest"
LOCATION="eastus"

echo "📋 Configuration:"
echo "   Resource Group: $RESOURCE_GROUP"
echo "   Container Name: $CONTAINER_NAME"
echo "   Location: $LOCATION"
echo ""

# Test Azure CLI
echo "✅ Testing Azure CLI..."
if ! az account show > /dev/null 2>&1; then
    echo "❌ Please login to Azure CLI first: az login"
    exit 1
fi
echo "✅ Azure CLI OK"

# Check if resource group exists
echo "✅ Checking resource group..."
if ! az group show --name "$RESOURCE_GROUP" > /dev/null 2>&1; then
    echo "⚠️  Creating resource group..."
    az group create --name "$RESOURCE_GROUP" --location "$LOCATION"
fi
echo "✅ Resource group ready"

# Build Docker image locally
echo "🔨 Building Docker image..."
if [ ! -f "Dockerfile" ]; then
    echo "❌ Dockerfile not found!"
    exit 1
fi

docker build -t "$IMAGE_NAME" .
if [ $? -ne 0 ]; then
    echo "❌ Docker build failed!"
    exit 1
fi
echo "✅ Docker image built"

# Create Azure Container Registry (Free tier)
ACR_NAME="webcashierregistry$(date +%s | tail -c 6)"
echo "📦 Creating Azure Container Registry (Free tier): $ACR_NAME"

az acr create \
    --resource-group "$RESOURCE_GROUP" \
    --name "$ACR_NAME" \
    --sku Basic \
    --location "$LOCATION"

if [ $? -ne 0 ]; then
    echo "❌ Failed to create Container Registry"
    exit 1
fi

# Enable admin access
az acr update --name "$ACR_NAME" --admin-enabled true

# Get ACR login server
ACR_SERVER=$(az acr show --name "$ACR_NAME" --resource-group "$RESOURCE_GROUP" --query "loginServer" --output tsv)
echo "✅ ACR Server: $ACR_SERVER"

# Login to ACR
echo "🔐 Logging into Container Registry..."
az acr login --name "$ACR_NAME"

# Tag and push image
echo "📤 Pushing image to registry..."
docker tag "$IMAGE_NAME" "$ACR_SERVER/$IMAGE_NAME"
docker push "$ACR_SERVER/$IMAGE_NAME"

# Get ACR credentials
echo "🔑 Getting registry credentials..."
ACR_USERNAME=$(az acr credential show --name "$ACR_NAME" --query "username" --output tsv)
ACR_PASSWORD=$(az acr credential show --name "$ACR_NAME" --query "passwords[0].value" --output tsv)

# Deploy to Azure Container Instances
echo "🚀 Deploying to Azure Container Instances..."
az container create \
    --resource-group "$RESOURCE_GROUP" \
    --name "$CONTAINER_NAME" \
    --image "$ACR_SERVER/$IMAGE_NAME" \
    --registry-login-server "$ACR_SERVER" \
    --registry-username "$ACR_USERNAME" \
    --registry-password "$ACR_PASSWORD" \
    --dns-name-label "$CONTAINER_NAME" \
    --ports 80 \
    --location "$LOCATION" \
    --cpu 0.5 \
    --memory 0.5

if [ $? -eq 0 ]; then
    echo ""
    echo "🎉 Deployment successful!"
    echo "📱 Your app should be available at:"
    echo "   http://$CONTAINER_NAME.$LOCATION.azurecontainer.io"
    echo ""
    echo "📊 Check status with:"
    echo "   az container show --resource-group $RESOURCE_GROUP --name $CONTAINER_NAME"
else
    echo "❌ Deployment failed!"
    exit 1
fi
