#!/bin/bash
set -eo pipefail

echo "========================================="
echo "Trader App Cleanup Script"
echo "========================================="
echo "This script will remove all Docker containers, images, and secrets related to the Trader application."
echo "WARNING: This will delete all data and configuration. This action cannot be undone."
echo "========================================="

# Confirm with the user
read -p "Are you sure you want to proceed? (y/n): " confirm
if [[ "$confirm" != "y" && "$confirm" != "Y" ]]; then
    echo "Cleanup aborted."
    exit 0
fi

echo "========================================="
echo "🔍 Stopping and removing containers..."
echo "========================================="
docker-compose down || echo "No containers to remove."

echo "========================================="
echo "🔍 Removing Docker images..."
echo "========================================="
docker rmi $(docker images --filter "reference=trader-api" -q) 2>/dev/null || echo "No trader-api image to remove."
docker rmi $(docker images --filter "reference=trader-client" -q) 2>/dev/null || echo "No trader-client image to remove."

echo "========================================="
echo "🔍 Removing Docker secrets..."
echo "========================================="
docker secret rm openrouter_key 2>/dev/null || echo "No openrouter_key secret to remove."
docker secret rm tradermade_key 2>/dev/null || echo "No tradermade_key secret to remove."

echo "========================================="
echo "🔍 Removing Docker networks..."
echo "========================================="
docker network rm trader-network 2>/dev/null || echo "No trader-network to remove."

echo "========================================="
echo "✅ Cleanup completed successfully!"
echo "========================================="
echo "All Docker containers, images, and secrets related to the Trader application have been removed."
echo "To redeploy the application, run the deployment script again."
echo "========================================="
