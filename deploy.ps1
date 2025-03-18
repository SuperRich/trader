# Simple deployment script for Trader application
# Usage: .\deploy.ps1 [command]
# Commands:
#   start - Build and start the containers
#   stop - Stop the containers
#   restart - Restart the containers
#   logs - View logs
#   build - Rebuild the containers

param (
    [string]$Command = ""
)

# Check if .env file exists
if (-not (Test-Path .env)) {
    Write-Host "Error: .env file not found. Please create it with your API keys." -ForegroundColor Red
    Write-Host "Example:"
    Write-Host "TRADERMADE_API_KEY=your_tradermade_api_key_here"
    Write-Host "OPENROUTER_API_KEY=your_openrouter_api_key_here"
    exit 1
}

# Check if API keys are set
$envContent = Get-Content .env
$tradermadeKey = ($envContent | Select-String "TRADERMADE_API_KEY=(.*)").Matches.Groups[1].Value
$openrouterKey = ($envContent | Select-String "OPENROUTER_API_KEY=(.*)").Matches.Groups[1].Value

if ($tradermadeKey -eq "your_tradermade_api_key_here" -or $openrouterKey -eq "your_openrouter_api_key_here") {
    Write-Host "Warning: You are using placeholder API keys. Please update your .env file with real API keys." -ForegroundColor Yellow
    $continue = Read-Host "Continue anyway? (y/n)"
    if ($continue -ne "y") {
        exit 1
    }
}

# Process command
switch ($Command) {
    "start" {
        Write-Host "Starting Trader application..." -ForegroundColor Green
        docker-compose up -d
        Write-Host "Trader application started. Frontend available at http://localhost:3000, API at http://localhost:7000" -ForegroundColor Green
    }
    "stop" {
        Write-Host "Stopping Trader application..." -ForegroundColor Yellow
        docker-compose down
        Write-Host "Trader application stopped." -ForegroundColor Yellow
    }
    "restart" {
        Write-Host "Restarting Trader application..." -ForegroundColor Yellow
        docker-compose restart
        Write-Host "Trader application restarted." -ForegroundColor Green
    }
    "logs" {
        Write-Host "Showing logs..." -ForegroundColor Cyan
        docker-compose logs -f
    }
    "build" {
        Write-Host "Rebuilding containers..." -ForegroundColor Cyan
        docker-compose build --no-cache
        Write-Host "Containers rebuilt. Use '.\deploy.ps1 start' to start the application." -ForegroundColor Green
    }
    default {
        Write-Host "Usage: .\deploy.ps1 [command]" -ForegroundColor Cyan
        Write-Host "Commands:"
        Write-Host "  start - Build and start the containers"
        Write-Host "  stop - Stop the containers"
        Write-Host "  restart - Restart the containers"
        Write-Host "  logs - View logs"
        Write-Host "  build - Rebuild the containers"
    }
}
