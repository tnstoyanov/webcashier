#!/bin/bash
# SwiftGoldPay Certificate PFX Generation Script
# This script creates a properly formatted PFX file from PEM certificate and private key

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CERT_DIR="$SCRIPT_DIR/WebCashier/cert"

echo "=========================================="
echo "SwiftGoldPay PFX Certificate Generator"
echo "=========================================="
echo ""

# Check if certificate files exist
if [ ! -f "$CERT_DIR/certificate.pem" ]; then
    echo "ERROR: certificate.pem not found in $CERT_DIR"
    exit 1
fi

if [ ! -f "$CERT_DIR/private.key" ]; then
    echo "ERROR: private.key not found in $CERT_DIR"
    exit 1
fi

echo "Found certificate files:"
echo "  - Certificate: $CERT_DIR/certificate.pem"
echo "  - Private key: $CERT_DIR/private.key"
echo ""

# Check if intermediate chain exists
CHAIN_OPTION=""
if [ -f "$CERT_DIR/client-chain.pem" ]; then
    echo "  - Chain file:  $CERT_DIR/client-chain.pem"
    CHAIN_OPTION="-certfile $CERT_DIR/client-chain.pem"
    echo ""
    echo "Will include intermediate certificates in PFX."
elif [ -f "$CERT_DIR/chain.pem" ]; then
    echo "  - Chain file:  $CERT_DIR/chain.pem"
    CHAIN_OPTION="-certfile $CERT_DIR/chain.pem"
    echo ""
    echo "Will include intermediate certificates in PFX."
else
    echo ""
    echo "No intermediate chain file found. Creating PFX with leaf certificate only."
fi
echo ""

# Ask for password (optional)
echo "Enter a password for the PFX file (press Enter for no password):"
read -s PFX_PASSWORD
echo ""

# Generate PFX
OUTPUT_FILE="$CERT_DIR/client.pfx"

echo "Generating PFX file..."
if [ -z "$PFX_PASSWORD" ]; then
    # No password - use -passout pass: to create truly password-less PFX
    openssl pkcs12 -export \
        -inkey "$CERT_DIR/private.key" \
        -in "$CERT_DIR/certificate.pem" \
        $CHAIN_OPTION \
        -out "$OUTPUT_FILE" \
        -passout pass:
    echo "Created password-less PFX file"
else
    # With password
    openssl pkcs12 -export \
        -inkey "$CERT_DIR/private.key" \
        -in "$CERT_DIR/certificate.pem" \
        $CHAIN_OPTION \
        -out "$OUTPUT_FILE" \
        -passout pass:"$PFX_PASSWORD"
    echo "Created password-protected PFX file"
    echo ""
    echo "IMPORTANT: Set this environment variable on Render.com:"
    echo "  CERT_PFX_PASSWORD=$PFX_PASSWORD"
fi

echo ""
echo "=========================================="
echo "PFX file created successfully!"
echo "=========================================="
echo ""
echo "Location: $OUTPUT_FILE"
echo ""

# Verify the PFX file
echo "Verifying PFX file..."
if [ -z "$PFX_PASSWORD" ]; then
    openssl pkcs12 -in "$OUTPUT_FILE" -noout -passin pass: 2>/dev/null
else
    openssl pkcs12 -in "$OUTPUT_FILE" -noout -passin pass:"$PFX_PASSWORD" 2>/dev/null
fi

if [ $? -eq 0 ]; then
    echo "✓ PFX file is valid and readable"
else
    echo "✗ WARNING: PFX file verification failed"
    exit 1
fi
echo ""

# Show certificate info
echo "Certificate information:"
if [ -z "$PFX_PASSWORD" ]; then
    openssl pkcs12 -in "$OUTPUT_FILE" -nokeys -passin pass: 2>/dev/null | openssl x509 -noout -subject -issuer -dates
else
    openssl pkcs12 -in "$OUTPUT_FILE" -nokeys -passin pass:"$PFX_PASSWORD" 2>/dev/null | openssl x509 -noout -subject -issuer -dates
fi
echo ""

echo "=========================================="
echo "Next steps:"
echo "=========================================="
echo "1. Test locally by running the application"
echo "2. Deploy to Render.com"
if [ -n "$PFX_PASSWORD" ]; then
    echo "3. Ensure CERT_PFX_PASSWORD environment variable is set on Render.com"
fi
echo ""
echo "The application will automatically find and load client.pfx from the /cert directory."
echo ""
