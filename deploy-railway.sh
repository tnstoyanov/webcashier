#!/bin/bash

echo "🚀 Starting Railway deployment..."

# Check if railway CLI is installed
if ! command -v railway &> /dev/null; then
    echo "Installing Railway CLI..."
    npm install -g @railway/cli
fi

# Navigate to project directory
cd /Users/tonystoyanov/Documents/cashier

# Login to Railway
echo "Logging into Railway..."
railway login

# Initialize project
echo "Initializing Railway project..."
railway link

# Deploy
echo "Deploying to Railway..."
railway up

echo "🎉 Deployment complete!"
echo "Check your Railway dashboard for the live URL."
