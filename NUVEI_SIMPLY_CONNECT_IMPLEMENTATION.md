# Nuvei Simply Connect Implementation

## Overview

This document describes the implementation of Nuvei's Simply Connect payment solution in the WebCashier application. Simply Connect provides a unified payment interface that allows customers to choose from multiple payment methods including credit cards, digital wallets, and local payment options.

## Implementation Date
January 25, 2026

## Architecture Components

### 1. Backend Service: NuveiSimplyConnectService
**File:** `WebCashier/Services/NuveiSimplyConnectService.cs`

#### Purpose
Handles all backend communication with Nuvei's API, specifically the `/openOrder` endpoint. This keeps sensitive credentials (secret key) secure on the server side.

#### Key Methods
- `InitiateSessionAsync(amount, currency, clientUniqueId)`: 
  - Calls Nuvei's `/openOrder` API
  - Generates SHA256 checksum for authentication
  - Returns `OpenOrderResponse` containing sessionToken
  - Logs all communication to Render.com logs via CommLogService

#### Configuration Requirements
- `Nuvei:merchant_id`: "3832456837996201334"
- `Nuvei:merchant_site_id`: "184063"
- `Nuvei:secret_key`: "[secret key from Nuvei]"
- `Nuvei:environment`: "test" (or "prod" for production)

#### API Endpoints Used
- Test: `https://ppp-test.nuvei.com/ppp/api/v1/openOrder.do`
- Production: `https://ppp.nuvei.com/ppp/api/v1/openOrder.do`

### 2. Controller Endpoint
**File:** `WebCashier/Controllers/NuveiController.cs`

#### Endpoint
`POST /Nuvei/SimplyConnect/OpenOrder`

#### Parameters
- `amount` (decimal): Payment amount
- `currency` (string): 3-letter currency code (USD, EUR, GBP, BRL, etc.)
- `__RequestVerificationToken` (anti-forgery token)

#### Response
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

### 3. Frontend UI Components
**File:** `WebCashier/Views/Payment/Index.cshtml`

#### Added Elements

##### Carousel Item (Step 1)
```html
<div class="carousel-item" id="nuvei-simply-connect" tabindex="0">
    <input type="radio" id="nuvei-simply-connect-radio" name="PaymentMethod" value="nuvei-simply-connect" />
    <label for="nuvei-simply-connect-radio" class="method-option">
        <div class="nuvei-simply-connect-icon">
            <img src="https://upload.wikimedia.org/wikipedia/commons/e/ee/Nuvei_Organization_logo.png" alt="Nuvei Simply Connect" width="52" height="35" />
        </div>
        <span class="payment-brand">Nuvei Simply Connect</span>
    </label>
</div>
```

##### Payment Form Container (Step 3)
- Added `#nuvei-simply-connect-details` container with:
  - Amount input (`#nuvei-sc-amount`)
  - Currency dropdown (`#nuvei-sc-currency`)
  - Promotion code field
  - "Proceed to Payment" button
  - Checkout placeholder (`#checkout`)

##### External Scripts and Styles
```html
<script id="nuvei-scplugin-script" type="text/javascript" defer src="https://cdn.safecharge.com/safecharge_resources/v1/checkout/checkout.js"></script>
<link id="nuveiCheckoutCss" type="text/css" rel="stylesheet" href="https://cdn.safecharge.com/safecharge_resources/v1/checkout/cc.css">
```

### 4. JavaScript Implementation
**File:** `WebCashier/Views/Payment/Index.cshtml` (Scripts section)

#### Features Implemented

##### Session Initialization
- Listens to "Proceed to Payment" button click
- Collects amount and currency
- Calls `/Nuvei/SimplyConnect/OpenOrder` endpoint
- Stores `sessionToken` for checkout initialization

##### Checkout Initialization
- Calls `window.checkout()` method from SafeCharge library
- Configures payment environment
- Handles all callback events:
  - `onReady`: Form loaded successfully
  - `onSelectPaymentMethod`: User selected a payment method
  - `onResult`: Payment transaction completed
  - `onDeclineRecovery`: Payment declined
  - `prePayment`: Pre-payment validation hook

##### Callback Handlers
- **APPROVED**: Redirects to success page
- **DECLINED**: Shows decline message with error code
- **ERROR**: Shows error message to user
- **Other**: Logs pending or other statuses

#### Configuration Parameters
```javascript
{
  sessionToken: "[from backend]",
  merchantSiteId: "184063",
  merchantId: "3832456837996201334",
  country: "US",
  currency: "[user selected]",
  amount: "[user entered]",
  renderTo: "#checkout",
  env: "int", // test environment
  theme: "tiles", // payment method presentation
  locale: "en",
  savePM: "true", // save payment method for future
  showCardLogos: true,
  alwaysCollectCvv: "true"
}
```

## Configuration

### appsettings.json
```json
"Nuvei": {
  "merchant_id": "3832456837996201334",
  "merchant_site_id": "184063",
  "secret_key": "puT8KQYqIbbQDHN5cQNAlYyuDedZxRYjA9WmEsKq1wrIPhxQqOx77Ep1uOA7sUde",
  "endpoint": "https://ppp-test.safecharge.com/ppp/purchase.do",
  "environment": "test"
}
```

### Dependency Injection (Program.cs)
```csharp
builder.Services.AddScoped<NuveiSimplyConnectService>();
```

