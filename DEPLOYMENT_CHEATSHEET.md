# Trader App Deployment Cheat Sheet

## Server Information
- IP Address: 217.154.57.29
- Access: `ssh root@217.154.57.29`

## Quick Deployment Steps

### 1. Prepare Deployment Package (On Your Local Machine)
```powershell
# Run this on your Windows computer
.\deploy-to-vps.ps1 -ServerIp "217.154.57.29"
```

### 2. Upload to Server (Follow Script Instructions)
```powershell
# The script will show you this command
scp trader-deploy.zip root@217.154.57.29:/tmp/
```

### 3. Connect to Server
```bash
ssh root@217.154.57.29
```

### 4. Set Up Server (Run These Commands)
```bash
# Go to the temp directory
cd /tmp

# Install unzip
apt update && apt install -y unzip

# Extract the deployment package
unzip trader-deploy.zip -d trader-deploy
cd trader-deploy

# Make the setup script executable
chmod +x setup.sh

# Run the setup script (installs Docker and prepares files)
./setup.sh
```

### 5. Configure API Keys
```bash
# Edit the environment file
nano /opt/trader/.env
```

Add your actual API keys:
```
TRADERMADE_API_KEY=your_actual_tradermade_key_here
OPENROUTER_API_KEY=your_actual_openrouter_key_here
```

Save with: `Ctrl+X`, then `Y`, then `Enter`

### 6. Start the Application
```bash
cd /opt/trader
./deploy.sh start
```

### 7. Open Firewall Ports (If Needed)
```bash
ufw allow 3000/tcp
ufw allow 7000/tcp
```

## Access Your Application
- Frontend: http://217.154.57.29:3000
- API: http://217.154.57.29:7000

## Common Commands

### View Logs
```bash
cd /opt/trader
./deploy.sh logs
```

### Restart Application
```bash
cd /opt/trader
./deploy.sh restart
```

### Stop Application
```bash
cd /opt/trader
./deploy.sh stop
```
