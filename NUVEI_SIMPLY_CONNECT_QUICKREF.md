# Nuvei Simply Connect - Quick Reference Guide

## What Was Implemented

Nuvei's Simply Connect payment solution has been fully integrated into the WebCashier application, allowing customers to make payments through a unified payment interface with multiple payment method options.

## Key Components

### 1. **Backend Service** (`NuveiSimplyConnectService.cs`)
   - Calls Nuvei's `/openOrder` API to initiate payment sessions
   - Securely handles merchant credentials
   - Generates cryptographic checksums for authentication
   - Logs all transactions to Render.com

### 2. **Controller Endpoint** 
   - Route: `POST /Nuvei/SimplyConnect/OpenOrder`
   - Receives amount and currency from frontend
   - Returns sessionToken needed for checkout form

### 3. **Frontend UI**
   - **Carousel Item**: New payment method option in the payment carousel
   - **Payment Form**: Dedicated form for entering amount, currency, and promo code
   - **Checkout Container**: Placeholder where Nuvei's payment form is rendered

### 4. **JavaScript Implementation**
   - Handles "Proceed to Payment" button click
   - Initiates backend session
   - Loads SafeCharge checkout.js library
   - Manages payment result callbacks
   - Handles success/failure/decline scenarios

## How It Works

```
Customer → Select "Nuvei Simply Connect" 
         → Enter Amount & Currency 
         → Click "Proceed to Payment"
         → (Backend initiates session with Nuvei)
         → Payment form displays
         → Customer selects payment method
         → Enters payment details
         → (SafeCharge processes payment)
         → Callback fires with result
         → Redirect to success/error page
```

## Configuration

The implementation uses existing Nuvei credentials in `appsettings.json`:
- **Merchant ID**: 3832456837996201334
- **Merchant Site ID**: 184063
- **Environment**: test (use "prod" for production)

## Files Changed

| File | Change Type | Description |
|------|-------------|-------------|
| `Services/NuveiSimplyConnectService.cs` | NEW | Backend service for Nuvei API calls |
| `Controllers/NuveiController.cs` | MODIFIED | Added OpenOrder endpoint |
| `Views/Payment/Index.cshtml` | MODIFIED | Added carousel item, form, and JavaScript |
| `appsettings.json` | MODIFIED | Added environment setting |
| `Program.cs` | MODIFIED | Registered NuveiSimplyConnectService |

## Testing the Implementation

1. **Start the application**
   ```bash
   cd WebCashier
   dotnet run
   ```

2. **Navigate to payment page**
   - Go to `http://localhost:5000/Payment`

3. **Select payment method**
   - Scroll to "Nuvei Simply Connect" in carousel
   - Click to select it

4. **Enter payment details**
   - Enter amount (e.g., 100)
   - Select currency (e.g., USD)
   - Click "Proceed to Payment"

5. **Verify checkout form**
   - Payment form should load
   - Select a payment method
   - Fill in payment details
   - Submit payment

6. **Check logs**
   - View Render.com logs for transaction details
   - Search for "nuvei-simply-connect" logs

## Expected Behavior

### ✅ Success Path
1. User sees "Nuvei Simply Connect" in carousel
2. User enters amount and currency
3. User clicks "Proceed to Payment"
4. Backend calls Nuvei API successfully
5. Payment form appears with available payment methods
6. User selects payment method and enters details
7. Payment is processed
8. User is redirected to success page

### ⚠️ Common Issues & Solutions

**Issue**: "Failed to initiate payment session"
- **Check**: Nuvei credentials in appsettings.json
- **Check**: Network connectivity to Nuvei API
- **Check**: Render.com logs for detailed error

**Issue**: "Payment form not loading"
- **Check**: Browser console for JavaScript errors
- **Check**: SafeCharge CDN is accessible
- **Check**: Environment setting ("int" for test, "prod" for prod)

**Issue**: "Callback not firing"
- **Check**: Payment completed on the form
- **Check**: Browser network tab for responses
- **Check**: onResult handler in JavaScript

## API Contract

### Request
```
POST /Nuvei/SimplyConnect/OpenOrder
Content-Type: application/x-www-form-urlencoded

amount=100&currency=USD&__RequestVerificationToken=...
```

### Response (Success)
```json
{
  "success": true,
  "sessionToken": "0e89136617884aa0b44bdc6b8ed6f224...",
  "orderId": 12783621111,
  "clientUniqueId": "202601252254004191218",
  "merchantId": "3832456837996201334",
  "merchantSiteId": "184063",
  "amount": 100,
  "currency": "USD"
}
```

### Response (Failure)
```json
{
  "success": false,
  "error": "Failed to initiate payment session"
}
```

## Security Features

✅ Secret key never exposed to frontend
✅ All checksums calculated server-side
✅ HTTPS enforced for all API calls
✅ Anti-forgery tokens on POST endpoints
✅ Sensitive values masked in logs
✅ Unique session token per transaction
✅ Unique client ID for idempotency

## Logging

All transactions are logged with tag `nuvei` to Render.com:

- `nuvei-simply-connect-outbound`: Request sent to Nuvei
- `nuvei-simply-connect-response`: Response received from Nuvei
- `nuvei-simply-connect-session-created`: Session successfully created
- `nuvei-simply-connect-error`: Error occurred during processing

Example log entry:
```
{
  "provider": "Nuvei Simply Connect",
  "action": "OpenOrder",
  "endpoint": "https://ppp-test.nuvei.com/ppp/api/v1/openOrder.do",
  "clientUniqueId": "202601252254004191218",
  "currency": "USD",
  "amount": "100",
  "timeStamp": "20260125225400"
}
```

## Supported Currencies

- USD (US Dollar)
- EUR (Euro)
- GBP (British Pound)
- BRL (Brazilian Real)
- And any other currency supported by Nuvei

## Supported Payment Methods

Through Nuvei's checkout form, customers can use:
- Credit/Debit Cards (Visa, Mastercard, Amex, etc.)
- Digital Wallets (Apple Pay, Google Pay, etc.)
- Local Payment Methods (varies by region)
- Bank Transfers
- E-wallets

## Production Deployment

To use production credentials:

1. Update `appsettings.json`:
   ```json
   "Nuvei": {
     "merchant_id": "[production_id]",
     "merchant_site_id": "[production_site_id]",
     "secret_key": "[production_secret]",
     "environment": "prod"
   }
   ```

2. Change frontend environment:
   ```javascript
   env: "prod" // instead of "int"
   ```

3. Update API endpoints to production URLs

## Support

For issues or questions:
- Check the comprehensive implementation document: `NUVEI_SIMPLY_CONNECT_IMPLEMENTATION.md`
- Review Nuvei API documentation: https://docs.nuvei.com/api/main/indexMain_v1_0.html
- Check Render.com logs for specific error messages
- Verify all configuration settings in appsettings.json

---
**Implementation Date**: January 25, 2026
**Status**: ✅ Complete and Ready for Testing
