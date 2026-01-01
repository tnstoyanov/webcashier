# Apple Pay via Nuvei Hosted Payment Page (HPP) Implementation

## Overview
This implementation adds Apple Pay as a payment method through Nuvei's Hosted Payment Page (HPP/Cashier), following the requirements specified in the tech documentation.

## Changes Made

### 1. Frontend - Carousel Item
**File:** [Views/Payment/Index.cshtml](Views/Payment/Index.cshtml#L118)

Added a new carousel item for Apple Pay Nuvei HPP with:
- Input radio button: `id="apple-pay-nuvei"`, `value="apple-pay-nuvei"`
- Apple Pay logo from Wikimedia
- Label: "Apple Pay HPP"

### 2. Frontend - Payment Form Section
**File:** [Views/Payment/Index.cshtml](Views/Payment/Index.cshtml#L419)

Added the Apple Pay Nuvei form container with:
- Amount input field (`ApplePayAmount`)
- Currency selector (`ApplePayCurrency`) with USD, EUR, GBP, BRL options
- Optional promotion code field
- Quick amount buttons (€50, €100, €200)
- Informational card with Apple Pay branding

### 3. Frontend - Form Visibility Toggle
**File:** [Views/Payment/Index.cshtml](Views/Payment/Index.cshtml#L1056)

Updated the payment method change listener to show/hide the Apple Pay form when the radio button is selected.

### 4. Backend - Service Configuration
**Files:** 
- [Services/INuveiService.cs](Services/INuveiService.cs)
- [Services/NuveiService.cs](Services/NuveiService.cs)

#### Changes to `NuveiRequest` record:
- Added `PaymentMethod` parameter (default: `"ppp_GooglePay"`)
- Allows specifying different Nuvei payment methods

#### Changes to `NuveiService.BuildPaymentForm()`:
- Uses the payment method from the request
- Dynamically sets `back_url` based on payment method:
  - Apple Pay: `/Payment?paymentMethod=apple-pay-nuvei`
  - Google Pay: `/Payment?paymentMethod=gpay`
- Passes payment method to the form fields

### 5. Backend - Controller Update
**File:** [Controllers/NuveiController.cs](Controllers/NuveiController.cs#L25)

Updated the `Create` POST action:
- Added `paymentMethod` parameter (default: `"ppp_GooglePay"`)
- Passes payment method to `NuveiRequest`
- Logs payment method in request telemetry

### 6. Frontend - JavaScript Modal and Form Submission
**File:** [Views/Payment/Index.cshtml](Views/Payment/Index.cshtml#L1720)

#### Form Submission Handler:
- Added handler for `apple-pay-nuvei` payment method
- Calls `showNuveiPreModal()` with `ppp_ApplePay` parameter

#### Modal Functions:
- `showNuveiPreModal(form, paymentMethod)`: Updated to accept payment method parameter
  - Dynamically updates modal title and message based on method (Apple Pay vs Google Pay)
  
- `initiateNuveiRequest(form, paymentMethod)`: Updated to support both payment methods
  - Determines the correct amount/currency field names based on method
  - Sends `paymentMethod` parameter to backend
  - Opens a popup window with Nuvei's hosted payment page
  - Auto-submits the HPP form with all required fields

### 7. Response Pages - Success/Error/Pending Handlers
**Files:**
- [Views/Nuvei/Success.cshtml](Views/Nuvei/Success.cshtml)
- [Views/Nuvei/Error.cshtml](Views/Nuvei/Error.cshtml)
- [Views/Nuvei/Pending.cshtml](Views/Nuvei/Pending.cshtml)

Updated Error and Pending views to:
- Detect payment method from callback parameters
- Display appropriate messaging for Apple Pay vs Google Pay
- Provide correct "Try Again" links based on payment method

### 8. Webhook Logging
**File:** [Controllers/NuveiController.cs](Controllers/NuveiController.cs#L120)

The existing `Callback` action logs:
- All webhook parameters with appropriate masking for sensitive data
- Checksum validation results
- Payment method in the callback
- Detailed diagnostic information for troubleshooting

## User Flow

### Success Path:
1. User selects "Apple Pay HPP" from payment method carousel
2. Apple Pay form section displays with amount options
3. User enters amount and currency (or clicks suggestion buttons)
4. Clicks "DEPOSIT"
5. Confirmation modal appears: "Apple Pay will open in a new tab. Continue?"
6. User clicks OK
7. Popup window opens with Nuvei's Hosted Payment Page
8. User clicks "Buy with Apple Pay"
9. Apple Pay authentication completes payment
10. Nuvei redirects to `success_url` with transaction details
11. Success page displays transaction info with:
    - Status
    - Transaction ID (PPP_TransactionID)
    - Client Unique ID
    - Payment Method
    - Amount
    - Currency

### Error Path:
1. If Nuvei declines or payment fails
2. Nuvei redirects to `error_url`
3. Error page displays:
    - Error status and reason
    - All callback details in a table
    - "Try Again" button to restart

### Pending Path:
1. If modal is closed or no webhook received within timeout
2. Redirect to `pending_url`
3. Pending page informs user transaction is processing

## API Endpoints

- **POST `/Nuvei/Create`**: Creates payment form
  - Parameters: `amount`, `currency`, `paymentMethod` (optional)
  - Returns: JSON with `success`, `formUrl`, `fields` array
  
- **POST/GET `/Nuvei/Callback`**: Webhook receiver from Nuvei
  - Logs webhook data
  - Validates checksum
  - Redirects to success/error/pending based on status
  
- **GET `/Nuvei/Success`**: Success page
- **GET `/Nuvei/Error`**: Error page
- **GET `/Nuvei/Pending`**: Pending page

## Configuration

Required Nuvei configuration in `appsettings.json` or runtime config store:
```json
{
  "Nuvei": {
    "merchant_id": "YOUR_MERCHANT_ID",
    "merchant_site_id": "YOUR_MERCHANT_SITE_ID",
    "secret_key": "YOUR_SECRET_KEY",
    "endpoint": "https://ppp-test.safecharge.com/ppp/purchase.do"
  }
}
```

## Key Implementation Details

1. **Payment Method Parameter**: Uses `ppp_ApplePay` as the Nuvei payment method code for Apple Pay
2. **Checksum Security**: Maintains existing checksum generation and validation
3. **Logging**: All requests and callbacks are logged with PII masking for sensitive fields
4. **Modal UX**: Unified modal UI for both Google Pay and Apple Pay with dynamic text
5. **Popup Handling**: Opens Nuvei HPP in popup/new tab to allow user interaction
6. **Error Handling**: Graceful fallbacks and diagnostic information in popup

## Testing Checklist

- [ ] Test carousel scrolling includes Apple Pay option
- [ ] Test form visibility toggle when Apple Pay is selected
- [ ] Test amount input validation
- [ ] Test currency selector
- [ ] Test amount suggestion buttons
- [ ] Test modal appearance with correct title/message
- [ ] Test popup opens on OK click
- [ ] Test Nuvei form submission to HPP endpoint
- [ ] Test success callback and redirect
- [ ] Test error callback and redirect
- [ ] Test pending page display
- [ ] Test webhook logging in CommLog service
- [ ] Test checksum validation
- [ ] Verify responsive design on mobile
- [ ] Test browser popup blocking handling

## Future Enhancements

- [ ] Dynamic customer details (currently hardcoded as per spec)
- [ ] Support for additional payment methods via same infrastructure
- [ ] Enhanced error messages with specific decline reasons
- [ ] Retry logic for transient failures
- [ ] Analytics/conversion tracking
- [ ] A/B testing for modal variations
