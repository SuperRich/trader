# Script to deploy the Trader application to a VPS
# Usage: .\deploy-to-vps.ps1 -ServerIp "your_server_ip" -Username "root" -SshKeyPath "C:\path\to\your\private_key.pem"

param (
    [Parameter(Mandatory=$true)]
    [string]$ServerIp,
    
    [Parameter(Mandatory=$false)]
    [string]$Username = "root",
    
    [Parameter(Mandatory=$false)]
    [string]$SshKeyPath = "",
    
    [Parameter(Mandatory=$false)]
    [switch]$UsePassword = $false
)

# Check if required files exist
$requiredFiles = @(
    "docker-compose.yml",
    "src/Trader.Api/Dockerfile",
    "src/Trader.Client/Dockerfile",
    ".env.example",
    "deploy.sh"
)

foreach ($file in $requiredFiles) {
    if (-not (Test-Path $file)) {
        Write-Host "Error: Required file $file not found." -ForegroundColor Red
        exit 1
    }
}

# Create a temporary directory for deployment files
$tempDir = "deploy-temp"
if (Test-Path $tempDir) {
    Remove-Item -Recurse -Force $tempDir
}
New-Item -ItemType Directory -Path $tempDir | Out-Null

# Copy required files to the temporary directory
Write-Host "Preparing deployment files..." -ForegroundColor Cyan
Copy-Item "docker-compose.yml" -Destination $tempDir
Copy-Item ".env.example" -Destination $tempDir
Copy-Item "deploy.sh" -Destination $tempDir
Copy-Item -Recurse "src" -Destination $tempDir

# Create a deployment script for the server
$setupScript = @"
#!/bin/bash
# Setup script for Trader application

# Update system
apt update && apt upgrade -y

# Install required packages
apt install -y apt-transport-https ca-certificates curl software-properties-common

# Add Docker's official GPG key
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | apt-key add -

# Add Docker repository
add-apt-repository "deb [arch=amd64] https://download.docker.com/linux/ubuntu \$(lsb_release -cs) stable"

# Install Docker
apt update
apt install -y docker-ce

# Install Docker Compose
curl -L "https://github.com/docker/compose/releases/download/v2.18.1/docker-compose-\$(uname -s)-\$(uname -m)" -o /usr/local/bin/docker-compose
chmod +x /usr/local/bin/docker-compose

# Create deployment directory
mkdir -p /opt/trader
cp -r * /opt/trader/
cd /opt/trader

# Make deploy script executable
chmod +x deploy.sh

# Create .env file from example
cp .env.example .env
echo "Please edit /opt/trader/.env to add your API keys"

# Print success message
echo ""
echo "Deployment preparation complete!"
echo "Next steps:"
echo "1. Edit /opt/trader/.env to add your API keys"
echo "2. Run: cd /opt/trader && ./deploy.sh start"
echo ""
"@

$setupScript | Out-File -FilePath "$tempDir/setup.sh" -Encoding utf8

# Convert line endings to Unix format
if (Get-Command "dos2unix" -ErrorAction SilentlyContinue) {
    dos2unix "$tempDir/setup.sh"
    dos2unix "$tempDir/deploy.sh"
} else {
    Write-Host "Warning: dos2unix not found. Line endings might cause issues on Linux." -ForegroundColor Yellow
}

# Create a zip file for deployment
Compress-Archive -Path "$tempDir/*" -DestinationPath "trader-deploy.zip" -Force
Write-Host "Created deployment package: trader-deploy.zip" -ForegroundColor Green

# Determine SSH command
$sshCommand = "ssh"
if ($SshKeyPath -ne "") {
    $sshCommand += " -i `"$SshKeyPath`""
}
$sshCommand += " $Username@$ServerIp"

# Determine SCP command
$scpCommand = "scp"
if ($SshKeyPath -ne "") {
    $scpCommand += " -i `"$SshKeyPath`""
}

# Display instructions
Write-Host "`nDeployment Instructions:" -ForegroundColor Cyan
Write-Host "1. Copy the deployment package to your server:" -ForegroundColor White
Write-Host "   $scpCommand trader-deploy.zip $Username@$ServerIp:/tmp/" -ForegroundColor Yellow

Write-Host "`n2. Connect to your server:" -ForegroundColor White
Write-Host "   $sshCommand" -ForegroundColor Yellow

Write-Host "`n3. Run these commands on the server:" -ForegroundColor White
Write-Host "   cd /tmp" -ForegroundColor Yellow
Write-Host "   apt update && apt install -y unzip" -ForegroundColor Yellow
Write-Host "   unzip trader-deploy.zip -d trader-deploy" -ForegroundColor Yellow
Write-Host "   cd trader-deploy" -ForegroundColor Yellow
Write-Host "   chmod +x setup.sh" -ForegroundColor Yellow
Write-Host "   ./setup.sh" -ForegroundColor Yellow
Write-Host "   # Edit the .env file to add your API keys" -ForegroundColor Yellow
Write-Host "   nano /opt/trader/.env" -ForegroundColor Yellow
Write-Host "   cd /opt/trader" -ForegroundColor Yellow
Write-Host "   ./deploy.sh start" -ForegroundColor Yellow

Write-Host "`nFor more detailed instructions, see DEPLOYMENT_GUIDE.md" -ForegroundColor Cyan

# Clean up
Remove-Item -Recurse -Force $tempDir
