# Trader App Cleanup Script for Windows

Write-Host "========================================="
Write-Host "Trader App Cleanup Script"
Write-Host "========================================="
Write-Host "This script will remove all Docker containers, images, and secrets related to the Trader application."
Write-Host "WARNING: This will delete all data and configuration. This action cannot be undone."
Write-Host "========================================="

# Confirm with the user
$confirm = Read-Host "Are you sure you want to proceed? (y/n)"
if ($confirm -ne "y" -and $confirm -ne "Y") {
    Write-Host "Cleanup aborted."
    exit 0
}

Write-Host "========================================="
Write-Host "🔍 Stopping and removing containers..."
Write-Host "========================================="
try {
    docker-compose down
}
catch {
    Write-Host "No containers to remove."
}

Write-Host "========================================="
Write-Host "🔍 Removing Docker images..."
Write-Host "========================================="
try {
    $apiImages = docker images --filter "reference=trader-api" -q
    if ($apiImages) {
        docker rmi $apiImages
    }
    else {
        Write-Host "No trader-api image to remove."
    }
}
catch {
    Write-Host "Error removing trader-api image: $_"
}

try {
    $clientImages = docker images --filter "reference=trader-client" -q
    if ($clientImages) {
        docker rmi $clientImages
    }
    else {
        Write-Host "No trader-client image to remove."
    }
}
catch {
    Write-Host "Error removing trader-client image: $_"
}

Write-Host "========================================="
Write-Host "🔍 Removing Docker secrets..."
Write-Host "========================================="
try {
    docker secret rm openrouter_key
}
catch {
    Write-Host "No openrouter_key secret to remove."
}

try {
    docker secret rm tradermade_key
}
catch {
    Write-Host "No tradermade_key secret to remove."
}

Write-Host "========================================="
Write-Host "🔍 Removing Docker networks..."
Write-Host "========================================="
try {
    docker network rm trader-network
}
catch {
    Write-Host "No trader-network to remove."
}

Write-Host "========================================="
Write-Host "✅ Cleanup completed successfully!" -ForegroundColor Green
Write-Host "========================================="
Write-Host "All Docker containers, images, and secrets related to the Trader application have been removed."
Write-Host "To redeploy the application, run the deployment script again."
Write-Host "========================================="
