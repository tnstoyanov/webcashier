#!/bin/bash

echo "🔧 FIXING GITHUB REPOSITORY ISSUE"
echo "================================="
echo ""
echo "✅ Username confirmed: tnstoyanov"
echo "❌ Repository 'webcashier' doesn't exist yet"
echo ""
echo "Let's fix this step by step:"
echo ""

echo "OPTION 1: Create Repository via Web Browser"
echo "-------------------------------------------"
echo "1. 🌐 Open: https://github.com/new"
echo "2. 📝 Repository name: webcashier"
echo "3. 📄 Description: WebCashier - Secure Payment Processing App"
echo "4. 🔓 Make it PUBLIC (required for free Render)"
echo "5. ❌ DON'T check 'Add README'"
echo "6. ✅ Click 'Create repository'"
echo ""

echo "OPTION 2: Create Repository via GitHub CLI"
echo "------------------------------------------"
echo "If GitHub CLI is working, run:"
echo "   gh repo create webcashier --public --description 'WebCashier - Secure Payment Processing App'"
echo ""

echo "OPTION 3: Alternative Repository Name"
echo "------------------------------------"
echo "If 'webcashier' is taken, try:"
echo "   - webcashier-app"
echo "   - payment-cashier"
echo "   - cashier-web"
echo "   - tnstoyanov-webcashier"
echo ""

echo "🎯 AFTER CREATING THE REPOSITORY:"
echo ""
echo "Run these commands:"
echo "   git remote remove origin"
echo "   git remote add origin https://github.com/tnstoyanov/webcashier.git"
echo "   git push -u origin main"
echo ""

# Let's also try to create it automatically
echo "🤖 ATTEMPTING AUTOMATIC CREATION..."
echo ""

# Try with GitHub CLI if available
if command -v gh &> /dev/null; then
    echo "📱 GitHub CLI found! Attempting to create repository..."
    gh repo create webcashier --public --description "WebCashier - Secure Payment Processing App" --confirm
    
    if [ $? -eq 0 ]; then
        echo "✅ Repository created successfully!"
        echo "🚀 Now pushing code..."
        
        git remote remove origin 2>/dev/null || true
        git remote add origin https://github.com/tnstoyanov/webcashier.git
        git push -u origin main
        
        if [ $? -eq 0 ]; then
            echo ""
            echo "🎉 SUCCESS! Code pushed to GitHub!"
            echo "📍 Repository: https://github.com/tnstoyanov/webcashier"
            echo ""
            echo "🚀 Now deploy on Render.com:"
            echo "1. Go to: https://render.com/dashboard"
            echo "2. Click 'New +' → 'Web Service'"
            echo "3. Connect GitHub: tnstoyanov/webcashier"
            echo "4. Deploy!"
            
            # Open Render dashboard
            open "https://render.com/dashboard" 2>/dev/null || echo "Visit: https://render.com/dashboard"
        else
            echo "❌ Push failed. Try manually."
        fi
    else
        echo "❌ Repository creation failed. Please create manually."
    fi
else
    echo "❌ GitHub CLI not available. Please create repository manually."
    echo ""
    echo "📖 Manual steps:"
    echo "1. Visit: https://github.com/new"
    echo "2. Create repository named: webcashier"
    echo "3. Then run:"
    echo "   git remote remove origin"
    echo "   git remote add origin https://github.com/tnstoyanov/webcashier.git"
    echo "   git push -u origin main"
fi

echo ""
echo "🔗 Quick links:"
echo "   📁 Create repo: https://github.com/new"
echo "   🚀 Render dashboard: https://render.com/dashboard"
