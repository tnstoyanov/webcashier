#!/bin/bash

echo "🚀 SIMPLE TERMINAL DEPLOYMENT GUIDE"
echo "=================================="
echo ""
echo "Since GitHub CLI is having issues, here's the manual approach:"
echo ""
echo "STEP 1: Create GitHub Repository"
echo "--------------------------------"
echo "1. Open browser: https://github.com"
echo "2. Click 'New repository' (green + button)"
echo "3. Repository name: webcashier"
echo "4. Description: WebCashier - Secure Payment Processing App"
echo "5. Keep it PUBLIC (for free hosting)"
echo "6. DON'T check 'Add README' (we have files already)"
echo "7. Click 'Create repository'"
echo ""
echo "STEP 2: Push Code to GitHub (run these commands)"
echo "-----------------------------------------------"
echo "After creating the repo, GitHub will show you commands. Run:"
echo ""
echo "git remote add origin https://github.com/YOUR_USERNAME/webcashier.git"
echo "git branch -M main"
echo "git push -u origin main"
echo ""
echo "STEP 3: Deploy to Render.com"
echo "----------------------------"
echo "1. Go to: https://render.com"
echo "2. Sign up with GitHub (free)"
echo "3. Click 'New +' → 'Web Service'"
echo "4. Connect your GitHub account"
echo "5. Select the 'webcashier' repository"
echo "6. Render will auto-detect render.yaml"
echo "7. Click 'Create Web Service'"
echo ""
echo "STEP 4: Get Your Live URL"
echo "------------------------"
echo "Your app will be live at:"
echo "https://webcashier-[random].onrender.com"
echo "Payment form at:"
echo "https://webcashier-[random].onrender.com/Payment"
echo ""
echo "✅ Your WebCashier app is ready for deployment!"
echo "All configuration files are already set up."

# Also create a simple script to prepare for pushing
echo ""
echo "To run the git commands above, I'll create a helper script..."

cat > push-to-github.sh << 'EOF'
#!/bin/bash

echo "🔄 Preparing to push to GitHub..."
echo ""
echo "⚠️  IMPORTANT: First create the repository on GitHub!"
echo "   Go to: https://github.com"
echo "   Create repository named: webcashier"
echo "   Then come back and continue."
echo ""
read -p "Press Enter after creating the GitHub repository..."

echo "📝 Please enter your GitHub username:"
read -p "Username: " GITHUB_USERNAME

if [ -z "$GITHUB_USERNAME" ]; then
    echo "❌ Username cannot be empty!"
    exit 1
fi

echo ""
echo "🔗 Adding GitHub remote..."
git remote add origin https://github.com/$GITHUB_USERNAME/webcashier.git

echo "🌿 Setting main branch..."
git branch -M main

echo "⬆️  Pushing to GitHub..."
git push -u origin main

if [ $? -eq 0 ]; then
    echo ""
    echo "🎉 SUCCESS! Code pushed to GitHub!"
    echo "📍 Your repository: https://github.com/$GITHUB_USERNAME/webcashier"
    echo ""
    echo "🚀 Next step: Deploy to Render.com"
    echo "   1. Go to: https://render.com"
    echo "   2. Sign up with GitHub"
    echo "   3. Create Web Service from your repo"
    echo "   4. Your app will be live!"
else
    echo ""
    echo "❌ Push failed! Make sure:"
    echo "   1. You created the GitHub repository"
    echo "   2. The repository name is 'webcashier'"
    echo "   3. You have write access to the repository"
fi
EOF

chmod +x push-to-github.sh

echo ""
echo "📜 Created helper script: push-to-github.sh"
echo "   Run: ./push-to-github.sh"
echo "   This will guide you through pushing to GitHub"
