#!/bin/bash

echo "🚀 WebCashier Terminal Deployment Script"
echo "========================================"

cd /Users/tonystoyanov/Documents/cashier

echo "Available deployment options:"
echo "1. Fly.io (recommended)"
echo "2. Railway"
echo "3. Heroku"

read -p "Choose deployment option (1-3): " choice

case $choice in
    1)
        echo "Deploying to Fly.io..."
        ./deploy-flyio.sh
        ;;
    2)
        echo "Deploying to Railway..."
        ./deploy-railway.sh
        ;;
    3)
        echo "Deploying to Heroku..."
        ./deploy-heroku.sh
        ;;
    *)
        echo "Invalid choice. Defaulting to Fly.io..."
        ./deploy-flyio.sh
        ;;
esac
