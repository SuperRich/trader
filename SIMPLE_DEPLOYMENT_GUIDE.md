# Simple Deployment Guide for Beginners

This guide will help you deploy your Trader application to a server without needing a domain name. We'll keep it simple and beginner-friendly.

## What You'll Need

- A VPS (Virtual Private Server) from any provider like DigitalOcean, AWS, Linode, etc.
  - Recommended: 2GB RAM, 1 CPU
  - Ubuntu 20.04 or newer
- Your TraderMade and OpenRouter API keys

## Step 1: Get a Server

1. Sign up for a VPS provider (DigitalOcean is beginner-friendly)
2. Create a new server (called "Droplet" on DigitalOcean)
   - Choose Ubuntu 20.04
   - Select at least 2GB RAM
   - Choose a data center region close to you
   - Add your SSH key or set a password

## Step 2: Connect to Your Server

Using Windows:
1. Open PowerShell or Command Prompt
2. Connect using SSH:
   ```
   ssh root@217.154.57.29
   ```
3. Enter your password if prompted

## Step 3: Set Up Docker

Copy and paste these commands one by one:

```bash
# Update your system
apt update && apt upgrade -y

# Install required packages
apt install -y apt-transport-https ca-certificates curl software-properties-common

# Add Docker's official GPG key
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | apt-key add -

# Add Docker repository
add-apt-repository "deb [arch=amd64] https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable"

# Install Docker
apt update
apt install -y docker-ce

# Install Docker Compose
curl -L "https://github.com/docker/compose/releases/download/v2.18.1/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
chmod +x /usr/local/bin/docker-compose

# Verify installations
docker --version
docker-compose --version
```

## Step 4: Copy Your Files to the Server

Option 1: Using the helper script (from your local Windows machine):
1. Run the deployment helper script:
   ```powershell
   .\deploy-to-vps.ps1 -ServerIp "217.154.57.29"
   ```
2. Follow the on-screen instructions

Option 2: Manual method (if the script doesn't work):
1. Create a zip file containing:
   - docker-compose.yml
   - src/ folder
   - .env.example
   - deploy.sh
2. Upload it to your server using SCP or SFTP
3. On your server:
   ```bash
   mkdir -p /opt/trader
   # Assuming you uploaded to /tmp/trader.zip
   unzip /tmp/trader.zip -d /opt/trader
   cd /opt/trader
   chmod +x deploy.sh
   ```

## Step 5: Configure Your API Keys

1. Create your environment file:
   ```bash
   cd /opt/trader
   cp .env.example .env
   nano .env
   ```

2. Edit the file to add your real API keys:
   ```
   TRADERMADE_API_KEY=your_actual_tradermade_key_here
   OPENROUTER_API_KEY=your_actual_openrouter_key_here
   ```

3. Save the file:
   - Press `Ctrl+X`
   - Press `Y` to confirm
   - Press `Enter` to save

## Step 6: Start Your Application

```bash
cd /opt/trader
./deploy.sh start
```

## Step 7: Access Your Application

Your application is now running! You can access it using:

- Frontend: http://217.154.57.29:3000
- API: http://217.154.57.29:7000

## Common Issues

### "Permission denied" errors
Run this command:
```bash
chmod +x deploy.sh
```

### "Connection refused" when trying to access the application
Make sure your server's firewall allows connections to ports 3000 and 7000:
```bash
ufw allow 3000/tcp
ufw allow 7000/tcp
```

### Docker containers not starting
Check the logs:
```bash
cd /opt/trader
./deploy.sh logs
```

## Basic Maintenance

### Stopping the application
```bash
cd /opt/trader
./deploy.sh stop
```

### Restarting the application
```bash
cd /opt/trader
./deploy.sh restart
```

### Viewing logs
```bash
cd /opt/trader
./deploy.sh logs
```

## Next Steps (Optional)

Once you're comfortable with this basic setup, you might want to:

1. Get a domain name and set up proper HTTPS
2. Set up automatic backups
3. Configure monitoring

But for now, this simple setup will get your application running quickly!
