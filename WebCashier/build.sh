#!/bin/bash
echo "Building WebCashier for Render.com..."
dotnet publish -c Release -o ./publish
echo "Build complete!"
