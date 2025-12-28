#!/bin/bash
# Test SwiftGoldPay Certificate Loading
# This script runs the application briefly to check certificate loading logs

cd "$(dirname "$0")/WebCashier"

echo "Starting application to test certificate loading..."
echo "Will capture first 15 seconds of logs..."
echo ""

# Run the application in background
timeout 15 dotnet run > /tmp/swiftgoldpay_test.log 2>&1 &
PID=$!

# Wait for startup
sleep 8

# Kill the process
kill $PID 2>/dev/null

echo "=========================================="
echo "SwiftGoldPay Certificate Loading Logs:"
echo "=========================================="
grep -i "swiftgoldpay" /tmp/swiftgoldpay_test.log | grep -i "cert\|pfx\|private\|pem" | head -40

echo ""
echo "=========================================="
echo "Full SwiftGoldPay Logs:"
echo "=========================================="
grep -i "swiftgoldpay" /tmp/swiftgoldpay_test.log | head -50

echo ""
echo "Full log saved to: /tmp/swiftgoldpay_test.log"
