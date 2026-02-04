#!/bin/bash

# Generate runtime config.json with environment variable
echo "Generating runtime config..."
cat > /opt/app-root/src/public/config.json <<EOF
{
  "environment": "${APP_ENVIRONMENT:-dev}"
}
EOF

echo "Starting Vite dev server..."
npm run dev
