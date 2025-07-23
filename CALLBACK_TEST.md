# Testing Praxis Callback Handling

## Test the callback endpoints locally

### Test Success Callback (POST with JSON)

```bash
curl -X POST http://localhost:5182/Payment/Return \
  -H "Content-Type: application/json" \
  -d '{
    "merchant_id": "API-Invesus",
    "application_key": "Invesus.com",
    "customer": {
      "customer_token": "72a42d712546d3d0182043361afbe7dc",
      "country": "US",
      "first_name": "Tony",
      "last_name": "Stoyanov"
    },
    "session": {
      "auth_token": "88edf407371419cfad5e83d52a2311a3",
      "intent": "payment",
      "session_status": "successful",
      "order_id": "1887433938",
      "currency": "USD",
      "amount": 20000,
      "payment_method": "Credit Card"
    },
    "transaction": {
      "transaction_type": "sale",
      "transaction_status": "approved",
      "tid": 2607073,
      "transaction_id": "125923",
      "currency": "USD",
      "amount": 20000,
      "payment_method": "Credit Card",
      "payment_processor": "Test Card Processor",
      "card": {
        "card_type": "VISA",
        "card_number": "493873******0001",
        "card_exp": "01/2026"
      },
      "status_code": "530",
      "status_details": "Approved"
    }
  }'
```

### Test Failure Callback (POST with JSON)

```bash
curl -X POST http://localhost:5182/Payment/Return \
  -H "Content-Type: application/json" \
  -d '{
    "merchant_id": "API-Invesus",
    "application_key": "Invesus.com",
    "customer": {
      "customer_token": "99e53f4d458266e7995d683d6c9cef5a",
      "country": "US",
      "first_name": "Tony",
      "last_name": "Stoyanov"
    },
    "session": {
      "auth_token": "99e31298023a1b7310cc66b09a7dda0b",
      "intent": "payment",
      "session_status": "failed",
      "order_id": "1781184954",
      "currency": "USD",
      "amount": 20000,
      "payment_method": "Credit Card"
    },
    "transaction": {
      "transaction_type": "sale",
      "transaction_status": "rejected",
      "tid": 2607086,
      "transaction_id": "214605",
      "currency": "USD",
      "amount": 20000,
      "payment_method": "Credit Card",
      "payment_processor": "Test Card Processor",
      "card": {
        "card_type": "VISA",
        "card_number": "493873******0001",
        "card_exp": "01/2026"
      },
      "status_code": "903",
      "status_details": "Do not honor"
    }
  }'
```

### Test Query Parameter Fallback (GET)

```bash
# Success
curl "http://localhost:5182/Payment/Return?transaction_status=approved&tid=123456&payment_method=Credit%20Card&currency=USD"

# Failure
curl "http://localhost:5182/Payment/Return?transaction_status=rejected&tid=123456&payment_method=Credit%20Card&currency=USD&status_code=903&status_details=Do%20not%20honor"
```

## Expected Behavior

- **Success**: `transaction_status=approved` → PaymentSuccess view
- **Failure**: `transaction_status=rejected` → PaymentFailure view
- **JSON Callback**: Properly parse nested structure and extract transaction details
- **Query Fallback**: Handle legacy query parameter format for testing
