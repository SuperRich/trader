# Deploy Trader App to VPS from local PowerShell terminal
# This script automates the deployment process from your local machine to the remote VPS

Write-Host "========================================="
Write-Host "Trader App VPS Deployment Script"
Write-Host "========================================="

# Get VPS details
$VPS_IP = Read-Host "Enter VPS IP [217.154.57.29]"
if ([string]::IsNullOrWhiteSpace($VPS_IP)) {
    $VPS_IP = "217.154.57.29"
}

$VPS_USER = Read-Host "Enter VPS username"
if ([string]::IsNullOrWhiteSpace($VPS_USER)) {
    Write-Host "❌ ERROR: VPS username cannot be empty" -ForegroundColor Red
    exit 1
}

$DEPLOY_PATH = Read-Host "Enter deployment path on VPS [~/trader]"
if ([string]::IsNullOrWhiteSpace($DEPLOY_PATH)) {
    $DEPLOY_PATH = "~/trader"
}

# Check if SSH is available
Write-Host "🔍 Checking if SSH is available..."
try {
    $sshVersion = ssh -V 2>&1
    Write-Host "✅ SSH is available" -ForegroundColor Green
}
catch {
    Write-Host "❌ ERROR: SSH is not available" -ForegroundColor Red
    Write-Host "Please install OpenSSH client and try again" -ForegroundColor Red
    exit 1
}

# Confirm with the user
Write-Host ""
Write-Host "Ready to deploy to VPS with the following settings:"
Write-Host "- VPS IP: $VPS_IP"
Write-Host "- VPS User: $VPS_USER"
Write-Host "- Deploy Path: $DEPLOY_PATH"
Write-Host ""
Write-Host "Enter Y to proceed with deployment or any other key to abort"
$confirm = Read-Host "Proceed with deployment?"
if ($confirm -ne "y" -and $confirm -ne "Y") {
    Write-Host "Deployment aborted." -ForegroundColor Yellow
    exit 0
}

# Check SSH connection and permissions
Write-Host "🔍 Testing SSH connection to VPS..."
try {
    $testConnection = ssh "$VPS_USER@$VPS_IP" "echo 'Connection successful'"
    Write-Host "✅ SSH connection successful" -ForegroundColor Green
}
catch {
    Write-Host "❌ ERROR: Failed to connect to VPS via SSH" -ForegroundColor Red
    Write-Host $_ -ForegroundColor Red
    exit 1
}

# Create a temporary deployment package
Write-Host "🔍 Creating temporary deployment package..."
$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ([System.Guid]::NewGuid().ToString())
New-Item -ItemType Directory -Path $tempDir | Out-Null

# Ensure we're in the right directory before copying
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location -Path $scriptPath

Copy-Item -Path "./*" -Destination $tempDir -Recurse
Write-Host "✅ Deployment package created at $tempDir" -ForegroundColor Green

# Create a deployment script that will run on the VPS
$setupScript = @'
#!/bin/bash
set -e

echo "Creating directory: {0}"
mkdir -p {0}

echo "Moving files to deployment directory"
cp -r /tmp/trader/* {0}/

echo "Setting execute permissions"
chmod +x {0}/deploy.sh

echo "Running deployment script"
cd {0}
./deploy.sh
'@

$setupScript = $setupScript -f $DEPLOY_PATH
$setupScriptPath = "$tempDir/remote-setup.sh"
Set-Content -Path $setupScriptPath -Value $setupScript -Encoding ASCII

# Create a directory on the VPS to store the files temporarily
Write-Host "🔍 Creating temporary directory on VPS..."
try {
    ssh "$VPS_USER@$VPS_IP" "mkdir -p /tmp/trader"
    Write-Host "✅ Temporary directory created" -ForegroundColor Green
}
catch {
    Write-Host "❌ ERROR: Failed to create temporary directory on VPS" -ForegroundColor Red
    Write-Host $_ -ForegroundColor Red
    # Clean up temp directory
    Remove-Item -Path $tempDir -Recurse -Force
    exit 1
}

# Copy files to VPS
Write-Host "🔍 Copying files to VPS..."
try {
    # First verify the temp directory exists and has files
    if (-not (Test-Path $tempDir)) {
        throw "Temporary directory $tempDir does not exist"
    }
    
    $files = Get-ChildItem -Path $tempDir
    if ($files.Count -eq 0) {
        throw "No files found in temporary directory $tempDir"
    }

    # Use forward slashes for SCP
    $tempDirUnix = $tempDir.Replace('\', '/')
    
    # Copy all files to the VPS
    scp -r "${tempDirUnix}/*" "${VPS_USER}@${VPS_IP}:/tmp/trader/"
    
    # Verify the remote-setup.sh file exists on VPS using a single command
    $checkFile = ssh "${VPS_USER}@${VPS_IP}" "if [ -f /tmp/trader/remote-setup.sh ]; then echo 'File exists'; fi"
    if (-not $checkFile) {
        throw "remote-setup.sh was not copied to VPS properly"
    }
    
    Write-Host "✅ Files copied to temporary location on VPS" -ForegroundColor Green
}
catch {
    Write-Host "❌ ERROR: Failed to copy files to VPS" -ForegroundColor Red
    Write-Host $_ -ForegroundColor Red
    # Clean up temp directory if it exists
    if (Test-Path $tempDir) {
        Remove-Item -Path $tempDir -Recurse -Force
    }
    exit 1
}

# Run the deployment script on the VPS
Write-Host "🔍 Running deployment on VPS..."
try {
    # Use semicolon for command separation in bash
    ssh "$VPS_USER@$VPS_IP" "chmod +x /tmp/trader/remote-setup.sh; /tmp/trader/remote-setup.sh"
    Write-Host "✅ Deployment completed" -ForegroundColor Green
}
catch {
    Write-Host "❌ ERROR: Failed to run deployment script on VPS" -ForegroundColor Red
    Write-Host $_ -ForegroundColor Red
    # Clean up temp directory
    Remove-Item -Path $tempDir -Recurse -Force
    exit 1
}

# Clean up temp directory
if (Test-Path $tempDir) {
    Remove-Item -Path $tempDir -Recurse -Force
}

Write-Host "========================================="
Write-Host "✅ Deployment to VPS completed successfully!" -ForegroundColor Green
Write-Host "========================================="
Write-Host "Your Trader application is now running on the VPS."
Write-Host ""
Write-Host "Access your application at:"
Write-Host "- Frontend: http://$VPS_IP:3000"
Write-Host "- API: http://$VPS_IP:80"
Write-Host ""
Write-Host "To check the status of your deployment, run:"
Write-Host "ssh $VPS_USER@$VPS_IP 'cd $DEPLOY_PATH && docker-compose ps'"
Write-Host ""
Write-Host "To view logs, run:"
Write-Host "ssh $VPS_USER@$VPS_IP 'cd $DEPLOY_PATH && docker-compose logs'"
Write-Host "========================================="
