#!/bin/bash

echo "📦 DIRECT RENDER DEPLOYMENT (NO GITHUB NEEDED)"
echo "=============================================="
echo ""
echo "If GitHub is giving issues, you can deploy directly to Render:"
echo ""

# Create a deployment-ready package
echo "🔨 Creating deployment package..."

# Make sure we're in the right directory
cd /Users/tonystoyanov/Documents/cashier

# Create a clean deployment folder
rm -rf deploy-package
mkdir deploy-package

# Copy essential files for deployment
cp -r WebCashier/ deploy-package/
cp render.yaml deploy-package/ 2>/dev/null || echo "render.yaml will be created"
cp README.md deploy-package/ 2>/dev/null || echo "No README needed"

# Create render.yaml in the package if it doesn't exist
cat > deploy-package/render.yaml << 'EOF'
services:
  - type: web
    name: webcashier
    env: dotnet
    buildCommand: dotnet publish -c Release -o ./publish
    startCommand: dotnet publish/WebCashier.dll --urls http://0.0.0.0:$PORT
    envVars:
      - key: ASPNETCORE_ENVIRONMENT
        value: Production
      - key: ASPNETCORE_URLS
        value: http://0.0.0.0:$PORT
EOF

# Create a simple dockerfile in case render prefers it
cat > deploy-package/Dockerfile << 'EOF'
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["WebCashier.csproj", "."]
RUN dotnet restore "WebCashier.csproj"
COPY . .
RUN dotnet build "WebCashier.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WebCashier.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "WebCashier.dll"]
EOF

# Create deployment instructions
cat > deploy-package/DEPLOY_INSTRUCTIONS.txt << 'EOF'
RENDER.COM DEPLOYMENT INSTRUCTIONS
==================================

Option 1: GitHub Deployment (Recommended)
1. Create GitHub repository named 'webcashier'
2. Push this code to GitHub
3. Connect GitHub to Render
4. Deploy from repository

Option 2: Direct Upload
1. Zip this entire folder
2. Go to Render.com dashboard
3. Create new "Static Site" or "Web Service"
4. Upload the zip file
5. Set build command: dotnet publish -c Release -o ./publish
6. Set start command: dotnet publish/WebCashier.dll --urls http://0.0.0.0:$PORT

Your WebCashier app features:
- Payment form at /Payment
- Bootstrap UI with validation
- HTTPS ready
- Production configured
EOF

echo "✅ Deployment package created in: deploy-package/"
echo ""
echo "📁 Package contents:"
ls -la deploy-package/
echo ""
echo "🎯 NEXT STEPS:"
echo ""
echo "OPTION A: GitHub Method (run the other script)"
echo "   ./render-deploy.sh"
echo ""
echo "OPTION B: Direct Upload Method"
echo "1. Zip the deploy-package folder"
echo "2. Go to Render.com → New + → Web Service"
echo "3. Choose 'Deploy an existing image or build from source'"
echo "4. Upload your zip file"
echo "5. Set environment to 'Docker' or '.NET'"
echo ""
echo "🌐 Either way, your app will be live with HTTPS!"
echo ""

# Create zip file for easy upload
echo "📦 Creating zip file for direct upload..."
cd deploy-package
zip -r ../webcashier-deploy.zip . > /dev/null
cd ..
echo "✅ Created: webcashier-deploy.zip"
echo ""
echo "📤 You can upload webcashier-deploy.zip directly to Render!"

# Open deploy package folder
echo "📂 Opening deploy package folder..."
open deploy-package 2>/dev/null || echo "Deploy package ready in: $(pwd)/deploy-package"
