# Callback System Testing - Updated

## Changes Made

### 1. Fixed NotificationUrl Configuration
- **Before**: `"NotificationUrl": "https://165191ec2e6bda1c110b03cd4e4f9e79.m.pipedream.net"`
- **After**: `"NotificationUrl": "https://webcashier.onrender.com/Payment/Callback"`

### 2. Added Dedicated Callback Endpoint
- **Endpoint**: `POST /Payment/Callback`
- **Purpose**: Receives JSON notifications from Praxis
- **Response**: Returns "OK" to acknowledge receipt
- **Data Storage**: Stores transaction data in TempData for later retrieval

### 3. Enhanced Return Method
- **Flow**: Checks TempData for callback data first, then falls back to query parameters
- **Data Source**: Prioritizes callback data over query parameters for more reliable transaction details

## Testing Process

### Step 1: Test Callback Endpoint
```bash
# Test the callback endpoint with sample JSON
curl -X POST https://webcashier.onrender.com/Payment/Callback \
  -H "Content-Type: application/json" \
  -d '{
    "merchant_id": "API-Invesus",
    "application_key": "Invesus.com",
    "customer": {
      "customer_token": "sample_token",
      "country": "US",
      "first_name": "John",
      "last_name": "Doe"
    },
    "session": {
      "auth_token": "sample_auth",
      "intent": "sale",
      "session_status": "active",
      "order_id": "12345",
      "currency": "USD",
      "amount": 1000,
      "payment_method": "card",
      "gateway": "040e154f306f145b84208512d00ef8d9"
    },
    "transaction": {
      "transaction_type": "sale",
      "transaction_status": "approved",
      "tid": 12345,
      "transaction_id": "tx_12345",
      "currency": "USD",
      "amount": 1000,
      "payment_method": "card",
      "payment_processor": "visa",
      "gateway": "040e154f306f145b84208512d00ef8d9",
      "card": {
        "card_type": "visa",
        "card_number": "****1234",
        "card_exp": "12/25",
        "card_issuer_name": "Test Bank",
        "card_issuer_country": "US"
      },
      "status_code": "0",
      "status_details": "Approved",
      "created_by": "system"
    },
    "timestamp": 1642694400,
    "version": "1.3"
  }'
```

### Step 2: Test Return Flow
```bash
# Test the return endpoint after callback data is stored
curl -X GET "https://webcashier.onrender.com/Payment/Return?tid=12345"
```

### Step 3: Test Success Case
```bash
# Test successful transaction callback
curl -X POST https://webcashier.onrender.com/Payment/Callback \
  -H "Content-Type: application/json" \
  -d '{
    "transaction": {
      "transaction_status": "approved",
      "tid": 98765,
      "transaction_id": "tx_success_98765",
      "currency": "USD",
      "amount": 2500,
      "payment_method": "card",
      "payment_processor": "mastercard",
      "card": {
        "card_type": "mastercard",
        "card_number": "****5678",
        "card_exp": "06/26"
      },
      "status_code": "0",
      "status_details": "Transaction approved"
    }
  }'

# Then test the return URL
curl -X GET "https://webcashier.onrender.com/Payment/Return?tid=98765"
```

### Step 4: Test Failure Case
```bash
# Test failed transaction callback
curl -X POST https://webcashier.onrender.com/Payment/Callback \
  -H "Content-Type: application/json" \
  -d '{
    "transaction": {
      "transaction_status": "rejected",
      "tid": 11111,
      "transaction_id": "tx_failed_11111",
      "currency": "USD",
      "amount": 1500,
      "payment_method": "card",
      "payment_processor": "visa",
      "card": {
        "card_type": "visa",
        "card_number": "****9999",
        "card_exp": "03/25"
      },
      "status_code": "1",
      "status_details": "Insufficient funds"
    }
  }'

# Then test the return URL
curl -X GET "https://webcashier.onrender.com/Payment/Return?tid=11111"
```

## Expected Behavior

### Callback Flow:
1. **Praxis sends notification** → `/Payment/Callback` endpoint
2. **Callback endpoint** processes JSON, stores data in TempData
3. **User redirected** → `/Payment/Return?tid=<transaction_id>`
4. **Return endpoint** retrieves data from TempData
5. **User sees** appropriate success/failure view with transaction details

### Success Indicators:
- ✅ Callback endpoint returns "OK" 
- ✅ Transaction data stored in TempData
- ✅ Return endpoint finds and uses callback data
- ✅ Success page shown for "approved" status
- ✅ Failure page shown for "rejected" status
- ✅ Transaction details displayed correctly

### Logs to Check:
- `"Praxis notification callback received"`
- `"Praxis callback processed - Status: {Status}, TID: {TID}"`
- `"Found transaction data from callback for TID: {TID}"`
- `"Using callback data - Status: {Status}, TID: {TID}"`

## Deployment Status
- **Committed**: c24a54d
- **Deployed**: Will be automatically deployed to Render.com
- **Ready for Testing**: Once deployment completes (~2-3 minutes)

## Next Steps
1. Wait for Render.com deployment to complete
2. Test the callback endpoint using the curl commands above
3. Verify that success/failure routing works correctly
4. Test with real Praxis payments to ensure end-to-end functionality
