# Quick GitHub Deployment Guide

## 1. Push to GitHub
```bash
cd /Users/tonystoyanov/Documents/cashier
git init
git add .
git commit -m "Initial commit"
git remote add origin https://github.com/yourusername/webcashier.git
git push -u origin main
```

## 2. Create Azure Web App via Portal
- Go to portal.azure.com
- Create Web App with .NET 8
- Note the app name

## 3. Enable GitHub Deployment
- In Azure Portal, go to your Web App
- Go to "Deployment Center"
- Choose "GitHub"
- Select your repository
- Azure will auto-create the workflow

## 4. Auto-Deploy
Every push to main branch will automatically deploy!

Your app will be live at: https://yourappname.azurewebsites.net
