import hmac
import hashlib

# Postman values
client_id = "027bc05c0d0ee13c0ddca9e0"
client_secret = "5d710c6f57010dbc441c6f22425965c0ee3177148dc4d4d7b18f472e31317127"
timestamp = "1763338867"  # From Postman
expected_signature = "47c81b4f23e7caa58d223f6426ccae4500d1e0258e38bf8a6ef2a664898cd3d1"

# Test different body variations
test_cases = [
    ("Empty string", ""),
    ("Empty JSON", "{}"),
    ("Null", None),
]

for name, body in test_cases:
    if body is None:
        data_to_sign = client_id + timestamp
    else:
        data_to_sign = client_id + timestamp + body
    
    signature = hmac.new(
        client_secret.encode('utf-8'),
        data_to_sign.encode('utf-8'),
        hashlib.sha256
    ).hexdigest()
    
    print(f"{name}: {signature}")
    if signature == expected_signature:
        print(f"  ✅ MATCH! Body should be: '{body}'")
    else:
        print(f"  ❌ Expected: {expected_signature}")