## Logging & Monitoring

All communication is logged to Render.com logs with specific tags for debugging:

### Outbound Request
```
nuvei-simply-connect-outbound
- provider, action, endpoint, merchantId, currency, amount, timeStamp, checksum
```

### Session Created
```
nuvei-simply-connect-session-created
- clientUniqueId, orderId, amount, currency
```

### Response
```
nuvei-simply-connect-response
- status, sessionToken (masked), orderId
```

### Errors & Exceptions
```
nuvei-simply-connect-error
- action, error message, stack trace
```

## Payment Flow (Step-by-Step)

### Step 1: User Selection
1. Customer selects "Nuvei Simply Connect" from carousel
2. Enters amount and currency
3. Clicks "Proceed to Payment"

### Step 2: Session Initiation (Backend)
1. Frontend sends POST to `/Nuvei/SimplyConnect/OpenOrder`
2. Backend validates credentials from appsettings
3. Generates checksum using SHA256(secret_key + concatenated_values)
4. Calls Nuvei `/openOrder` API
5. Receives `sessionToken` and `orderId`
6. Logs transaction to Render.com
7. Returns session data to frontend

### Step 3: Checkout Form Display (Frontend)
1. JavaScript receives `sessionToken`
2. Loads SafeCharge checkout.js library
3. Calls `window.checkout()` with session data
4. Form renders in `#checkout` container
5. Customer sees available payment methods

### Step 4: Payment Processing
1. Customer selects payment method
2. Enters payment details
3. Submits payment
4. SafeCharge processes transaction
5. Callback fires with result

### Step 5: Result Handling
1. **If APPROVED**: Redirect to `/Payment/Success`
2. **If DECLINED**: Show error, allow retry
3. **If ERROR**: Show error message
4. **If Pending**: Handle accordingly

## Testing Checklist

- [ ] Application compiles without errors
- [ ] NuveiSimplyConnectService registered in DI
- [ ] Carousel item displays correctly
- [ ] Payment form container hidden by default
- [ ] Form shows when "Nuvei Simply Connect" selected
- [ ] "Proceed to Payment" button calls correct endpoint
- [ ] openOrder endpoint returns valid session token
- [ ] Checkout.js library loads from CDN
- [ ] Payment form renders in checkout container
- [ ] Payment methods display correctly
- [ ] Customer can select and enter payment details
- [ ] Callback handlers fire correctly
- [ ] Success/error/decline states handled properly
- [ ] Logs appear in Render.com logs
- [ ] Mobile responsive layout working

## Environment Variables

Optional environment variables for production:

```bash
# Set to "prod" for production environment (switches API endpoints)
NUVEI_ENVIRONMENT=test

# Override Nuvei credentials if using runtime config
NUVEI_MERCHANT_ID=your_merchant_id
NUVEI_MERCHANT_SITE_ID=your_site_id
NUVEI_SECRET_KEY=your_secret_key
```

## Security Considerations

1. **Secret Key Protection**: Secret key never exposed to frontend
2. **Server-Side Checksum**: All checksums calculated on backend
3. **HTTPS Only**: All API calls must use HTTPS
4. **Anti-Forgery**: POST endpoints protected with CSRF tokens
5. **Logging**: Sensitive values (tokens, checksums) masked or redacted in logs
6. **Session Token**: Short-lived, unique per transaction
7. **Client Unique ID**: Unique per transaction for idempotency

## Troubleshooting

### "Failed to initiate payment session"
- Check Nuvei credentials in appsettings
- Verify merchant account is active
- Check Render.com logs for API response errors

### "Payment form not loading"
- Verify SafeCharge CDN is accessible
- Check browser console for JavaScript errors
- Ensure sessionToken is valid
- Check environment setting ("int" vs "prod")

### "Callback not firing"
- Verify payment completed on SafeCharge form
- Check browser network tab for response
- Review JavaScript console for errors
- Check that onResult handler is properly defined

### Logs not appearing
- Verify CommLogService is configured
- Check Render.com log streaming is enabled
- Verify environment variable `COMM_LOGS_ENABLED=true`

## References

- Nuvei Simply Connect Documentation: https://docs.nuvei.com/api/main/indexMain_v1_0.html
- SafeCharge Checkout.js API: https://cdn.safecharge.com/safecharge_resources/v1/checkout/checkout.js
- Implementation Spec: Nuvei Simply Connect Implementation.md (attached)

## Future Enhancements

1. Add DCC (Dynamic Currency Conversion) support
2. Implement card installment options
3. Add pre-population of customer data
4. Support for UPO (User Payment Option) - save cards
5. Implement decline recovery UI
6. Add support for more payment locales
7. Webhook endpoint for asynchronous payment notifications
8. Reconciliation report generation

## Files Modified

1. `WebCashier/Services/NuveiSimplyConnectService.cs` - NEW
2. `WebCashier/Controllers/NuveiController.cs` - Added OpenOrder endpoint
3. `WebCashier/Views/Payment/Index.cshtml` - Added UI and JavaScript
4. `WebCashier/appsettings.json` - Added environment setting
5. `WebCashier/Program.cs` - Registered NuveiSimplyConnectService

---
**Implementation Status:** ✅ Complete
**Date:** January 25, 2026
**Version:** 1.0
