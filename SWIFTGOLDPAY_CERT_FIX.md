# SwiftGoldPay Certificate Handling - Fix Documentation

## Problem

SwiftGoldPay API was returning error **"C-4002: Certificate invalid or expired"** even though:
- The server certificate pinning was working correctly
- The certificate files existed in the `/cert` directory
- The PFX file was being loaded

## Root Cause

The PFX file was not being loaded correctly due to:

1. **Improper PFX creation**: Using `openssl pkcs12 -export` with the `-nodes` flag doesn't create a proper password-less PFX
2. **Wrong key storage flags**: The original code used `MachineKeySet` which doesn't work well on Linux (Render.com)
3. **Insufficient error handling**: No fallback strategies when PFX loading failed
4. **Missing diagnostics**: No detailed logging to identify certificate issues

## Solution

### Code Changes

Updated [Program.cs](WebCashier/Program.cs) with the following improvements:

#### 1. **Multi-Strategy PFX Loading**
The application now tries multiple strategies to load the PFX:
- Strategy 1: `EphemeralKeySet` (best for Linux/containers)
- Strategy 2: `MachineKeySet + PersistKeySet` (traditional approach)
- Strategy 3: `DefaultKeySet` (fallback)

#### 2. **Improved PEM Loading**
- Uses `EphemeralKeySet` on Linux for proper keystore handling
- Exports to PFX format and re-imports for consistency
- Better error handling and diagnostics

#### 3. **Enhanced Diagnostics**
Added comprehensive logging:
- File sizes and loading strategies
- Certificate details (subject, issuer, serial, thumbprint)
- Private key verification
- Expiration date checks with warnings
- Stack traces on errors

#### 4. **Better Error Messages**
Clear warnings when no certificate is found with instructions on how to fix.

### Regenerating the PFX File

#### Option 1: Use the Provided Script (Recommended)

```bash
./generate-pfx.sh
```

This script:
- Validates that certificate files exist
- Optionally includes intermediate certificates
- Creates a properly formatted PFX
- Verifies the generated PFX
- Shows certificate information

#### Option 2: Manual OpenSSL Command

For a **password-less** PFX (recommended for Render.com):
```bash
cd WebCashier/cert
openssl pkcs12 -export \
    -inkey private.key \
    -in certificate.pem \
    -certfile client-chain.pem \
    -out client.pfx \
    -passout pass:
```

For a **password-protected** PFX:
```bash
cd WebCashier/cert
openssl pkcs12 -export \
    -inkey private.key \
    -in certificate.pem \
    -certfile client-chain.pem \
    -out client.pfx \
    -passout pass:YourSecurePassword
```

Then set on Render.com:
```
CERT_PFX_PASSWORD=YourSecurePassword
```

### Deployment Options

#### Option A: Deploy PFX File (Recommended)
1. Generate `client.pfx` using the script or manual command above
2. Place in `WebCashier/cert/` directory
3. Deploy to Render.com
4. If using a password, set `CERT_PFX_PASSWORD` environment variable

#### Option B: Use Environment Variables
Set these on Render.com:
```bash
SGP_CLIENT_CERT_PEM=<base64 encoded certificate.pem content>
SGP_CLIENT_KEY_PEM=<base64 encoded private.key content>
```

Or use the Base64-encoded PFX:
```bash
# Generate base64 PFX locally
cat WebCashier/cert/client.pfx | base64 > pfx.b64

# Set on Render.com
SGP_CLIENT_PFX_BASE64=<paste content of pfx.b64>
SGP_CLIENT_PFX_PASSWORD=<password if set>
```

#### Option C: Keep Using PEM Files
The application will automatically fall back to loading `certificate.pem` and `private.key` directly if the PFX is not found or fails to load. This now works correctly on Linux with the improved loading logic.

### Certificate File Locations

The application searches for certificates in these directories (in order):
1. `$CERT_DIR` (environment variable)
2. `WebCashier/cert/` (relative to project root)
3. `../cert/` (parent directory)
4. `cert/` (relative to binary location)
5. `/cert` (absolute path)

### Verifying the Fix

After deploying, check the Render.com logs for:

```
[SwiftGoldPay] Attempting to load client PFX from: /app/WebCashier/cert/client.pfx
[SwiftGoldPay] PFX file size: XXXX bytes
[SwiftGoldPay] Successfully loaded PFX with EphemeralKeySet
[SwiftGoldPay] Successfully loaded client PFX certificate
[SwiftGoldPay] Client cert subject: CN=...
[SwiftGoldPay] Client cert issuer: CN=...
[SwiftGoldPay] Client cert has private key: True
[SwiftGoldPay] Client cert valid from (UTC): ...
[SwiftGoldPay] Client cert valid until (UTC): ...
```

If you see warnings about expiration:
```
[SwiftGoldPay] WARNING: Certificate is expired or not yet valid! Current time: ...
```

This means you need to request a new certificate from SwiftGoldPay.

### Testing Locally

1. Regenerate the PFX:
```bash
./generate-pfx.sh
```

2. Run the application:
```bash
cd WebCashier
dotnet run
```

3. Check the startup logs for certificate loading confirmation

4. Test a SwiftGoldPay payment to verify the certificate is accepted

## Technical Details

### Why EphemeralKeySet?

On Linux containers (like Render.com), using `EphemeralKeySet` is crucial because:
- No persistent key storage location is needed
- Avoids permission issues with user/machine key stores
- Keys exist only in memory for the lifetime of the application
- Perfect for containerized/ephemeral environments

### PFX vs PEM

**PFX (PKCS#12)**:
- Single file containing certificate + private key + chain
- More portable across Windows/Linux/macOS
- Can be password-protected
- Better for distribution

**PEM**:
- Separate files for certificate and key
- Human-readable
- Better for debugging
- Common on Linux systems

The application now handles both correctly with proper platform-specific logic.

## Troubleshooting

### Still Getting "Certificate invalid or expired"

1. **Check certificate expiration**:
```bash
openssl x509 -in WebCashier/cert/certificate.pem -noout -dates
```

2. **Verify PFX contains private key**:
```bash
openssl pkcs12 -in WebCashier/cert/client.pfx -noout -passin pass:
```

3. **Check certificate details match SwiftGoldPay requirements**:
```bash
openssl x509 -in WebCashier/cert/certificate.pem -noout -text
```

4. **Verify certificate chain is complete**:
```bash
openssl pkcs12 -in WebCashier/cert/client.pfx -noout -chain -passin pass:
```

### Certificate Not Loading

Check the application logs for:
- File path where it's looking for certificates
- Any error messages during loading
- Whether it fell back to PEM files
- Certificate details (subject, thumbprint, etc.)

### Certificate Expired

Request a new certificate from SwiftGoldPay and replace:
- `certificate.pem` - your new certificate
- `private.key` - your new private key (if they provide a new one)
- `client-chain.pem` - intermediate certificates (if provided)

Then regenerate the PFX file.

## Summary

The fix includes:
- ✅ Multiple PFX loading strategies with proper flags for Linux
- ✅ Improved PEM loading with platform-specific handling
- ✅ Comprehensive diagnostic logging
- ✅ Better error messages and warnings
- ✅ Script for proper PFX generation
- ✅ Certificate expiration detection
- ✅ Private key verification

The application will now correctly load and use the client certificate for mTLS authentication with SwiftGoldPay's API.
