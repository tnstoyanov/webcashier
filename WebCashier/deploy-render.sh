#!/bin/bash

echo "🎨 Render.com Free Deployment"
echo "============================="

echo "✅ Setting up Render.com deployment..."

# Create render.yaml for Render.com
cat > render.yaml << EOF
services:
  - type: web
    name: webcashier
    env: dotnet
    buildCommand: dotnet publish -c Release -o ./publish
    startCommand: dotnet publish/WebCashier.dll --urls http://0.0.0.0:\$PORT
    envVars:
      - key: ASPNETCORE_ENVIRONMENT
        value: Production
      - key: ASPNETCORE_URLS
        value: http://0.0.0.0:\$PORT
EOF

# Create a simple build script
cat > build.sh << EOF
#!/bin/bash
echo "Building WebCashier for Render.com..."
dotnet publish -c Release -o ./publish
echo "Build complete!"
EOF

chmod +x build.sh

echo "✅ Render.com configuration created!"
echo ""
echo "📁 Files created:"
echo "  - render.yaml"
echo "  - build.sh"
echo ""
echo "🎯 Deployment steps:"
echo "  1. Visit: https://render.com"
echo "  2. Sign up with GitHub (free)"
echo "  3. Connect your GitHub repository"
echo "  4. Choose 'Web Service'"
echo "  5. Select this repository"
echo "  6. Render will auto-detect the render.yaml"
echo ""
echo "🎁 Render.com free tier includes:"
echo "  - 750 hours/month free"
echo "  - Automatic HTTPS"
echo "  - Custom domains"
echo "  - Auto-deploy from Git"
echo "  - Sleep after 15min inactivity (wakes on request)"
