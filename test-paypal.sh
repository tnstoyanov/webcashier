#!/bin/bash

# PayPal Integration Testing Script
# Usage: ./test-paypal.sh [base_url] [amount] [currency]

BASE_URL="${1:-http://localhost:5000}"
AMOUNT="${2:-10.00}"
CURRENCY="${3:-USD}"

echo "🔵 PayPal Integration Test Script"
echo "=================================="
echo "Base URL: $BASE_URL"
echo "Amount: $AMOUNT $CURRENCY"
echo ""

# Step 1: Create Order
echo "📝 Step 1: Creating order..."
echo "POST $BASE_URL/PayPal/Create"

RESPONSE=$(curl -s -X POST "$BASE_URL/PayPal/Create" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "amount=$AMOUNT&currency=$CURRENCY&description=Test%20Payment")

echo "Response:"
echo "$RESPONSE" | python3 -m json.tool 2>/dev/null || echo "$RESPONSE"
echo ""

# Extract orderId from response
ORDER_ID=$(echo "$RESPONSE" | grep -o '"orderId":"[^"]*' | cut -d'"' -f4)

if [ -z "$ORDER_ID" ]; then
  echo "❌ Failed to create order. Check credentials in appsettings.json"
  exit 1
fi

echo "✅ Order created: $ORDER_ID"
echo ""

# Step 2: Get Approval URL
APPROVAL_URL=$(echo "$RESPONSE" | grep -o '"approvalUrl":"[^"]*' | cut -d'"' -f4)

if [ -n "$APPROVAL_URL" ]; then
  echo "🔗 Approval URL:"
  echo "$APPROVAL_URL"
  echo ""
  echo "⚠️  Open this URL in your browser to approve payment"
  echo "Then run the capture command:"
  echo "  curl -X POST '$BASE_URL/PayPal/Capture' -d 'orderId=$ORDER_ID'"
else
  echo "❌ No approval URL in response"
  exit 1
fi
