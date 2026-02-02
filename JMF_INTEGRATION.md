# JM Financial Integration Setup Guide

## Overview
JM Financial's Hosted Payment Page (HPP) integration has been implemented into the WebCashier payment platform. This allows users to make secure payments through JM Financial's hosted payment interface.

## Configuration

### Required Settings
Add the following settings to your `appsettings.json` or environment variables:

```json
{
  "JMF": {
    "MerchantKey": "your-merchant-key-here",
    "ApiPassword": "your-api-password-here",
    "ApiEndpoint": "https://checkout.jmfinancialkw.com/api/v1/session"
  }
}
```

### Configuration Parameters

| Parameter | Description | Required |
|-----------|-------------|----------|
| `MerchantKey` | Your JM Financial merchant key (UUID format) | Yes |
| `ApiPassword` | Your JM Financial API password | Yes |
| `ApiEndpoint` | JM Financial API endpoint URL | No (defaults to production) |

### Environment Variables
You can also configure via environment variables:

```bash
export JMF__MerchantKey="your-merchant-key"
export JMF__ApiPassword="your-api-password"
export JMF__ApiEndpoint="https://checkout.jmfinancialkw.com/api/v1/session"
```

## Implementation Details

### Components Added

#### 1. **Services**
- `IJMFService` - Interface for JM Financial payment operations
- `JMFService` - Implementation of JM Financial API integration

#### 2. **Controllers**
- `JMFController` - Handles payment creation and callbacks
  - `POST /JMF/Create` - Creates a payment session and returns redirect URL
  - `GET /JMF/Success` - Success redirect page after payment
  - `GET /JMF/Cancel` - Cancel redirect page if user abandons payment

#### 3. **Views**
- Payment carousel item to select JM Financial as payment method
- JM Financial payment form with customer name, email, amount, and currency fields
- Success page (`/Views/JMF/Success.cshtml`)
- Cancel page (`/Views/JMF/Cancel.cshtml`)

#### 4. **Frontend**
- JavaScript handler `handleJMFPayment()` for form submission
- Integrated with existing payment modal system
- Loading overlay during payment session creation

## API Flow

```
1. User selects "JM Ak HPP" payment method
2. User fills in:
   - Amount ($100 - $199.99)
   - Currency (USD)
   - Customer Name
   - Customer Email
3. User clicks "DEPOSIT"
4. handleJMFPayment() calls POST /JMF/Create
5. JMFService calls JM Financial API with:
   - Order details (number, amount, currency, description)
   - Customer information
   - Calculated hash for authentication
   - Redirect URLs (success, cancel)
6. JM Financial returns redirect URL
7. User is redirected to JM Financial's hosted payment page
8. After payment, user is redirected back to success/cancel URL
```

## Hash Calculation

The hash is calculated as follows:
```
hash = SHA1(MD5(UPPERCASE(order_number + amount + currency + description + api_password)))
```

Implementation:
1. Concatenate: `order_number + amount + currency + description + api_password`
2. Convert to uppercase
3. Calculate MD5 hash
4. Calculate SHA1 hash of the MD5 result
5. Convert final hash to lowercase hex string

## Testing

### Test Credentials
Use the following test data to verify the integration:

**Test Payment Details:**
- Amount: 150.00
- Currency: USD
- Customer Name: Test User
- Customer Email: test@example.com

### Test Workflow
1. Navigate to the Payment page
2. Select "JM Ak HPP" from the payment methods carousel
3. Enter test details and click "DEPOSIT"
4. Confirm redirect to JM Financial's payment page
5. Complete test payment
6. Verify redirect to success or cancel page

## Logging

All JM Financial operations are logged with prefix `[JMF]` for easy debugging:

- `jmf-create-session-inbound` - Payment session creation request
- `jmf-api-response` - API response status
- `jmf-create-success` - Successful payment session creation
- `jmf-create-error` - Error creating payment session
- `jmf-success` - User reached success page
- `jmf-cancel` - User reached cancel page

View logs in the Communication Log section of the admin panel.

## Error Handling

Common error scenarios and their handling:

| Error | Cause | Resolution |
|-------|-------|-----------|
| "Missing JMF configuration" | MerchantKey or ApiPassword not set | Configure credentials in appsettings.json |
| "Failed to parse API response" | Invalid JSON from JM Financial | Verify API endpoint URL and credentials |
| "No redirect URL in response" | JM Financial API error | Check error details in logs |
| "HTTP 400/401" | Authentication failure | Verify MerchantKey and ApiPassword |

## Security Considerations

1. **Hash Validation** - All requests are authenticated via SHA1/MD5 hash
2. **HTTPS** - All communication with JM Financial is encrypted
3. **Anti-Forgery** - CSRF tokens validated on form submission
4. **Sensitive Data** - Passwords and sensitive fields are masked in logs

## Support

For issues or questions:
1. Check the Communication Log for detailed error messages
2. Verify configuration is correct
3. Review hash calculation implementation
4. Contact JM Financial support with transaction details

## References

- JM Financial Documentation: https://jmfinancialkw.com
- API Endpoint: https://checkout.jmfinancialkw.com/api/v1/session
- Order Number Range: 3000000 - 3999999 (automatically generated)
