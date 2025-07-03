#!/bin/bash

# Azure Static Web Apps Free Deployment
echo "🌐 Deploying to Azure Static Web Apps (Free Tier)"
echo "=================================================="

RESOURCE_GROUP="WebCashier-RG"
APP_NAME="webcashier-static-$(date +%s)"
LOCATION="eastus2"

echo "📋 Configuration:"
echo "   Resource Group: $RESOURCE_GROUP"
echo "   App Name: $APP_NAME"
echo "   Location: $LOCATION"
echo ""

# Test Azure CLI
echo "✅ Testing Azure CLI..."
if ! az account show > /dev/null 2>&1; then
    echo "❌ Please login to Azure CLI first: az login"
    exit 1
fi
echo "✅ Azure CLI OK"

# Check if Static Web Apps extension is installed
echo "🔧 Installing Azure Static Web Apps CLI extension..."
az extension add --name staticwebapp 2>/dev/null || az extension update --name staticwebapp

# Ensure resource group exists
echo "✅ Checking resource group..."
if ! az group show --name "$RESOURCE_GROUP" > /dev/null 2>&1; then
    echo "⚠️  Creating resource group..."
    az group create --name "$RESOURCE_GROUP" --location "$LOCATION"
fi

# Build the application for static hosting
echo "🔨 Building application for static deployment..."

# Create a static build script
cat > build-static.sh << 'EOF'
#!/bin/bash

# Clean previous builds
rm -rf ./static-build
mkdir -p ./static-build

# Build the .NET app
dotnet publish -c Release -o ./temp-publish

