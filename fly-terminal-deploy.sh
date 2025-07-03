#!/bin/bash

echo "🌩️  TERMINAL DEPLOYMENT WITH FLY.IO"
echo "=================================="
echo ""
echo "Fly.io has the best terminal deployment experience!"
echo ""

# Install flyctl if not present
if ! command -v fly &> /dev/null; then
    echo "📦 Installing Fly.io CLI..."
    curl -L https://fly.io/install.sh | sh
    export PATH="$HOME/.fly/bin:$PATH"
fi

echo "✅ Fly.io CLI ready!"
echo ""

# Create fly.toml for .NET deployment
cat > fly.toml << 'EOF'
app = "webcashier"
primary_region = "sjc"
console_command = "/bin/bash"

[build]

[env]
  ASPNETCORE_ENVIRONMENT = "Production"
  ASPNETCORE_URLS = "http://+:8080"

[http_service]
  internal_port = 8080
  force_https = true
  auto_stop_machines = true
  auto_start_machines = true
  min_machines_running = 0

[[vm]]
  cpu_kind = "shared"
  cpus = 1
  memory_mb = 256
EOF

# Generate unique app name
APP_NAME="webcashier-$(date +%s | tail -c 6)"
sed -i '' "s/app = \"webcashier\"/app = \"$APP_NAME\"/" fly.toml

echo "📱 App name: $APP_NAME"
echo ""

echo "🚀 AUTOMATED DEPLOYMENT STARTING..."
echo ""
echo "Steps:"
echo "1. fly auth login (opens browser)"
echo "2. fly launch --no-deploy --copy-config --name $APP_NAME"
echo "3. fly deploy"
echo ""

echo "🔑 Starting authentication..."
fly auth login

if [ $? -eq 0 ]; then
    echo ""
    echo "✅ Authentication successful!"
    echo ""
    echo "🚀 Creating and deploying app..."
    
    # Launch the app
    fly launch --no-deploy --copy-config --name $APP_NAME
    
    if [ $? -eq 0 ]; then
        echo ""
        echo "📦 Deploying to production..."
        fly deploy
        
        if [ $? -eq 0 ]; then
            echo ""
            echo "🎉 DEPLOYMENT SUCCESSFUL!"
            echo "========================"
            echo "🌐 Your app is live at: https://$APP_NAME.fly.dev"
            echo "💳 Payment form: https://$APP_NAME.fly.dev/Payment"
            echo ""
            echo "🛠️  Useful commands:"
            echo "   fly logs"
            echo "   fly status"
            echo "   fly ssh console"
        else
            echo "❌ Deployment failed!"
        fi
    else
        echo "❌ App creation failed!"
    fi
else
    echo "❌ Authentication failed!"
fi
