# WebCashier - Secure Payment Application

A secure payment processing web application built with ASP.NET Core MVC.

## 🚀 Live Demo
- **Render.com Deployment**: https://webcashier.onrender.com/Payment
- **Local Development**: `https://localhost:7068`

## 🆓 Render.com Free Tier Deployment

This application is deployed to Render.com's free tier:

### Features
- **Free Hosting**: Render.com provides free hosting for web applications
- **Automatic SSL**: Free SSL certificates included
- **GitHub Integration**: Auto-deploy on git push
- **.NET 9.0 Support**: Full support for modern .NET applications

### Deployment Configuration
The application is configured for Render.com deployment with:
- Auto-build from GitHub repository
- Environment variables for production settings
- Optimized for free tier resources

## 🔧 Local Development

### Prerequisites
- .NET 9.0 SDK
- Any IDE (Visual Studio, VS Code, Rider)

### Running Locally
```bash
cd WebCashier
dotnet restore
dotnet run
```

- **HTTP**: `http://localhost:5182`
- **HTTPS**: `https://localhost:7068`

## 📱 Features

- ✅ Secure HTTPS payment form
- ✅ Multiple payment methods with carousel selection
- ✅ Interactive credit card widget with 3D flip animation
- ✅ Real-time form validation with custom credit card validation
- ✅ Amount suggestion buttons
- ✅ Loading overlay for better UX
- ✅ Enhanced card brand detection (including new Mastercard ranges)
- ✅ Responsive design optimized for all devices
- ✅ Modern UI with professional styling

## 🛡️ Security

- HTTPS/TLS encryption
- Anti-forgery tokens
- Input validation with Luhn algorithm
- Secure headers
- Production-ready configuration

## 📄 License

MIT License - feel free to use for your projects!
