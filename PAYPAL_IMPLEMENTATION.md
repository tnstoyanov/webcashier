# PayPal REST API v2 Implementation Guide

## Overview
This document describes the PayPal REST API v2 integration implemented in the WebCashier payment processing application. The implementation follows PayPal's OAuth 2.0 authentication and order management flow.

## Architecture Components

### 1. Configuration
**File:** [appsettings.json](appsettings.json)

PayPal credentials and endpoints are configured in the `PayPal` section:

```json
"PayPal": {
  "ApiUrl": "https://api.sandbox.paypal.com",
  "ClientId": "AQYGa8hZX7mmNW-qvZtJtOGDp6PJQiI7cwIrVimmmZzxgdpOyryJcfNCOEeodoSvc4oQ_0iZ8hcx1jvQ",
  "ClientSecret": "EMbkIni1mkBa0Ygrg2uYDpB2Sc5k62PlnBmlAZzoHuKYyUcqNy7Wdda0buWbYTgdOle2PGt7yGgiKl-u",
  "MerchantId": "3WT6ZKGS5YSV8",
  "BrandName": "Finansero",
  "ReturnUrl": "https://webcashier.onrender.com/PayPal/Return",
  "CancelUrl": "https://webcashier.onrender.com/PayPal/Cancel",
  "NotifyUrl": "https://webcashier.onrender.com/api/paypal/notification"
}
```

**Configuration Values:**
- `ApiUrl`: PayPal API endpoint (sandbox or production)
- `ClientId` & `ClientSecret`: OAuth 2.0 credentials from PayPal Developer Console
- `MerchantId`: Merchant account ID for payment receiving
- `BrandName`: Brand name displayed to customers during checkout
- `ReturnUrl`: Redirect after customer approves payment
- `CancelUrl`: Redirect if customer cancels payment
- `NotifyUrl`: Webhook endpoint for payment notifications

### 2. Data Models
**Directory:** [Models/PayPal](Models/PayPal)

#### PayPalOAuthResponse.cs
OAuth 2.0 token response from PayPal authentication endpoint.

**Key Fields:**
- `AccessToken`: Bearer token for API calls
- `ExpiresIn`: Token expiration time in seconds
- `TokenType`: Always "Bearer"

#### PayPalOrderRequest.cs
Request payload for creating new payment orders.

**Key Classes:**
- `PayPalOrderRequest`: Main order creation request
- `PurchaseUnit`: Order line item with amount and description
- `ExperienceContext`: Controls buyer checkout experience (brand, URLs, shipping preference)

#### PayPalOrderResponse.cs
Complete order response with status and payment details.

**Key Classes:**
- `PayPalOrderResponse`: Order status and links
- `Capture`: Captured payment details
- `Payer`: Customer information

#### PayPalWebhook.cs
Webhook event structure for payment notifications.

### 3. Service Layer
**File:** [Services/IPayPalService.cs](Services/IPayPalService.cs) & [Services/PayPalService.cs](Services/PayPalService.cs)

#### Interface: IPayPalService

```csharp
public interface IPayPalService
{
    Task<string?> GetAccessTokenAsync();
    Task<PayPalOrderResponse?> CreateOrderAsync(decimal amount, string currency, string description, string referenceId);
    Task<PayPalOrderResponse?> CaptureOrderAsync(string orderId);
    Task<PayPalOrderResponse?> GetOrderAsync(string orderId);
}
```

#### Implementation: PayPalService

**Key Features:**

1. **OAuth Token Management**
   - Automatic token caching with expiration tracking
   - Tokens cached until 1 minute before expiry
   - Automatic refresh on demand

2. **Order Creation**
   - Creates CAPTURE-intent orders (immediate capture after approval)
   - Generates unique reference IDs per order
   - Includes payer experience context (brand name, checkout preference, URLs)

3. **Order Capture**
   - Captures approved orders
   - Transitions order from APPROVED to COMPLETED state
   - Retrieves capture transaction details

4. **Order Retrieval**
   - Gets current order status
   - Retrieves payer information
   - Fetches capture/authorization details