# Copy static files
cp -r ./wwwroot/* ./static-build/ 2>/dev/null || true

# Create a simple HTML payment form (since we can't run server-side code)
cat > ./static-build/index.html << 'HTML'
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>WebCashier - Payment Processing</title>
    <link href="css/site.css" rel="stylesheet" />
    <link href="lib/bootstrap/dist/css/bootstrap.min.css" rel="stylesheet" />
    <style>
        .payment-form {
            max-width: 500px;
            margin: 50px auto;
            padding: 30px;
            border: 1px solid #ddd;
            border-radius: 10px;
            background: #f9f9f9;
        }
        .form-header {
            text-align: center;
            margin-bottom: 30px;
        }
        .btn-payment {
            background: #28a745;
            border: none;
            padding: 12px 30px;
            font-size: 16px;
        }
        .security-notice {
            font-size: 12px;
            color: #666;
            margin-top: 20px;
            text-align: center;
        }
    </style>
</head>
<body>
    <div class="container">
        <div class="payment-form">
            <div class="form-header">
                <h2>💳 WebCashier Payment</h2>
                <p class="text-muted">Secure Payment Processing</p>
            </div>
            
            <form id="paymentForm">
                <div class="mb-3">
                    <label for="cardNumber" class="form-label">Card Number</label>
                    <input type="text" class="form-control" id="cardNumber" placeholder="1234 5678 9012 3456" required>
                </div>
                
                <div class="row">
                    <div class="col-md-6 mb-3">
                        <label for="expiryDate" class="form-label">Expiry Date</label>
                        <input type="text" class="form-control" id="expiryDate" placeholder="MM/YY" required>
                    </div>
                    <div class="col-md-6 mb-3">
                        <label for="cvv" class="form-label">CVV</label>
                        <input type="text" class="form-control" id="cvv" placeholder="123" required>
                    </div>
                </div>
                
                <div class="mb-3">
                    <label for="cardName" class="form-label">Cardholder Name</label>
                    <input type="text" class="form-control" id="cardName" placeholder="John Doe" required>
                </div>
                
                <div class="mb-3">
                    <label for="amount" class="form-label">Amount</label>
                    <div class="input-group">
                        <span class="input-group-text">$</span>
                        <input type="number" class="form-control" id="amount" placeholder="0.00" step="0.01" required>
                    </div>
                </div>
                
                <div class="mb-3">
                    <label for="email" class="form-label">Email</label>
                    <input type="email" class="form-control" id="email" placeholder="john@example.com" required>
                </div>
                
                <button type="submit" class="btn btn-success btn-payment w-100">
                    🔒 Process Payment
                </button>
                
                <div class="security-notice">
                    <p>🔐 Your payment information is encrypted and secure.<br>
                    This is a demo application for educational purposes.</p>
                </div>
            </form>
        </div>
    </div>
    
    <script src="lib/jquery/dist/jquery.min.js"></script>
    <script src="lib/bootstrap/dist/js/bootstrap.bundle.min.js"></script>
    <script>
        document.getElementById('paymentForm').addEventListener('submit', function(e) {
            e.preventDefault();
            
            // Simulate payment processing
            const submitBtn = document.querySelector('.btn-payment');
            const originalText = submitBtn.innerHTML;
            
            submitBtn.innerHTML = '⏳ Processing...';
            submitBtn.disabled = true;
            
            setTimeout(() => {
                alert('✅ Payment processed successfully!\n\nNote: This is a demo application. No actual payment was processed.');
                submitBtn.innerHTML = originalText;
                submitBtn.disabled = false;
                document.getElementById('paymentForm').reset();
            }, 2000);
        });
        
        // Format card number
        document.getElementById('cardNumber').addEventListener('input', function(e) {
            let value = e.target.value.replace(/\s/g, '').replace(/\D/g, '');
            value = value.replace(/(\d{4})(?=\d)/g, '$1 ');
            e.target.value = value;
        });
        
        // Format expiry date
        document.getElementById('expiryDate').addEventListener('input', function(e) {
            let value = e.target.value.replace(/\D/g, '');
            if (value.length >= 2) {
                value = value.substring(0, 2) + '/' + value.substring(2, 4);
            }
            e.target.value = value;
        });
        
        // CVV format
        document.getElementById('cvv').addEventListener('input', function(e) {
            e.target.value = e.target.value.replace(/\D/g, '').substring(0, 4);
        });
    </script>
</body>
</html>
HTML

# Create payment page
cp ./static-build/index.html ./static-build/payment.html

# Create a simple API simulation page
cat > ./static-build/api.html << 'HTML'
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>WebCashier API - Status</title>
    <link href="lib/bootstrap/dist/css/bootstrap.min.css" rel="stylesheet" />
</head>
<body>
    <div class="container mt-5">
        <div class="text-center">
            <h1>🚀 WebCashier API</h1>
            <p class="lead">Payment Processing Service</p>
            <div class="alert alert-success">
                <h4>✅ Service Status: Online</h4>
                <p>All payment processing services are operational.</p>
            </div>
            <a href="index.html" class="btn btn-primary">Go to Payment Form</a>
        </div>
    </div>
</body>
</html>
HTML

echo "✅ Static build complete"
EOF

chmod +x build-static.sh
./build-static.sh

# Create Static Web App
echo "🚀 Creating Azure Static Web App..."
az staticwebapp create \
    --name "$APP_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --location "$LOCATION" \
    --source "./static-build" \
    --branch main \
    --app-location "/" \
    --output-location "/"

if [ $? -eq 0 ]; then
    echo ""
    echo "🎉 STATIC WEB APP DEPLOYMENT SUCCESSFUL!"
    echo "========================================"
    
    # Get the URL
    URL=$(az staticwebapp show --name "$APP_NAME" --resource-group "$RESOURCE_GROUP" --query "defaultHostname" --output tsv)
    
    echo "🌐 Your app is available at:"
    echo "   https://$URL"
    echo "💳 Payment form:"
    echo "   https://$URL/payment.html"
    echo ""
    echo "📊 App details:"
    echo "   Name: $APP_NAME"
    echo "   Resource Group: $RESOURCE_GROUP"
    echo "   Type: Static Web App (Free Tier)"
else
    echo "❌ Static Web App deployment failed!"
    exit 1
fi
