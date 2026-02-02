# JM Financial Integration - Quick Start

## 🚀 Getting Started

### 1. Configure Credentials
Add to `appsettings.Development.json` or `appsettings.Production.json`:

```json
{
  "JMF": {
    "MerchantKey": "your-merchant-key",
    "ApiPassword": "your-api-password"
  }
}
```

**Where to get credentials:**
- Contact JM Financial
- Obtain your Merchant Key and API Password
- Example values provided in attached files

### 2. Build & Run
```bash
cd WebCashier
dotnet build
dotnet run
```

### 3. Test Payment Flow

**Step-by-Step:**

1. Navigate to `https://localhost:5001/Payment`
2. Look for **"JM Ak HPP"** in the payment methods carousel (appears after PayPal)
3. Click on **"JM Ak HPP"** radio button
4. Fill in the form:
   - Amount: Enter between $100 - $199.99
   - Currency: USD (fixed)
   - Name: Your full name
   - Email: Your email address
5. Click **"DEPOSIT"** button
6. Confirm the modal popup
7. You'll be redirected to JM Financial's hosted payment page
8. Complete the payment
9. You'll be redirected back to success or cancel page

### 4. Verify Integration

**Check if working:**
- [ ] Payment form appears when JM Financial is selected
- [ ] Form validates required fields
- [ ] Loading overlay appears when submitting
- [ ] Modal shows before redirect
- [ ] Redirects to JM Financial payment page
- [ ] Success/Cancel pages display correctly
- [ ] Check logs for `[JMF]` prefixed messages

## 📋 Files Overview

| File | Purpose |
|------|---------|
| `IJMFService.cs` | Service interface |
| `JMFService.cs` | Payment session creation logic |
| `JMFController.cs` | HTTP endpoints |
| `Views/JMF/Success.cshtml` | Success page |
| `Views/JMF/Cancel.cshtml` | Cancel/error page |
| `Index.cshtml` | Payment form integration |

## 🔧 Common Issues

### Issue: "Missing JMF configuration"
**Solution:** Check that MerchantKey and ApiPassword are set in appsettings.json

### Issue: Redirect URL shows error
**Solution:** Verify your credentials are correct and BaseUrl is configured

### Issue: Form not showing
**Solution:** 
1. Ensure carousel shows "JM Ak HPP" option
2. Click on the radio button to select it
3. Form should appear below

## 📊 Amount Ranges

Currently configured amounts:
- Minimum: $100.00
- Maximum: $199.99
- Quick buttons: $100, $150, $199.99

To change these, edit `Index.cshtml` in the JMF form section.

## 🔐 Security Notes

- All requests are authenticated via SHA1/MD5 hash
- HTTPS is required for production
- Credentials should be in secrets manager, not code
- Check Communication Log for audit trail

## 📞 Support

For detailed information, see:
- `JMF_INTEGRATION.md` - Full integration guide
- `JMF_IMPLEMENTATION.md` - Implementation details
- Communication Log in admin panel - Transaction logs

## 🎯 Next Steps

1. ✅ Configure credentials
2. ✅ Build the application
3. ✅ Test payment flow
4. ✅ Review logs
5. ✅ Deploy to staging
6. ✅ Deploy to production

---

**Last Updated:** February 2, 2026
**Status:** Ready for deployment