**Render.com Logging:**
All API calls are logged via `ICommLogService` with event categories:
- `paypal-oauth-request`: OAuth token request
- `paypal-oauth-success`: Successful token acquisition
- `paypal-order-create`: Order creation request
- `paypal-order-created`: Order successfully created
- `paypal-order-capture`: Order capture request
- `paypal-captured`: Order successfully captured
- `paypal-order-retrieved`: Order details fetched
- `paypal-*-error`: Error conditions with HTTP status and error details
- `paypal-*-exception`: Unexpected exceptions with stack traces

### 4. Controller
**File:** [Controllers/PayPalController.cs](Controllers/PayPalController.cs)

Handles all PayPal payment operations and callbacks.

#### Endpoints:

##### POST /PayPal/Create
Creates a new PayPal order and returns the approval URL.

**Parameters:**
- `amount` (decimal): Payment amount
- `currency` (string): ISO currency code (USD, EUR, etc.)
- `description` (string): Order description

**Response:**
```json
{
  "success": true,
  "orderId": "3C679910GA908715U",
  "approvalUrl": "https://www.sandbox.paypal.com/checkoutnow?token=EC-...",
  "status": "CREATED"
}
```

##### POST /PayPal/Capture
Captures a previously approved order.

**Parameters:**
- `orderId` (string): PayPal order ID to capture

**Response:**
```json
{
  "success": true,
  "orderId": "3C679910GA908715U",
  "status": "COMPLETED",
  "captures": [
    {
      "Id": "1AB23456CD789012E",
      "Status": "COMPLETED",
      "Amount": "10.00"
    }
  ]
}
```

##### GET /PayPal/Return
Callback endpoint after customer approves payment on PayPal.

**Query Parameters:**
- `token` (string): PayPal order ID

**Behavior:**
- Retrieves order details
- Stores order info in session
- Redirects to success page

##### GET /PayPal/Cancel
Callback endpoint if customer cancels payment.

**Query Parameters:**
- `token` (string): PayPal order ID

**Behavior:**
- Logs cancellation event
- Redirects to payment selection page

##### POST /PayPal/Notification
Webhook endpoint for PayPal payment notifications.

**Headers:**
- `X-Event-Type`: Webhook event type (PAYMENT.CAPTURE.COMPLETED, etc.)

**Behavior:**
- Logs all webhook events
- Returns 200 OK to acknowledge receipt

### 5. Service Registration
**File:** [Program.cs](Program.cs)

PayPal service is registered with HttpClient configuration:

```csharp
builder.Services.AddHttpClient<IPayPalService, PayPalService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "WebCashier/1.0");
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    handler.SslProtocols = System.Security.Authentication.SslProtocols.Tls12;
    handler.CheckCertificateRevocationList = false;
    if (builder.Environment.IsDevelopment())
    {
        handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
    }
    return handler;
});
```

## API Flow

### Order Creation & Capture Flow

```
1. User initiates payment
   ↓
2. POST /PayPal/Create
   - Service calls PayPal OAuth endpoint for access token
   - Creates order via POST /v2/checkout/orders
   - Returns approval URL to client
   ↓
3. Client redirects user to PayPal approval URL
   ↓
4. User approves payment on PayPal
   ↓
5. PayPal redirects user to GET /PayPal/Return?token={orderId}
   ↓
6. Controller retrieves order details via /v2/checkout/orders/{orderId}
   ↓
7. POST /PayPal/Capture (via frontend)
   - Service calls POST /v2/checkout/orders/{orderId}/capture
   - Order status changes to COMPLETED
   - Returns capture details
   ↓
8. Payment confirmed, order processed
```

## Logging & Monitoring (Render.com)

All PayPal operations are logged through the `ICommLogService` which:

1. **Logs to Console**: All events logged with `[PayPal]` prefix
2. **Sends to Render.com**: If enabled in appsettings (`CommLogs:Enabled = true`)
3. **Event Categories**: Each operation categorized as "paypal" for filtering
4. **Sensitive Data**: Tokens not logged; amounts and IDs logged for tracking

**Log Format:**
```
[PayPal] Creating order: 10.00 USD
[PayPal] Order created successfully: 3C679910GA908715U
[PayPal] Capturing order: 3C679910GA908715U
[PayPal] Order captured successfully: 3C679910GA908715U, Status: COMPLETED
```

