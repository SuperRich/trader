# Trader App Deployment Script for Windows
# PowerShell equivalent of deploy.sh

Write-Host "========================================="
Write-Host "Trader App Deployment Script"
Write-Host "========================================="

# Dependency checks
function Check-Command {
    param (
        [string]$command
    )
    
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
        Write-Host "❌ ERROR: $command could not be found" -ForegroundColor Red
        Write-Host "Please install $command and try again" -ForegroundColor Red
        exit 1
    }
}

Write-Host "🔍 Checking dependencies..."
Check-Command "docker"
Check-Command "docker-compose"

# Interactive setup
Write-Host "========================================="
Write-Host "VPS Configuration"
Write-Host "========================================="
$VPS_IP = Read-Host "Enter VPS IP [217.154.57.29]"
if ([string]::IsNullOrWhiteSpace($VPS_IP)) {
    $VPS_IP = "217.154.57.29"
}

$DOMAIN = Read-Host "Enter domain name (leave blank for IP)"

# Secret setup
Write-Host "========================================="
Write-Host "API Keys Configuration"
Write-Host "========================================="
Write-Host "These keys will be securely stored as Docker secrets"

function Setup-Secret {
    param (
        [string]$name,
        [string]$secretName
    )
    
    $secret = Read-Host -AsSecureString "Enter $name"
    $BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secret)
    $secretValue = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)
    
    if ([string]::IsNullOrWhiteSpace($secretValue)) {
        Write-Host "❌ ERROR: $name cannot be empty" -ForegroundColor Red
        exit 1
    }
    
    try {
        $secretValue | docker secret create $secretName - 2>$null
        if ($LASTEXITCODE -ne 0) {
            Write-Host "⚠️ Secret $secretName already exists. Recreating..." -ForegroundColor Yellow
            docker secret rm $secretName 2>$null
            $secretValue | docker secret create $secretName -
        }
        Write-Host "✅ Secret $secretName configured successfully" -ForegroundColor Green
    }
    catch {
        Write-Host "❌ Failed to create Docker secret: $_" -ForegroundColor Red
        exit 1
    }
}

Setup-Secret "OpenRouter API Key" "openrouter_key"
Setup-Secret "TraderMade API Key" "tradermade_key"

# Environment configuration
$env:COMPOSE_PROJECT_NAME = "trader"
$env:VPS_IP = $VPS_IP
$env:DOMAIN = $DOMAIN

Write-Host "========================================="
Write-Host "🚀 Starting deployment..."
Write-Host "========================================="

# Build and deploy
Write-Host "🔨 Building Docker images..."
docker-compose build --no-cache
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed. Please check the error messages above." -ForegroundColor Red
    exit 1
}

Write-Host "🚀 Starting services..."
docker-compose up -d --force-recreate
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Service startup failed. Please check the error messages above." -ForegroundColor Red
    exit 1
}

# Health check
Write-Host "🔍 Performing health check..."
Start-Sleep -Seconds 5

try {
    $API_HEALTH = Invoke-WebRequest -Uri "http://localhost:80/health" -Method Head -UseBasicParsing -ErrorAction SilentlyContinue
    $API_STATUS = $API_HEALTH.StatusCode
}
catch {
    $API_STATUS = "failed"
}

try {
    $CLIENT_HEALTH = Invoke-WebRequest -Uri "http://localhost:3000" -Method Head -UseBasicParsing -ErrorAction SilentlyContinue
    $CLIENT_STATUS = $CLIENT_HEALTH.StatusCode
}
catch {
    $CLIENT_STATUS = "failed"
}

if ($API_STATUS -eq 200) {
    Write-Host "✅ API is running" -ForegroundColor Green
}
else {
    Write-Host "⚠️ API health check returned: $API_STATUS" -ForegroundColor Yellow
    Write-Host "Check logs with: docker-compose logs api" -ForegroundColor Yellow
}

if ($CLIENT_STATUS -eq 200) {
    Write-Host "✅ Client is running" -ForegroundColor Green
}
else {
    Write-Host "⚠️ Client health check returned: $CLIENT_STATUS" -ForegroundColor Yellow
    Write-Host "Check logs with: docker-compose logs client" -ForegroundColor Yellow
}

Write-Host "========================================="
Write-Host "✅ Deployment complete!" -ForegroundColor Green
Write-Host "========================================="

if ([string]::IsNullOrWhiteSpace($DOMAIN)) {
    Write-Host "Access your application at: http://$VPS_IP"
}
else {
    Write-Host "Access your application at: http://$DOMAIN"
}

Write-Host ""
Write-Host "Useful commands:"
Write-Host "- View logs: docker-compose logs"
Write-Host "- Stop services: docker-compose down"
Write-Host "- Restart services: docker-compose restart"
Write-Host "========================================="
