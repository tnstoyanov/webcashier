#!/bin/bash

echo "🚂 Railway.app Free Deployment"
echo "============================="

# Check if Railway CLI is installed
if ! command -v railway &> /dev/null; then
    echo "📦 Installing Railway CLI..."
    npm install -g @railway/cli
fi

echo "✅ Railway CLI ready!"

# Initialize Railway project
echo "🚀 Setting up Railway project..."
echo "Please follow these steps:"
echo ""
echo "1. Visit: https://railway.app"
echo "2. Sign up with GitHub (free)"
echo "3. Come back here and run: railway login"
echo "4. Then run: railway init"
echo "5. Finally run: railway up"
echo ""
echo "Railway offers:"
echo "  - $5 free credit monthly"
echo "  - Automatic HTTPS"
echo "  - Custom domains"
echo "  - Zero config deployment"
echo ""

# Create railway.json for configuration
cat > railway.json << EOF
{
  "deploy": {
    "startCommand": "dotnet WebCashier.dll",
    "healthcheckPath": "/",
    "healthcheckTimeout": 100
  }
}
EOF

# Create Procfile for Railway
cat > Procfile << EOF
web: dotnet publish/WebCashier.dll --urls http://0.0.0.0:\$PORT
EOF

echo "✅ Railway configuration files created!"
echo ""
echo "📁 Files created:"
echo "  - railway.json"
echo "  - Procfile"
echo ""
echo "🎯 Next steps:"
echo "  1. Run: railway login"
echo "  2. Run: railway init"
echo "  3. Run: railway up"
