# JMF 3DS Redirect Handling Fix

## Issue
JMF 3DS redirects were not being handled properly. The application was returning a 200 response with content (1163 bytes) but failing to extract the redirect URL, resulting in the error:
```
[JMF] Response missing redirect URL
```

## Root Cause
The original implementation only looked for the redirect URL in a single location:
- `result.Response.RedirectUrl`

However, JM Financial's 3DS response format may return the redirect URL in different locations depending on the response type:
1. At the top level: `redirect_url`
2. In the response object: `response.redirect_url`
3. In other alternative formats

## Solution
Enhanced [JMFService.cs](WebCashier/Services/JMFService.cs) with flexible response parsing:

### Changes Made

1. **Added comprehensive response content logging**
   - Debug log of full API response for troubleshooting
   - Helps identify actual response format from JM Financial

2. **Implemented flexible JSON parsing**
   - First attempts standard deserialization into `JMFPaymentResponse`
   - Falls back to dynamic JSON document parsing if nested structure not found
   - Searches for `redirect_url` in multiple locations:
     - Top level (for 3DS responses)
     - In response object (standard format)

3. **Enhanced error logging**
   - Logs full response when redirect URL not found
   - More descriptive error messages
   - Tracks session ID and order number when available

4. **Better error handling**
   - Separate try-catch for flexible parsing
   - Graceful fallback to error response
   - Logs parsing exceptions for debugging

### Response Parsing Logic
```
1. Try standard deserialization into JMFPaymentResponse
   └─ If found redirect_url in nested Response object → return result

2. Parse as flexible JSON document
   └─ Check for redirect_url at top level (3DS format)
   └─ If not found, check in response object
   └─ If still not found, check for alternative property names
   └─ Extract and populate SessionId and OrderNumber if available

3. If redirect_url found anywhere:
   └─ Build structured response with all extracted fields
   └─ Return populated JMFPaymentResponse

4. If no redirect_url found:
   └─ Log full response for debugging
   └─ Return error response with details
```

## Testing

### Before Fix
```log
fail: WebCashier.Services.JMFService[0]
      [JMF] Response missing redirect URL
fail: WebCashier.Controllers.JMFController[0]
      [JMF] No redirect URL in response
```

### After Fix
With enhanced logging, you should see:
```log
info: WebCashier.Services.JMFService[0]
      [JMF] API Response Status: 200
dbug: WebCashier.Services.JMFService[0]
      [JMF] API Response Content: {actual response content}
info: WebCashier.Services.JMFService[0]
      [JMF] Found redirect_url at top level (3DS format)
info: WebCashier.Services.JMFService[0]
      [JMF] Payment session created successfully. SessionId: {id}, Order: {number}
```

## Debugging

If the issue persists:

1. **Check application log level** - Set to Debug to see full response content
2. **Enable CommLog** - Review communication logs in admin panel
3. **Look for response format** - The logged response content will show actual structure
4. **Verify credentials** - Ensure `JMF:MerchantKey` and `JMF:ApiPassword` are correct

## Files Modified
- [WebCashier/Services/JMFService.cs](WebCashier/Services/JMFService.cs)
  - Enhanced response parsing in `CreatePaymentSessionAsync` method
  - Added flexible JSON document parsing
  - Improved error logging for debugging

## Backwards Compatibility
✅ Fully backwards compatible - still handles standard response format while also supporting alternative formats.
