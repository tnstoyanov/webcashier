# SwiftGoldPay Certificate Fix - Deployment Checklist

## Summary

Fixed SwiftGoldPay certificate handling to resolve "Certificate invalid or expired" error (C-4002) by implementing:
- Multi-strategy PFX loading with proper flags for Linux containers
- Improved PEM certificate loading with platform-specific handling
- Comprehensive diagnostic logging
- Better error messages and warnings

## Files Changed

1. **[Program.cs](WebCashier/Program.cs)** - Updated certificate loading logic
   - Lines ~425-570: Enhanced PFX and PEM loading with multiple strategies
   - Added EphemeralKeySet for Linux compatibility
   - Added detailed logging and diagnostics
   - Added certificate expiration checks

2. **[generate-pfx.sh](generate-pfx.sh)** - New script to generate PFX correctly
3. **[SWIFTGOLDPAY_CERT_FIX.md](SWIFTGOLDPAY_CERT_FIX.md)** - Comprehensive documentation

## Pre-Deployment Steps

### 1. Regenerate the PFX File

The existing PFX might be corrupted or improperly formatted. Regenerate it:

```bash
cd WebCashier/cert
openssl pkcs12 -export \
    -inkey private.key \
    -in certificate.pem \
    -certfile client-chain.pem \
    -out client.pfx \
    -passout pass:
```

Or use the script:
```bash
./generate-pfx.sh
```

**Verification:**
```bash
cd WebCashier/cert
openssl pkcs12 -in client.pfx -noout -passin pass:
# Should output: MAC verified OK
```

### 2. Check Certificate Expiration

```bash
cd WebCashier/cert
openssl x509 -in certificate.pem -noout -dates
```

Current certificate is valid until: **June 30, 2026** ✓

### 3. Verify Files Are in Git

```bash
git status
# Ensure client.pfx is in WebCashier/cert/
```

## Deployment to Render.com

### Option A: Deploy with PFX File (Recommended)

1. **Commit and push changes:**
```bash
git add WebCashier/Program.cs
git add WebCashier/cert/client.pfx
git add generate-pfx.sh
git add SWIFTGOLDPAY_CERT_FIX.md
git commit -m "Fix SwiftGoldPay certificate handling with multi-strategy loading"
git push
```

2. **Trigger Render.com deployment** (automatic on push)

3. **Monitor logs for:**
```
[SwiftGoldPay] Attempting to load client PFX from: /app/WebCashier/cert/client.pfx
[SwiftGoldPay] PFX file size: XXXX bytes
[SwiftGoldPay] Successfully loaded PFX with EphemeralKeySet
[SwiftGoldPay] Successfully loaded client PFX certificate
[SwiftGoldPay] Client cert has private key: True
```

### Option B: Use Environment Variables (Alternative)

If you prefer not to commit the PFX to git:

1. **Generate Base64 PFX:**
```bash
cd WebCashier/cert
cat client.pfx | base64 | pbcopy  # macOS
# Or save to file: cat client.pfx | base64 > pfx.b64
```

2. **Set on Render.com:**
- Go to Environment settings
- Add: `SGP_CLIENT_PFX_BASE64` = (paste the base64 content)
- Optional: `CERT_PFX_PASSWORD` = (if you used a password)

3. **Deploy**

## Post-Deployment Verification

### 1. Check Startup Logs

Look for these success indicators:
```
[SwiftGoldPay] Successfully loaded client PFX certificate
[SwiftGoldPay] Client cert subject: CN=sandbox-partner.swiftgoldpay.com...
[SwiftGoldPay] Client cert has private key: True
[SwiftGoldPay] Client cert valid until (UTC): 2026-06-30...
```

### 2. Test SwiftGoldPay Payment

Make a test payment and check the API response.

**Expected:** No more "C-4002: Certificate invalid or expired" errors

### 3. Monitor for Warnings

If you see:
```
[SwiftGoldPay] WARNING: Certificate is expired or not yet valid!
```
This means you need a new certificate from SwiftGoldPay.

## Rollback Plan

If issues occur:

1. **Revert to previous version:**
```bash
git revert HEAD
git push
```

2. **Check logs for specific errors**

3. **Try environment variable approach** (Option B above)

## Key Improvements

✅ **Multi-strategy loading**: Tries 3 different approaches to load PFX  
✅ **Linux compatibility**: Uses EphemeralKeySet for containerized environments  
✅ **Better diagnostics**: Detailed logging shows exactly what's happening  
✅ **Expiration checks**: Warns if certificate is expired  
✅ **Private key verification**: Confirms the certificate has a usable private key  
✅ **Fallback support**: Falls back to PEM files if PFX fails  

## Expected Outcome

After deployment, SwiftGoldPay API calls should succeed with proper mTLS authentication:

**Before:**
```json
{
  "status": {
    "code": "C-4002",
    "message": "Certificate invalid or expired"
  },
  "data": null
}
```

**After:**
```json
{
  "status": {
    "code": "S-0000",
    "message": "Success"
  },
  "data": {
    "token": "eyJ..."
  }
}
```

## Support

If you encounter issues:

1. Check the full deployment logs on Render.com
2. Look for the `[SwiftGoldPay]` log entries
3. Verify the certificate files exist in `/app/WebCashier/cert/`
4. Check certificate expiration
5. See [SWIFTGOLDPAY_CERT_FIX.md](SWIFTGOLDPAY_CERT_FIX.md) for troubleshooting

---

**Ready to deploy!** ✅