**Render.com Integration:**
- Logs sent to configured `CommLogs:Endpoint`
- Each log entry includes timestamp, type, category, and data
- Webhook events fully logged for audit trail
- Errors include HTTP status codes and error messages

## Error Handling

The implementation includes comprehensive error handling:

1. **Configuration Errors**: Missing credentials logged with clear messages
2. **Network Errors**: HTTP errors logged with status codes and response bodies
3. **Token Errors**: OAuth failures logged and cached token invalidated
4. **Order Errors**: Order creation/capture failures logged with order ID
5. **Exceptions**: Unexpected errors logged with stack traces

All errors are logged to both console and Render.com logs for monitoring.

## Security Considerations

1. **Credentials**: Stored in appsettings.json (should use environment variables in production)
2. **HTTPS**: All API calls over TLS 1.2+
3. **Token Caching**: Tokens cached securely with expiration validation
4. **CSRF Protection**: [ValidateAntiForgeryToken] applied to POST endpoints
5. **Request IDs**: Unique PayPal-Request-Id headers for idempotency
6. **Webhook Validation**: Future implementation should validate signature

## Testing

### Manual Testing Steps:

1. **Create Order:**
   ```bash
   curl -X POST https://webcashier.onrender.com/PayPal/Create \
     -d "amount=10.00&currency=USD&description=Test Order"
   ```

2. **Approve on PayPal**: Visit returned `approvalUrl`

3. **Capture Order:**
   ```bash
   curl -X POST https://webcashier.onrender.com/PayPal/Capture \
     -d "orderId={orderId}"
   ```

### Using Postman:

PayPal Postman collection and environment provided in attachments.

## Configuration for Production

When deploying to production:

1. Update `ApiUrl` to `https://api.paypal.com` (remove "sandbox")
2. Use production `ClientId` and `ClientSecret` from PayPal
3. Update `ReturnUrl`, `CancelUrl`, and `NotifyUrl` to production domains
4. Set `CommLogs:Enabled` to `true` for monitoring
5. Store credentials in environment variables, not appsettings
6. Implement webhook signature validation
7. Update `BrandName` to actual merchant name

## Future Enhancements

1. **Webhook Signature Validation**: Verify PayPal webhook authenticity
2. **STC API Integration**: Support for transaction context (additional fraud data)
3. **Refund Handling**: Implement refund operations
4. **Alternative Payment Methods**: Add credit card, bank transfer options
5. **Order Status Polling**: Client-side polling for async capture
6. **Payment Method Selection**: Display available methods to customer
7. **Currency Conversion**: Support for multi-currency orders

## References

- [PayPal REST API v2 Documentation](https://developer.paypal.com/api/checkout/orders/)
- [OAuth 2.0 Authentication](https://developer.paypal.com/api/rest/authentication/)
- [PayPal Developer Dashboard](https://developer.paypal.com/dashboard/)

## Files Modified/Created

### Created:
- [WebCashier/Models/PayPal/PayPalOAuthResponse.cs](WebCashier/Models/PayPal/PayPalOAuthResponse.cs)
- [WebCashier/Models/PayPal/PayPalOrderRequest.cs](WebCashier/Models/PayPal/PayPalOrderRequest.cs)
- [WebCashier/Models/PayPal/PayPalOrderResponse.cs](WebCashier/Models/PayPal/PayPalOrderResponse.cs)
- [WebCashier/Models/PayPal/PayPalWebhook.cs](WebCashier/Models/PayPal/PayPalWebhook.cs)
- [WebCashier/Services/IPayPalService.cs](WebCashier/Services/IPayPalService.cs)
- [WebCashier/Services/PayPalService.cs](WebCashier/Services/PayPalService.cs)
- [WebCashier/Controllers/PayPalController.cs](WebCashier/Controllers/PayPalController.cs)

### Modified:
- [WebCashier/appsettings.json](WebCashier/appsettings.json) - Added PayPal section
- [WebCashier/Program.cs](WebCashier/Program.cs) - Registered PayPal service
