#!/bin/bash

# Alternative Free Deployment Options
echo "🆓 Free Deployment Alternatives for WebCashier"
echo "=============================================="
echo ""
echo "Since Azure free tier quota is limited, here are other FREE options:"
echo ""
echo "1. 🚢 Railway (Free Tier)"
echo "   - 512MB RAM, 1GB Disk"
echo "   - No credit card required"
echo "   - Great for .NET apps"
echo "   - Command: railway login && railway deploy"
echo ""
echo "2. 🟢 Render (Free Tier)"
echo "   - 512MB RAM"
echo "   - Sleeps after 15 min of inactivity"
echo "   - Supports .NET"
echo "   - Web interface deployment"
echo ""
echo "3. 🐙 GitHub Pages + GitHub Actions"
echo "   - For static version of the app"
echo "   - Unlimited bandwidth"
echo "   - Custom domain support"
echo ""
echo "4. 🌍 Netlify (Free Tier)"
echo "   - 100GB bandwidth/month"
echo "   - For static sites"
echo "   - Drag & drop deployment"
echo ""
echo "5. ▲ Vercel (Free Tier)"
echo "   - Serverless functions"
echo "   - Good for frontend + API"
echo "   - 100GB bandwidth"
echo ""

read -p "Would you like to try Railway deployment? (y/n): " choice

if [[ $choice == "y" || $choice == "Y" ]]; then
    echo ""
    echo "🚂 Setting up Railway deployment..."
    
    # Check if Railway CLI is installed
    if ! command -v railway &> /dev/null; then
        echo "📦 Installing Railway CLI..."
        
        # Install Railway CLI
        if command -v npm &> /dev/null; then
            npm install -g @railway/cli
        elif command -v brew &> /dev/null; then
            brew install railway
        else
            echo "❌ Please install Node.js or Homebrew first"
            echo "Then run: npm install -g @railway/cli"
            exit 1
        fi
    fi
    
    echo "✅ Railway CLI ready"
    
    # Create railway.json
    cat > railway.json << 'JSON'
{
  "build": {
    "builder": "NIXPACKS"
  },
  "deploy": {
    "restartPolicyType": "ON_FAILURE",
    "restartPolicyMaxRetries": 10
  }
}
JSON
    
    # Create nixpacks.toml for .NET configuration
    cat > nixpacks.toml << 'TOML'
[phases.build]
cmd = 'dotnet publish -c Release -o out'

[phases.start]
cmd = 'cd out && dotnet WebCashier.dll'

[variables]
ASPNETCORE_URLS = 'http://0.0.0.0:$PORT'
ASPNETCORE_ENVIRONMENT = 'Production'
TOML
    
    echo "🔧 Railway configuration created"
    echo ""
    echo "Next steps:"
    echo "1. Run: railway login"
    echo "2. Run: railway init"
    echo "3. Run: railway up"
    echo ""
    echo "Your app will be deployed to a free Railway instance!"
    
else
    echo ""
    echo "💡 Manual deployment options:"
    echo ""
    echo "🌐 For static deployment to GitHub Pages:"
    echo "1. Push your code to GitHub"
    echo "2. Go to Settings > Pages"
    echo "3. Deploy from branch"
    echo ""
    echo "🎯 For Render deployment:"
    echo "1. Go to render.com"
    echo "2. Connect your GitHub repo"
    echo "3. Select 'Web Service'"
    echo "4. Build: dotnet publish -c Release -o out"
    echo "5. Start: cd out && dotnet WebCashier.dll"
    echo ""
    echo "⚡ For Vercel deployment:"
    echo "1. Install Vercel CLI: npm i -g vercel"
    echo "2. Run: vercel"
    echo "3. Follow the prompts"
fi
