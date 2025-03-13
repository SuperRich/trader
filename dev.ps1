# Function to check if a port is in use
function Test-Port {
    param($port)
    $result = $null
    try {
        $result = Test-NetConnection -ComputerName localhost -Port $port -WarningAction SilentlyContinue
    } catch {}
    return $result.TcpTestSucceeded
}

# Kill any existing processes on our ports
$ports = @(7001, 3000)
foreach ($port in $ports) {
    $process = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess
    if ($process) {
        Stop-Process -Id $process -Force
        Write-Host "Killed process using port $port"
    }
}

# Create an array to store the processes
$processes = @()

# Start the .NET backend
Write-Host "Starting .NET backend..."
$backend = Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd ./src/Trader.Api; dotnet run --environment Development" -PassThru
$processes += $backend

# Wait for the backend to start
$attempts = 0
$maxAttempts = 30
while (-not (Test-Port 7001) -and $attempts -lt $maxAttempts) {
    Write-Host "Waiting for backend to start..."
    Start-Sleep -Seconds 1
    $attempts++
}

if ($attempts -eq $maxAttempts) {
    Write-Host "Backend failed to start within 30 seconds" -ForegroundColor Red
    $processes | ForEach-Object { Stop-Process -Id $_.Id -Force }
    exit 1
}

# Start the Next.js frontend
Write-Host "Starting Next.js frontend..."
$frontend = Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd ./src/Trader.Client; npm run dev" -PassThru
$processes += $frontend

# Wait for the frontend to start
$attempts = 0
while (-not (Test-Port 3000) -and $attempts -lt $maxAttempts) {
    Write-Host "Waiting for frontend to start..."
    Start-Sleep -Seconds 1
    $attempts++
}

if ($attempts -eq $maxAttempts) {
    Write-Host "Frontend failed to start within 30 seconds" -ForegroundColor Red
    $processes | ForEach-Object { Stop-Process -Id $_.Id -Force }
    exit 1
}

Write-Host "Both services are running!" -ForegroundColor Green
Write-Host "Frontend: http://localhost:3000"
Write-Host "Backend: https://localhost:7001"

# Handle script termination
$null = Register-ObjectEvent -InputObject ([System.Console]) -EventName CancelKeyPress -Action {
    Write-Host "`nStopping all services..."
    $processes | ForEach-Object { Stop-Process -Id $_.Id -Force }
    exit 0
} 