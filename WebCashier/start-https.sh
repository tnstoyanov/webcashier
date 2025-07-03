#!/bin/bash
cd /Users/tonystoyanov/Documents/cashier/WebCashier
echo "Starting WebCashier with HTTPS..."
dotnet run > app.log 2>&1 &
echo $! > app.pid
echo "Application started with PID: $(cat app.pid)"
echo "Logs are in app.log"
echo "To stop: kill \$(cat app.pid)"
