#!/bin/bash
set -eo pipefail

echo "========================================="
echo "Trader App Deployment Script"
echo "========================================="

# Dependency checks
check_command() {
    if ! command -v $1 &> /dev/null; then
        echo "❌ ERROR: $1 could not be found"
        echo "Please install $1 and try again"
        exit 1
    fi
}

echo "🔍 Checking dependencies..."
check_command docker
check_command docker-compose

# Interactive setup
echo "========================================="
echo "VPS Configuration"
echo "========================================="
read -p "Enter VPS IP [217.154.57.29]: " VPS_IP
VPS_IP=${VPS_IP:-217.154.57.29}

read -p "Enter domain name (leave blank for IP): " DOMAIN

# Secret setup
echo "========================================="
echo "API Keys Configuration"
echo "========================================="
echo "These keys will be securely stored as Docker secrets"

setup_secret() {
    read -sp "Enter $1: " secret
    echo
    if [ -z "$secret" ]; then
        echo "❌ ERROR: $1 cannot be empty"
        exit 1
    fi
    echo "$secret" | docker secret create $2 - || {
        echo "⚠️ Secret $2 already exists. Recreating..."
        docker secret rm $2 &>/dev/null || true
        echo "$secret" | docker secret create $2 -
    }
    echo "✅ Secret $2 configured successfully"
}

setup_secret "OpenRouter API Key" openrouter_key
setup_secret "TraderMade API Key" tradermade_key

# Environment configuration
export COMPOSE_PROJECT_NAME=trader
export VPS_IP
export DOMAIN

echo "========================================="
echo "🚀 Starting deployment..."
echo "========================================="

# Build and deploy
echo "🔨 Building Docker images..."
docker-compose build --no-cache || {
    echo "❌ Build failed. Please check the error messages above."
    exit 1
}

echo "🚀 Starting services..."
docker-compose up -d --force-recreate || {
    echo "❌ Service startup failed. Please check the error messages above."
    exit 1
}

# Health check
echo "🔍 Performing health check..."
sleep 5
API_HEALTH=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:80/health || echo "failed")
CLIENT_HEALTH=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:3000 || echo "failed")

if [ "$API_HEALTH" = "200" ]; then
    echo "✅ API is running"
else
    echo "⚠️ API health check returned: $API_HEALTH"
    echo "Check logs with: docker-compose logs api"
fi

if [ "$CLIENT_HEALTH" = "200" ]; then
    echo "✅ Client is running"
else
    echo "⚠️ Client health check returned: $CLIENT_HEALTH"
    echo "Check logs with: docker-compose logs client"
fi

echo "========================================="
echo "✅ Deployment complete!"
echo "========================================="
echo "Access your application at: ${DOMAIN:-http://$VPS_IP}"
echo ""
echo "Useful commands:"
echo "- View logs: docker-compose logs"
echo "- Stop services: docker-compose down"
echo "- Restart services: docker-compose restart"
echo "========================================="
