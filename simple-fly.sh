#!/bin/bash
echo "Installing Fly.io CLI..."
curl -L https://fly.io/install.sh | sh
export PATH="$PATH:$HOME/.fly/bin"
cd /Users/tonystoyanov/Documents/cashier
flyctl auth login
flyctl launch --no-deploy --name webcashier
flyctl deploy
