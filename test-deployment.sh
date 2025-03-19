#!/bin/bash
set -eo pipefail

echo "========================================="
echo "Trader App Deployment Test Script"
echo "========================================="

# Check if Docker is running
echo "🔍 Checking if Docker is running..."
if ! docker info > /dev/null 2>&1; then
    echo "❌ ERROR: Docker is not running or not accessible"
    echo "Please start Docker and try again"
    exit 1
fi
echo "✅ Docker is running"

# Check if Docker Compose is installed
echo "🔍 Checking if Docker Compose is installed..."
if ! command -v docker-compose &> /dev/null; then
    echo "❌ ERROR: Docker Compose is not installed"
    echo "Please install Docker Compose and try again"
    exit 1
fi
echo "✅ Docker Compose is installed"

# Check if the containers are running
echo "🔍 Checking if the containers are running..."
if ! docker-compose ps | grep -q "trader-api"; then
    echo "❌ ERROR: trader-api container is not running"
    echo "Please run the deployment script first"
    exit 1
fi

if ! docker-compose ps | grep -q "trader-client"; then
    echo "❌ ERROR: trader-client container is not running"
    echo "Please run the deployment script first"
    exit 1
fi
echo "✅ Containers are running"

# Check if the API is accessible
echo "🔍 Checking if the API is accessible..."
API_HEALTH=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:80/health || echo "failed")
if [ "$API_HEALTH" = "200" ]; then
    echo "✅ API is accessible"
else
    echo "❌ ERROR: API is not accessible (status: $API_HEALTH)"
    echo "Check the API logs with: docker-compose logs api"
    exit 1
fi

# Check if the client is accessible
echo "🔍 Checking if the client is accessible..."
CLIENT_HEALTH=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:3000 || echo "failed")
if [ "$CLIENT_HEALTH" = "200" ]; then
    echo "✅ Client is accessible"
else
    echo "❌ ERROR: Client is not accessible (status: $CLIENT_HEALTH)"
    echo "Check the client logs with: docker-compose logs client"
    exit 1
fi

# Check if Docker secrets are configured
echo "🔍 Checking if Docker secrets are configured..."
if ! docker secret ls | grep -q "openrouter_key"; then
    echo "❌ ERROR: openrouter_key secret is not configured"
    echo "Please run the deployment script first"
    exit 1
fi

if ! docker secret ls | grep -q "tradermade_key"; then
    echo "❌ ERROR: tradermade_key secret is not configured"
    echo "Please run the deployment script first"
    exit 1
fi
echo "✅ Docker secrets are configured"

# Check API configuration
echo "🔍 Checking API configuration..."
CONFIG_RESPONSE=$(curl -s http://localhost:80/api/diagnostics/config || echo "failed")
if [ "$CONFIG_RESPONSE" = "failed" ]; then
    echo "❌ ERROR: Could not get API configuration"
    echo "Check the API logs with: docker-compose logs api"
    exit 1
fi

if echo "$CONFIG_RESPONSE" | grep -q "HasTraderMadeApiKey\":true"; then
    echo "✅ TraderMade API key is configured"
else
    echo "❌ WARNING: TraderMade API key is not configured or not valid"
    echo "Check the API logs with: docker-compose logs api"
fi

if echo "$CONFIG_RESPONSE" | grep -q "HasOpenRouterApiKey\":true"; then
    echo "✅ OpenRouter API key is configured"
else
    echo "❌ WARNING: OpenRouter API key is not configured or not valid"
    echo "Check the API logs with: docker-compose logs api"
fi

echo "========================================="
echo "✅ Deployment test completed successfully!"
echo "========================================="
echo "Your Trader application is running correctly."
echo ""
echo "Access your application at:"
echo "- Frontend: http://localhost:3000"
echo "- API: http://localhost:80"
echo ""
echo "For more information, check the README.md file."
echo "========================================="
