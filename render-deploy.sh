#!/bin/bash

echo "🎉 RENDER.COM DEPLOYMENT - FINAL STEPS"
echo "====================================="
echo ""
echo "Great! You're logged in to Render.com. Now let's deploy your WebCashier app."
echo ""

# Check current directory
if [ ! -f "cashier.sln" ]; then
    echo "⚠️  Make sure you're in the correct directory!"
    echo "   cd /Users/tonystoyanov/Documents/cashier"
    exit 1
fi

echo "✅ You're in the correct directory"
echo ""

echo "STEP 1: Create GitHub Repository"
echo "-------------------------------"
echo "1. Open a new tab: https://github.com/new"
echo "2. Repository name: webcashier"
echo "3. Description: WebCashier - Secure Payment Processing App"
echo "4. Make it PUBLIC (required for free Render deployment)"
echo "5. DON'T add README (we have files already)"
echo "6. Click 'Create repository'"
echo ""

read -p "Press Enter after creating the GitHub repository..."

echo ""
echo "STEP 2: Enter Your GitHub Username"
echo "---------------------------------"
read -p "GitHub username: " GITHUB_USERNAME

if [ -z "$GITHUB_USERNAME" ]; then
    echo "❌ Username required!"
    exit 1
fi

echo ""
echo "STEP 3: Push to GitHub"
echo "---------------------"
echo "🔗 Adding GitHub remote..."

# Remove any existing origin (in case of previous attempts)
git remote remove origin 2>/dev/null || true

# Add the new remote
git remote add origin https://github.com/$GITHUB_USERNAME/webcashier.git

echo "📤 Pushing to GitHub..."
git branch -M main
git push -u origin main

if [ $? -eq 0 ]; then
    echo ""
    echo "✅ SUCCESS! Code pushed to GitHub!"
    echo "📍 Repository: https://github.com/$GITHUB_USERNAME/webcashier"
    echo ""
    
    echo "STEP 4: Deploy to Render.com"
    echo "----------------------------"
    echo "Now go back to Render.com and:"
    echo ""
    echo "1. Click 'New +' button"
    echo "2. Select 'Web Service'"
    echo "3. Choose 'Build and deploy from a Git repository'"
    echo "4. Click 'Connect' next to GitHub"
    echo "5. Find and select: $GITHUB_USERNAME/webcashier"
    echo "6. Click 'Connect'"
    echo ""
    echo "Render will automatically:"
    echo "✅ Detect your render.yaml configuration"
    echo "✅ Set up .NET 8.0 environment"
    echo "✅ Build and deploy your app"
    echo "✅ Provide HTTPS URL"
    echo ""
    echo "🎯 Your app will be live at:"
    echo "   https://webcashier-[random].onrender.com"
    echo "   https://webcashier-[random].onrender.com/Payment"
    echo ""
    echo "🎉 DEPLOYMENT READY!"
    
    # Open Render.com to make it easier
    echo ""
    echo "🌐 Opening Render.com for you..."
    open "https://render.com/dashboard" 2>/dev/null || echo "Visit: https://render.com/dashboard"
    
else
    echo ""
    echo "❌ Push failed! Please check:"
    echo "1. GitHub repository was created correctly"
    echo "2. Repository name is exactly 'webcashier'"
    echo "3. You have write access to the repository"
    echo ""
    echo "Try running: git push origin main"
fi
