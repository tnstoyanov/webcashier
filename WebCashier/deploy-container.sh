#!/bin/bash

echo "🐳 Azure Container Instances Deployment"
echo "======================================"

# Configuration
RESOURCE_GROUP="WebCashier-RG"
CONTAINER_NAME="webcashier-container-$(date +%s)"
IMAGE_NAME="webcashier"
REGISTRY_NAME="webcashierregistry$(date +%s)"

echo "📋 Configuration:"
echo "   Resource Group: $RESOURCE_GROUP"
echo "   Container Name: $CONTAINER_NAME"
echo "   Registry Name: $REGISTRY_NAME"

# Check if Docker is installed
if ! command -v docker &> /dev/null; then
    echo "❌ Docker is not installed. Please install Docker first."
    exit 1
fi

echo "✅ Building Docker image locally..."
docker build -t $IMAGE_NAME .

if [ $? -ne 0 ]; then
    echo "❌ Docker build failed!"
    exit 1
fi

echo "✅ Docker image built successfully!"

# Create Azure Container Registry (has free tier)
echo "🏗️  Creating Azure Container Registry..."
az acr create \
    --resource-group $RESOURCE_GROUP \
    --name $REGISTRY_NAME \
    --sku Basic \
    --admin-enabled true

if [ $? -ne 0 ]; then
    echo "❌ Failed to create Azure Container Registry!"
    exit 1
fi

echo "✅ Azure Container Registry created!"

# Get registry credentials
echo "🔑 Getting registry credentials..."
REGISTRY_SERVER=$(az acr show --name $REGISTRY_NAME --resource-group $RESOURCE_GROUP --query "loginServer" --output tsv)
REGISTRY_USERNAME=$(az acr credential show --name $REGISTRY_NAME --resource-group $RESOURCE_GROUP --query "username" --output tsv)
REGISTRY_PASSWORD=$(az acr credential show --name $REGISTRY_NAME --resource-group $RESOURCE_GROUP --query "passwords[0].value" --output tsv)

echo "✅ Registry credentials retrieved!"

# Tag and push image
echo "📤 Pushing image to registry..."
docker tag $IMAGE_NAME $REGISTRY_SERVER/$IMAGE_NAME:latest

# Login to registry
echo $REGISTRY_PASSWORD | docker login $REGISTRY_SERVER --username $REGISTRY_USERNAME --password-stdin

# Push image
docker push $REGISTRY_SERVER/$IMAGE_NAME:latest

if [ $? -ne 0 ]; then
    echo "❌ Failed to push image to registry!"
    exit 1
fi

echo "✅ Image pushed successfully!"

# Create container instance
echo "🚀 Creating container instance..."
az container create \
    --resource-group $RESOURCE_GROUP \
    --name $CONTAINER_NAME \
    --image $REGISTRY_SERVER/$IMAGE_NAME:latest \
    --registry-login-server $REGISTRY_SERVER \
    --registry-username $REGISTRY_USERNAME \
    --registry-password $REGISTRY_PASSWORD \
    --dns-name-label $CONTAINER_NAME \
    --ports 80 \
    --environment-variables ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS=http://+:80

if [ $? -ne 0 ]; then
    echo "❌ Failed to create container instance!"
    exit 1
fi

echo "✅ Container instance created successfully!"

# Get the public URL
echo "🔗 Getting public URL..."
PUBLIC_URL=$(az container show --resource-group $RESOURCE_GROUP --name $CONTAINER_NAME --query "ipAddress.fqdn" --output tsv)

if [ -n "$PUBLIC_URL" ]; then
    echo ""
    echo "🎉 DEPLOYMENT SUCCESSFUL!"
    echo "========================"
    echo "🌐 Your app is available at: http://$PUBLIC_URL"
    echo "💳 Payment form: http://$PUBLIC_URL/Payment"
    echo ""
    echo "📊 Container status:"
    az container show --resource-group $RESOURCE_GROUP --name $CONTAINER_NAME --query "containers[0].instanceView.currentState" --output table
else
    echo "❌ Could not retrieve public URL!"
fi

echo ""
echo "🛠️  Useful commands:"
echo "   Check logs: az container logs --resource-group $RESOURCE_GROUP --name $CONTAINER_NAME"
echo "   Check status: az container show --resource-group $RESOURCE_GROUP --name $CONTAINER_NAME"
echo "   Delete container: az container delete --resource-group $RESOURCE_GROUP --name $CONTAINER_NAME --yes"
