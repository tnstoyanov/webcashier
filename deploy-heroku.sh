#!/bin/bash

echo "🚀 Starting Heroku deployment..."

# Check if heroku CLI is installed
if ! command -v heroku &> /dev/null; then
    echo "Installing Heroku CLI..."
    curl https://cli-assets.heroku.com/install.sh | sh
fi

# Navigate to project directory
cd /Users/tonystoyanov/Documents/cashier

# Login to Heroku
echo "Logging into Heroku..."
heroku login

# Create the app
echo "Creating Heroku app..."
heroku create webcashier-app --stack container

# Set buildpack to container (since we have Dockerfile)
heroku stack:set container -a webcashier-app

# Deploy using git
echo "Deploying to Heroku..."
git push heroku main

echo "🎉 Deployment complete!"
echo "Your app should be available at: https://webcashier-app.herokuapp.com"
