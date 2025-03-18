# Trader Application Deployment

This repository contains a quick and easy deployment setup for the Trader application, focusing on the backend and frontend components.

## Prerequisites

- [Docker](https://docs.docker.com/get-docker/)
- [Docker Compose](https://docs.docker.com/compose/install/)
- API keys for:
  - TraderMade
  - OpenRouter

## Quick Start

1. Clone this repository
2. Copy `.env.example` to `.env` and add your API keys:
   ```bash
   cp .env.example .env
   ```
3. Edit the `.env` file and replace the placeholder API keys with your actual keys
4. Run the deployment script:
   - On Linux/macOS:
     ```bash
     chmod +x deploy.sh
     ./deploy.sh start
     ```
   - On Windows:
     ```powershell
     .\deploy.ps1 start
     ```
5. Access the application:
   - Frontend: http://localhost:3000
   - API: http://localhost:7000

## Deployment Commands

The deployment scripts provide several commands:

- `start`: Build and start the containers
- `stop`: Stop the containers
- `restart`: Restart the containers
- `logs`: View logs
- `build`: Rebuild the containers

Example:
```bash
./deploy.sh logs
```

## Environment Variables

The following environment variables are used:

- `TRADERMADE_API_KEY`: Your TraderMade API key
- `OPENROUTER_API_KEY`: Your OpenRouter API key
- `COMPOSE_PROJECT_NAME`: The Docker Compose project name (default: trader)

## Docker Compose Configuration

The `docker-compose.yml` file defines two services:

1. **api**: The .NET 8 backend API
   - Exposes port 7000
   - Uses environment variables for API keys

2. **client**: The frontend application
   - Exposes port 3000
   - Connects to the API service

## Future CI/CD Considerations

This setup is designed for quick deployment but can be extended for CI/CD:

1. Store API keys as secrets in your CI/CD platform (GitHub Actions, GitLab CI, etc.)
2. Add build and deployment workflows
3. Implement automated testing
4. Set up container registry integration
5. Configure automatic deployments to staging/production environments

## Troubleshooting

- **API Connection Issues**: Ensure the API keys are correctly set in the `.env` file
- **Container Startup Failures**: Check the logs with `./deploy.sh logs`
- **Port Conflicts**: If ports 3000 or 7000 are already in use, modify the port mappings in `docker-compose.yml`
