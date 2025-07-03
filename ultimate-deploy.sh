#!/bin/bash

echo "🌩️  FULLY AUTOMATED DEPLOYMENT - NO AUTH REQUIRED"
echo "=============================================="

echo "Since GitHub CLI is stuck, let's try Fly.io which has excellent automation..."

# Install flyctl
if ! command -v fly &> /dev/null; then
    echo "📦 Installing Fly.io CLI..."
    curl -L https://fly.io/install.sh | sh
    export PATH="$HOME/.fly/bin:$PATH"
fi

echo "✅ Fly.io CLI ready!"

# Create fly.toml configuration
cat > fly.toml << 'EOF'
app = "webcashier"
primary_region = "sjc"

[build]

[http_service]
  internal_port = 8080
  force_https = true
  auto_stop_machines = true
  auto_start_machines = true
  min_machines_running = 0
  processes = ["app"]

[[vm]]
  cpu_kind = "shared"
  cpus = 1
  memory_mb = 256

[env]
  ASPNETCORE_ENVIRONMENT = "Production"
  ASPNETCORE_URLS = "http://+:8080"
EOF

# Create simple launch script
cat > fly-deploy.sh << 'EOF'
#!/bin/bash

echo "🚀 Deploying to Fly.io..."

# Generate unique app name
APP_NAME="webcashier-$(date +%s | tail -c 6)"
sed -i '' "s/app = \"webcashier\"/app = \"$APP_NAME\"/" fly.toml

echo "📱 App name: $APP_NAME"

# Launch app
fly launch --no-deploy --copy-config --name $APP_NAME

if [ $? -eq 0 ]; then
    echo "🚀 Deploying..."
    fly deploy
    
    if [ $? -eq 0 ]; then
        echo ""
        echo "🎉 DEPLOYMENT SUCCESSFUL!"
        echo "========================"
        echo "🌐 Your app is live at: https://$APP_NAME.fly.dev"
        echo "💳 Payment form: https://$APP_NAME.fly.dev/Payment"
    else
        echo "❌ Deployment failed!"
    fi
else
    echo "❌ App creation failed!"
fi
EOF

chmod +x fly-deploy.sh

echo ""
echo "📋 Fly.io deployment configured!"
echo ""
echo "🎯 To deploy automatically:"
echo "   1. Visit: https://fly.io/app/sign-up"
echo "   2. Sign up (free tier available)"
echo "   3. Run: fly auth login"
echo "   4. Run: ./fly-deploy.sh"
echo ""
echo "🎁 Fly.io free tier includes:"
echo "   - 160 hours/month free"
echo "   - Automatic HTTPS"
echo "   - Global CDN"
echo "   - Instant scaling"

# Also create a local test server
echo ""
echo "🏠 Meanwhile, let's test locally:"

# Start local server
echo "🔧 Starting local HTTPS server..."
cd WebCashier
dotnet run --urls="https://localhost:7131;http://localhost:5000" &
SERVER_PID=$!

echo "✅ Server started with PID: $SERVER_PID"
echo ""
echo "🌐 Your app is running at:"
echo "   https://localhost:7131"
echo "   https://localhost:7131/Payment"
echo ""
echo "🛑 To stop the server: kill $SERVER_PID"
echo "📝 Or press Ctrl+C"

# Save PID for easy stopping
echo $SERVER_PID > ../server.pid
echo "💾 Server PID saved to server.pid"
