#!/bin/bash

echo "🚀 FULLY AUTOMATED DEPLOYMENT WITH HEROKU"
echo "========================================"

# Check if Heroku CLI is installed
if ! command -v heroku &> /dev/null; then
    echo "📦 Installing Heroku CLI..."
    curl https://cli-assets.heroku.com/install.sh | sh
    
    if [ $? -ne 0 ]; then
        echo "⚠️  Heroku CLI installation failed. Installing via Homebrew..."
        brew tap heroku/brew && brew install heroku
    fi
fi

echo "✅ Heroku CLI ready!"

# Create heroku.yml for container deployment
cat > heroku.yml << 'EOF'
build:
  docker:
    web: Dockerfile
run:
  web: dotnet WebCashier.dll --urls http://0.0.0.0:$PORT
EOF

# Update Dockerfile for Heroku
cat > Dockerfile << 'EOF'
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["WebCashier/WebCashier.csproj", "WebCashier/"]
RUN dotnet restore "WebCashier/WebCashier.csproj"
COPY . .
WORKDIR "/src/WebCashier"
RUN dotnet build "WebCashier.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WebCashier.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "WebCashier.dll"]
EOF

echo "📋 Created Heroku deployment files!"
echo ""
echo "🔑 Next steps for automated deployment:"
echo ""
echo "1. Login to Heroku:"
echo "   heroku login --interactive"
echo ""
echo "2. Create and deploy app:"
echo "   heroku create webcashier-\$(date +%s)"
echo "   heroku stack:set container"
echo "   git add -A"
echo "   git commit -m 'Deploy to Heroku'"
echo "   git push heroku main"
echo ""
echo "📱 Or run the automated script:"
echo "   ./heroku-auto-deploy.sh"

# Create automated deployment script
cat > heroku-auto-deploy.sh << 'EOF'
#!/bin/bash

echo "🚀 Automated Heroku Deployment Starting..."

# Login check
if ! heroku auth:whoami &> /dev/null; then
    echo "🔑 Please login to Heroku first:"
    heroku login --interactive
fi

# Create unique app name
APP_NAME="webcashier-$(date +%s)"
echo "📱 Creating Heroku app: $APP_NAME"

# Create app
heroku create $APP_NAME

if [ $? -ne 0 ]; then
    echo "❌ Failed to create Heroku app!"
    exit 1
fi

# Set stack to container
echo "🐳 Setting container stack..."
heroku stack:set container --app $APP_NAME

# Add all files and commit
echo "📝 Committing files..."
git add -A
git commit -m "Deploy WebCashier to Heroku" || true

# Deploy to Heroku
echo "🚀 Deploying to Heroku..."
git push heroku main

if [ $? -eq 0 ]; then
    echo ""
    echo "🎉 DEPLOYMENT SUCCESSFUL!"
    echo "========================"
    echo "🌐 Your app is live at: https://$APP_NAME.herokuapp.com"
    echo "💳 Payment form: https://$APP_NAME.herokuapp.com/Payment"
    echo ""
    echo "🛠️  Useful commands:"
    echo "   heroku logs --tail --app $APP_NAME"
    echo "   heroku ps --app $APP_NAME"
else
    echo "❌ Deployment failed!"
    echo "Check logs: heroku logs --tail --app $APP_NAME"
fi
EOF

chmod +x heroku-auto-deploy.sh

echo ""
echo "✅ Heroku deployment ready!"
echo "🎯 Run: ./heroku-auto-deploy.sh"
