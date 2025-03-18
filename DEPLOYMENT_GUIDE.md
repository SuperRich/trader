# Deploying to a Web Server

This guide explains how to deploy the Trader application to an actual web server using a VPS (Virtual Private Server).

## Prerequisites

- A VPS with at least 2GB RAM (e.g., DigitalOcean Droplet, AWS EC2, Linode, etc.)
- Ubuntu 20.04 or newer recommended
- Domain name (optional but recommended)

## Step 1: Set Up Your VPS

1. Create a new VPS instance with your preferred provider
2. Connect to your VPS via SSH:
   ```bash
   ssh root@your_server_ip
   ```
3. Update the system:
   ```bash
   apt update && apt upgrade -y
   ```

## Step 2: Install Docker and Docker Compose

```bash
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

## Step 3: Set Up Firewall (Optional but Recommended)

```bash
# Install UFW if not already installed
apt install -y ufw

# Allow SSH connections
ufw allow ssh

# Allow HTTP and HTTPS
ufw allow 80/tcp
ufw allow 443/tcp

# Enable firewall
ufw enable
```

## Step 4: Deploy the Application

1. Create a deployment directory:
   ```bash
   mkdir -p /opt/trader
   cd /opt/trader
   ```

2. Copy your project files to the server:
   
   **Option 1: Using SCP (from your local machine)**
   ```bash
   # Run this on your local machine, not the server
   scp -r docker-compose.yml src .env.example deploy.sh root@your_server_ip:/opt/trader/
   ```
   
   **Option 2: Using Git**
   ```bash
   # On the server
   apt install -y git
   git clone https://your-repository-url.git .
   ```

3. Create and configure the .env file:
   ```bash
   cp .env.example .env
   nano .env
   ```
   
   Update with your actual API keys:
   ```
   TRADERMADE_API_KEY=your_actual_tradermade_key
   OPENROUTER_API_KEY=your_actual_openrouter_key
   ```

4. Make the deployment script executable:
   ```bash
   chmod +x deploy.sh
   ```

5. Start the application:
   ```bash
   ./deploy.sh start
   ```

## Step 5: Set Up Nginx as a Reverse Proxy (Recommended)

1. Install Nginx:
   ```bash
   apt install -y nginx
   ```

2. Create a configuration file:
   ```bash
   nano /etc/nginx/sites-available/trader
   ```

3. Add the following configuration:
   ```nginx
   server {
       listen 80;
       server_name your-domain.com www.your-domain.com;

       location / {
           proxy_pass http://localhost:3000;
           proxy_http_version 1.1;
           proxy_set_header Upgrade $http_upgrade;
           proxy_set_header Connection 'upgrade';
           proxy_set_header Host $host;
           proxy_cache_bypass $http_upgrade;
       }

       location /api/ {
           proxy_pass http://localhost:7000;
           proxy_http_version 1.1;
           proxy_set_header Upgrade $http_upgrade;
           proxy_set_header Connection 'upgrade';
           proxy_set_header Host $host;
           proxy_cache_bypass $http_upgrade;
       }
   }
   ```

4. Enable the site:
   ```bash
   ln -s /etc/nginx/sites-available/trader /etc/nginx/sites-enabled/
   ```

5. Test and restart Nginx:
   ```bash
   nginx -t
   systemctl restart nginx
   ```

## Step 6: Set Up SSL with Let's Encrypt (Recommended)

1. Install Certbot:
   ```bash
   apt install -y certbot python3-certbot-nginx
   ```

2. Obtain and install SSL certificate:
   ```bash
   certbot --nginx -d your-domain.com -d www.your-domain.com
   ```

3. Follow the prompts to complete the setup

## Step 7: Set Up Automatic Updates (Optional)

Create a script to pull the latest changes and restart the application:

```bash
nano /opt/trader/update.sh
```

Add the following content:
```bash
#!/bin/bash
cd /opt/trader
git pull
./deploy.sh restart
```

Make it executable:
```bash
chmod +x /opt/trader/update.sh
```

Set up a cron job to run it periodically (e.g., daily):
```bash
crontab -e
```

Add the following line:
```
0 2 * * * /opt/trader/update.sh >> /var/log/trader-update.log 2>&1
```

## Troubleshooting

### Application Not Starting
Check the logs:
```bash
./deploy.sh logs
```

### Nginx Configuration Issues
Check Nginx error logs:
```bash
tail -f /var/log/nginx/error.log
```

### SSL Certificate Issues
Check Certbot logs:
```bash
tail -f /var/log/letsencrypt/letsencrypt.log
```

## Monitoring and Maintenance

1. Check container status:
   ```bash
   docker ps
   ```

2. View resource usage:
   ```bash
   docker stats
   ```

3. Backup your .env file:
   ```bash
   cp /opt/trader/.env /root/trader-env-backup
   ```

4. Update Docker and system regularly:
   ```bash
   apt update && apt upgrade -y
   ```
