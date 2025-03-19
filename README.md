# Trader Application Deployment

This repository contains a secure, one-click deployment solution for the Trader application, designed to work on any VPS with Docker installed.

## Architecture

The deployment uses Docker and Docker Compose to create a secure, containerized environment:

```
VPS (217.154.57.29)
├── Docker Engine
│   ├── trader-api (.NET 8 API)
│   │   ├── Non-root user
│   │   ├── Secure API keys (Docker secrets)
│   │   └── Health checks
│   └── trader-client (Next.js)
│       ├── Non-root user
│       ├── Production build
│       └── Health checks
└── Docker Network (isolated)
```

## Prerequisites

- [Docker](https://docs.docker.com/get-docker/) (v20.10+)
- [Docker Compose](https://docs.docker.com/compose/install/) (v2.0+)
- API keys for:
  - TraderMade
  - OpenRouter

## One-Click Deployment

### Option 1: Deploy Locally to Remote VPS

You can deploy directly from your local machine to the remote VPS using our one-click deployment script:

1. Open a PowerShell terminal in VSCode
2. Run the VPS deployment script:

   ```powershell
   .\deploy-to-vps.ps1
   ```

3. Follow the interactive prompts:
   - Enter your VPS IP (defaults to 217.154.57.29)
   - Enter your VPS username
   - Enter the deployment path (defaults to ~/trader)
   - Confirm the deployment

The script will:
- Test the SSH connection to your VPS
- Create a temporary deployment package locally
- Copy files to a temporary location on the VPS (/tmp/trader)
- Create a script that will set up the deployment on the VPS
- Run the deployment script on the VPS
- Clean up temporary files

This approach works even with limited permissions on the VPS, as it:
- Uses /tmp directory which is typically writable by all users
- Creates a custom setup script that handles directory creation
- Runs all commands in a single SSH session

### Option 2: Deploy Directly on VPS

Alternatively, you can clone the repository directly on the VPS:

1. SSH into your VPS:
   ```bash
   ssh root@217.154.57.29
   ```

2. Clone the repository and run the deployment script:
   ```bash
   git clone https://github.com/SuperRich/trader.git
   cd trader
   chmod +x deploy.sh
   ./deploy.sh
   ```

### Option 3: Deploy Locally for Testing

You can also deploy locally for testing:

1. Open a PowerShell terminal in VSCode
2. Run the PowerShell deployment script:
   ```powershell
   .\deploy.ps1
   ```

4. Follow the interactive prompts:
   - Confirm or update the VPS IP address
   - Enter a domain name (optional)
   - Provide your API keys when prompted

The script will:
- Validate all dependencies
- Securely store your API keys as Docker secrets
- Build and start the containers
- Perform health checks
- Display access information

## Accessing the Application

After deployment:
- Frontend: http://your-vps-ip:3000
- API: http://your-vps-ip:80

## Security Features

This deployment includes several security enhancements:

1. **Docker Secrets**: API keys are stored as Docker secrets, not environment variables
2. **Non-root Users**: Containers run as non-privileged users
3. **Isolated Network**: Services communicate over an internal Docker network
4. **Health Checks**: Automatic monitoring of service health
5. **Resource Limits**: Memory limits prevent container resource exhaustion

## Verifying Deployment

After running the deployment script, you can verify that everything is working correctly by running the test script:

**For Linux/macOS:**
```bash
chmod +x test-deployment.sh
./test-deployment.sh
```

**For Windows:**
```powershell
.\test-deployment.ps1
```

The test script will check:
- Docker and Docker Compose installation
- Container status
- API and client accessibility
- Docker secrets configuration
- API key validation

## Troubleshooting

The deployment script includes comprehensive error handling. If issues occur:

1. **API Key Issues**:
   - The script will validate API keys and prompt for re-entry if needed
   - Check Docker secrets: `docker secret ls`
   - Verify API configuration: `curl http://localhost:80/api/diagnostics/config`

2. **Permission Problems**:
   - All containers run as non-root users with proper filesystem permissions
   - Check container logs: `docker-compose logs api` or `docker-compose logs client`

3. **Network Issues**:
   - Verify ports 80 and 3000 are accessible on your VPS
   - Check firewall settings: `sudo ufw status`
   - Test API health endpoint: `curl http://localhost:80/health`

4. **Container Failures**:
   - View detailed logs: `docker-compose logs`
   - Check container status: `docker-compose ps`
   - Run the test script to diagnose issues

## Common Commands

```bash
# View logs
docker-compose logs

# Stop all services
docker-compose down

# Restart services
docker-compose restart

# View container status
docker-compose ps

# Update and rebuild (after code changes)
docker-compose build --no-cache
docker-compose up -d
```

## Customization

- **Port Changes**: Edit `docker-compose.yml` to modify exposed ports
- **Domain Setup**: Configure your domain DNS to point to your VPS IP
- **SSL/TLS**: For HTTPS, consider adding Traefik or Nginx as a reverse proxy

## Maintenance

- **Updates**: Pull the latest code and rerun `./deploy.sh`
- **Backups**: Consider volume mounts for persistent data
- **Monitoring**: The health checks enable integration with monitoring tools

## Cleanup

If you need to remove the application completely or start fresh, use the cleanup script:

**For Linux/macOS:**
```bash
chmod +x cleanup.sh
./cleanup.sh
```

**For Windows:**
```powershell
.\cleanup.ps1
```

The cleanup script will:
- Stop and remove all containers
- Remove Docker images
- Remove Docker secrets
- Remove Docker networks

This is useful when:
- You want to start with a clean slate
- You're troubleshooting deployment issues
- You're moving the application to a different server
