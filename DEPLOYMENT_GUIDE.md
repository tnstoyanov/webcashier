# WebCashier Deployment Guide - Render.com

## 🎉 Ready for Deployment!

The WebCashier application has been successfully configured and pushed to GitHub. Here's how to deploy it to Render.com:

## 📋 Pre-deployment Checklist

✅ **Code Status**: All changes committed and pushed to GitHub
✅ **SSL Issues**: Fixed - TLS 1.2 configuration working
✅ **Payment Flow**: Success/failure redirection working
✅ **Praxis Integration**: API calls successful with 3DS support
✅ **Error Handling**: Comprehensive logging and error handling
✅ **Production Config**: appsettings.Production.json configured
✅ **Docker Support**: .NET 9.0 Dockerfile ready
✅ **Render Config**: render.yaml configured for deployment

## 🚀 Deployment Steps

### 1. Access Render.com
- Visit: https://render.com
- Sign up with your GitHub account (free tier available)

### 2. Create New Web Service
- Click "New +" → "Web Service"
- Connect your GitHub account if not already connected
- Select the `webcashier` repository
- Choose the `main` branch

### 3. Configure Service
- **Name**: `webcashier` (or your preferred name)
- **Environment**: `Docker` (Render will auto-detect)
- **Region**: Choose your preferred region
- **Branch**: `main`
- **Build Command**: `dotnet publish -c Release -o ./publish`
- **Start Command**: `dotnet publish/WebCashier.dll --urls http://0.0.0.0:$PORT`

### 4. Environment Variables
Render should automatically detect these from render.yaml:
- `ASPNETCORE_ENVIRONMENT`: `Production`
- `ASPNETCORE_URLS`: `http://0.0.0.0:$PORT`

### 5. Deploy
- Click "Create Web Service"
- Render will automatically build and deploy your application
- Wait for deployment to complete (usually 5-10 minutes)

## 🔧 Post-Deployment Configuration

### Update Return URL
After deployment, you'll get a Render URL like: `https://webcashier-abc123.onrender.com`

**Important**: Update the ReturnUrl in your deployed app:
1. Go to your Render dashboard
2. Navigate to Environment tab
3. Add environment variable:
   - **Key**: `Praxis__ReturnUrl`
   - **Value**: `https://your-app-name.onrender.com/Payment/Return`

### Test Payment Flow
1. Visit your deployed app: `https://your-app-name.onrender.com`
2. Navigate to `/Payment`
3. Test a payment with test card: `4242 4242 4242 4242`
4. Verify 3DS flow and return handling

## 📊 Monitoring

### Render Dashboard
- View logs in real-time
- Monitor deployment status
- Check service health

### Application Logs
The app includes comprehensive logging for:
- Payment processing
- SSL connections
- API calls to Praxis
- Return URL handling
- Error tracking

## 🎯 Free Tier Limits

Render.com free tier includes:
- 750 hours/month (enough for development/testing)
- Automatic HTTPS
- Custom domains
- Sleep after 15 minutes of inactivity

## 🔐 Security Notes

- SSL/TLS 1.2 properly configured
- Certificate validation bypass only in development
- Production environment uses proper SSL validation
- All sensitive data handled securely

## 🆘 Troubleshooting

### Common Issues:
1. **Build fails**: Check .NET 9.0 compatibility
2. **SSL errors**: Verify production SSL configuration
3. **Return URL issues**: Update ReturnUrl environment variable
4. **Payment failures**: Check Praxis configuration

### Debug Steps:
1. Check Render logs for errors
2. Verify environment variables
3. Test payment flow step by step
4. Check Praxis API responses

## 🎊 Success!

Your WebCashier application is now ready for production use with:
- Complete payment processing
- 3D Secure authentication
- Success/failure handling
- Beautiful UI
- Comprehensive logging
- SSL security

Happy payments! 💳✨
