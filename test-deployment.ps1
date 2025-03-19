# Trader App Deployment Test Script for Windows

Write-Host "========================================="
Write-Host "Trader App Deployment Test Script"
Write-Host "========================================="

# Check if Docker is running
Write-Host "🔍 Checking if Docker is running..."
try {
    $dockerInfo = docker info 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Docker command failed"
    }
    Write-Host "✅ Docker is running" -ForegroundColor Green
}
catch {
    Write-Host "❌ ERROR: Docker is not running or not accessible" -ForegroundColor Red
    Write-Host "Please start Docker and try again" -ForegroundColor Red
    exit 1
}

# Check if Docker Compose is installed
Write-Host "🔍 Checking if Docker Compose is installed..."
try {
    $dockerComposeVersion = docker-compose --version 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Docker Compose command failed"
    }
    Write-Host "✅ Docker Compose is installed" -ForegroundColor Green
}
catch {
    Write-Host "❌ ERROR: Docker Compose is not installed" -ForegroundColor Red
    Write-Host "Please install Docker Compose and try again" -ForegroundColor Red
    exit 1
}

# Check if the containers are running
Write-Host "🔍 Checking if the containers are running..."
$containersRunning = $true
$psOutput = docker-compose ps
if (-not ($psOutput -match "trader-api")) {
    Write-Host "❌ ERROR: trader-api container is not running" -ForegroundColor Red
    Write-Host "Please run the deployment script first" -ForegroundColor Red
    $containersRunning = $false
}

if (-not ($psOutput -match "trader-client")) {
    Write-Host "❌ ERROR: trader-client container is not running" -ForegroundColor Red
    Write-Host "Please run the deployment script first" -ForegroundColor Red
    $containersRunning = $false
}

if (-not $containersRunning) {
    exit 1
}
Write-Host "✅ Containers are running" -ForegroundColor Green

# Check if the API is accessible
Write-Host "🔍 Checking if the API is accessible..."
try {
    $apiResponse = Invoke-WebRequest -Uri "http://localhost:80/health" -Method Head -UseBasicParsing -ErrorAction SilentlyContinue
    if ($apiResponse.StatusCode -eq 200) {
        Write-Host "✅ API is accessible" -ForegroundColor Green
    }
    else {
        Write-Host "❌ ERROR: API is not accessible (status: $($apiResponse.StatusCode))" -ForegroundColor Red
        Write-Host "Check the API logs with: docker-compose logs api" -ForegroundColor Red
        exit 1
    }
}
catch {
    Write-Host "❌ ERROR: API is not accessible" -ForegroundColor Red
    Write-Host "Check the API logs with: docker-compose logs api" -ForegroundColor Red
    exit 1
}

# Check if the client is accessible
Write-Host "🔍 Checking if the client is accessible..."
try {
    $clientResponse = Invoke-WebRequest -Uri "http://localhost:3000" -Method Head -UseBasicParsing -ErrorAction SilentlyContinue
    if ($clientResponse.StatusCode -eq 200) {
        Write-Host "✅ Client is accessible" -ForegroundColor Green
    }
    else {
        Write-Host "❌ ERROR: Client is not accessible (status: $($clientResponse.StatusCode))" -ForegroundColor Red
        Write-Host "Check the client logs with: docker-compose logs client" -ForegroundColor Red
        exit 1
    }
}
catch {
    Write-Host "❌ ERROR: Client is not accessible" -ForegroundColor Red
    Write-Host "Check the client logs with: docker-compose logs client" -ForegroundColor Red
    exit 1
}

# Check if Docker secrets are configured
Write-Host "🔍 Checking if Docker secrets are configured..."
$secretsOutput = docker secret ls
if (-not ($secretsOutput -match "openrouter_key")) {
    Write-Host "❌ ERROR: openrouter_key secret is not configured" -ForegroundColor Red
    Write-Host "Please run the deployment script first" -ForegroundColor Red
    exit 1
}

if (-not ($secretsOutput -match "tradermade_key")) {
    Write-Host "❌ ERROR: tradermade_key secret is not configured" -ForegroundColor Red
    Write-Host "Please run the deployment script first" -ForegroundColor Red
    exit 1
}
Write-Host "✅ Docker secrets are configured" -ForegroundColor Green

# Check API configuration
Write-Host "🔍 Checking API configuration..."
try {
    $configResponse = Invoke-RestMethod -Uri "http://localhost:80/api/diagnostics/config" -UseBasicParsing -ErrorAction SilentlyContinue
    
    if ($configResponse.HasTraderMadeApiKey) {
        Write-Host "✅ TraderMade API key is configured" -ForegroundColor Green
    }
    else {
        Write-Host "❌ WARNING: TraderMade API key is not configured or not valid" -ForegroundColor Yellow
        Write-Host "Check the API logs with: docker-compose logs api" -ForegroundColor Yellow
    }
    
    if ($configResponse.HasOpenRouterApiKey) {
        Write-Host "✅ OpenRouter API key is configured" -ForegroundColor Green
    }
    else {
        Write-Host "❌ WARNING: OpenRouter API key is not configured or not valid" -ForegroundColor Yellow
        Write-Host "Check the API logs with: docker-compose logs api" -ForegroundColor Yellow
    }
}
catch {
    Write-Host "❌ ERROR: Could not get API configuration" -ForegroundColor Red
    Write-Host "Check the API logs with: docker-compose logs api" -ForegroundColor Red
    exit 1
}

Write-Host "========================================="
Write-Host "✅ Deployment test completed successfully!" -ForegroundColor Green
Write-Host "========================================="
Write-Host "Your Trader application is running correctly."
Write-Host ""
Write-Host "Access your application at:"
Write-Host "- Frontend: http://localhost:3000"
Write-Host "- API: http://localhost:80"
Write-Host ""
Write-Host "For more information, check the README.md file."
Write-Host "========================================="
