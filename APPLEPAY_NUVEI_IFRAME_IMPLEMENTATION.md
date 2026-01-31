# Apple Pay Nuvei IFrame Implementation

## Overview
Apple Pay payment support has been added to WebCashier through Nuvei's IFrame integration. This implementation allows customers to make payments using Apple Pay through an embedded payment form rather than being redirected to a hosted payment page.

## Key Features
- **Embedded IFrame Integration**: Payment form is embedded directly on the payment page
- **Apple Pay Support**: Customers can use Apple Pay for quick checkout
- **Responsive Design**: Payment form adapts to different screen sizes
- **Session Management**: Secure session handling for payment processing

## Technical Architecture

### Components Added

#### 1. Frontend (Views/Payment/Index.cshtml)
- **New Carousel Item**: "Apple Pay IFrame" option added to payment method carousel
- **Form Container**: Dedicated form for Apple Pay IFrame with amount/currency fields
- **IFrame Container**: Div container for embedding Nuvei's payment form
- **Event Handlers**: JavaScript functions to manage form submission and IFrame loading

#### 2. Backend (Controllers/NuveiController.cs)
- **New Endpoint**: `POST /Nuvei/ApplePay/GetIFrameUrl`
  - Generates complete payment URL with all required parameters
  - Includes parent_url parameter for Apple Pay IFrame support
  - Returns both iframe URL and form metadata

#### 3. Layout (_Layout.cshtml)
- **Nuvei Apple Pay Script**: `sc_applepay.min.js` loaded from Nuvei CDN
  - Acts as proxy between IFrame and parent page
  - Enables cross-origin communication

## Implementation Details

### User Flow
1. User selects "Apple Pay IFrame" from payment method carousel
2. Form container shows with amount and currency fields
3. User enters amount, selects currency, and clicks "Deposit"
4. `handleApplePayNuveiIFrame()` function is triggered
5. Backend generates IFrame URL via `/Nuvei/ApplePay/GetIFrameUrl`
6. Payment form loads in embedded IFrame
7. After payment, user is redirected to success/error/pending URL

### URL Generation
The endpoint builds a GET request with parameters:
```
https://ppp-test.safecharge.com/ppp/purchase.do?
  merchant_id=...
  merchant_site_id=...
  time_stamp=...
  currency=EUR
  amount=100
  parent_url=https://yourapp.com
  ...other parameters...
```

### IFrame Not Nested Rule
According to Nuvei's requirements:
- The Nuvei IFrame must NOT be nested in another IFrame
- It must be directly on the parent page
- `sc_applepay.min.js` handles cross-origin communication

## Files Modified

### 1. `/WebCashier/Views/Payment/Index.cshtml`
- Added carousel item for "Apple Pay IFrame"
- Added form container `#apple-pay-nuvei-iframe-details`
- Added IFrame container `#apple-pay-nuvei-iframe-container`
- Added form method switch case for `apple-pay-nuvei-iframe`
- Added form submission handler for Apple Pay IFrame
- Added `handleApplePayNuveiIFrame()` JavaScript function
- Exposed function globally via `window.handleApplePayNuveiIFrame`

### 2. `/WebCashier/Controllers/NuveiController.cs`
- Added `GetApplePayIFrameUrl()` endpoint
- Generates complete IFrame URL with all Nuvei parameters
- Includes logging and error handling
- Returns JSON response with iframeUrl and metadata

### 3. `/WebCashier/Views/Shared/_Layout.cshtml`
- Added Nuvei Apple Pay support script: `sc_applepay.min.js`
- Loaded from CDN: `https://cdn.safecharge.com/safecharge_resources/v1/sc_applepay.min.js`

## Configuration

### Nuvei Merchant Details (Embedded in NuveiService)
- merchant_id
- merchant_site_id
- secret_key (for checksum calculation)

### Payment Parameters
- amount: Decimal amount entered by user
- currency: Selected currency (USD, EUR, GBP, BRL)
- parent_url: Application base URL (for IFrame communication)

## Security Features

1. **Anti-CSRF Token**: Form submission includes CSRF token validation
2. **Session Management**: Payment session stored securely
3. **Checksum Verification**: Request signature validates merchant and amount
4. **HTTPS Only**: Enforced in production environment
5. **Redirect URLs**: Callback URLs must be whitelisted in Nuvei configuration

## Testing Checklist

- [ ] Carousel displays "Apple Pay IFrame" option
- [ ] Form loads when selecting "Apple Pay IFrame"
- [ ] Amount and currency fields are required
- [ ] Form validation works
- [ ] IFrame loads payment form successfully
- [ ] sc_applepay.min.js script loads
- [ ] Payment can be completed in IFrame
- [ ] Success/error/pending redirects work
- [ ] Logging captures all transactions
- [ ] Mobile responsive design works

## API Response Example

### Success Response
```json
{
  "success": true,
  "iframeUrl": "https://ppp-test.safecharge.com/ppp/purchase.do?merchant_id=...",
  "formUrl": "https://ppp-test.safecharge.com/ppp/purchase.do",
  "parentUrl": "https://yourapp.com",
  "amount": 100,
  "currency": "EUR"
}
```

### Error Response
```json
{
  "success": false,
  "error": "Error message describing what went wrong"
}
```

## Browser Support

- Chrome 88+
- Safari 14+ (Apple Pay supported)
- Firefox 86+
- Edge 88+

## Troubleshooting

### IFrame Not Loading
1. Check browser console for CORS errors
2. Verify `sc_applepay.min.js` is loaded
3. Check Nuvei merchant configuration
4. Verify parent_url matches allowed domains

### Payment Not Completing
1. Check amount and currency are valid
2. Verify merchant credentials
3. Check callback URLs are configured in Nuvei dashboard
4. Review server logs for payment status

### Cross-Origin Issues
1. Ensure sc_applepay.min.js is loaded on parent page
2. Verify parent_url parameter is set correctly
3. Check Nuvei IFrame configuration for domain whitelist

## Related Documentation

- Nuvei Apple Pay Integration: `APPLE_PAY_NUVEI_HPP_IMPLEMENTATION.md`
- Payment Method Integration: `NUVEI_IMPLEMENTATION_VERIFICATION.md`
- Simply Connect Implementation: `NUVEI_SIMPLY_CONNECT_IMPLEMENTATION.md`

## Future Enhancements

1. Add payment method selection UI within IFrame
2. Implement tokenization for saved cards
3. Add 3D Secure support
4. Support additional currencies
5. Implement recurring payment support
6. Add analytics tracking for payment funnel

## Support

For issues or questions:
- Check Nuvei's official documentation
- Review application logs in `/app/logs/`
- Contact Nuvei support for API-related issues
