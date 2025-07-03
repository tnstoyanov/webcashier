#!/bin/bash

echo "🔍 Azure CLI Diagnostic Script"
echo "============================="

echo ""
echo "1. Azure CLI Version:"
az version 2>/dev/null || echo "❌ Azure CLI not working"

echo ""
echo "2. Network connectivity test:"
curl -s --connect-timeout 5 https://management.azure.com/ > /dev/null && echo "✅ Can reach Azure" || echo "❌ Network issue"

echo ""
echo "3. Login status:"
az account show --query name --output tsv 2>/dev/null && echo "✅ Logged in" || echo "❌ Not logged in"

echo ""
echo "4. Available subscriptions:"
az account list --query "[].name" --output tsv 2>/dev/null || echo "❌ Cannot list subscriptions"

echo ""
echo "5. Resource groups:"
az group list --query "[].name" --output tsv 2>/dev/null || echo "❌ Cannot list resource groups"

echo ""
echo "🔧 If any items show ❌, try:"
echo "   - az logout && az login"
echo "   - Check internet connection"
echo "   - Restart terminal"
