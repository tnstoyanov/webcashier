#!/bin/bash

echo "🚀 Starting Fly.io deployment..."

# Check if flyctl is installed
if ! command -v flyctl &> /dev/null; then
    echo "Installing Fly.io CLI..."
    curl -L https://fly.io/install.sh | sh
    export PATH="$PATH:$HOME/.fly/bin"
fi

echo "Fly.io CLI version:"
flyctl version

# Navigate to project directory
cd /Users/tonystoyanov/Documents/cashier

# Login to Fly.io (will open browser for auth)
echo "Logging into Fly.io..."
flyctl auth login

# Initialize the app
echo "Initializing Fly.io app..."
flyctl launch --no-deploy --name webcashier

# Deploy the app
echo "Deploying to Fly.io..."
flyctl deploy

echo "🎉 Deployment complete!"
echo "Your app should be available at: https://webcashier.fly.dev"
