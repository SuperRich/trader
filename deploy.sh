#!/bin/bash

# Simple deployment script for Trader application
# Usage: ./deploy.sh [command]
# Commands:
#   start - Build and start the containers
#   stop - Stop the containers
#   restart - Restart the containers
#   logs - View logs
#   build - Rebuild the containers

# Check if .env file exists
if [ ! -f .env ]; then
    echo "Error: .env file not found. Please create it with your API keys."
    echo "Example:"
    echo "TRADERMADE_API_KEY=your_tradermade_api_key_here"
    echo "OPENROUTER_API_KEY=your_openrouter_api_key_here"
    exit 1
fi

# Check if API keys are set
source .env
if [ "$TRADERMADE_API_KEY" = "your_tradermade_api_key_here" ] || [ "$OPENROUTER_API_KEY" = "your_openrouter_api_key_here" ]; then
    echo "Warning: You are using placeholder API keys. Please update your .env file with real API keys."
    read -p "Continue anyway? (y/n) " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        exit 1
    fi
fi

# Process command
case "$1" in
    start)
        echo "Starting Trader application..."
        docker-compose up -d
        echo "Trader application started. Frontend available at http://localhost:3000, API at http://localhost:7000"
        ;;
    stop)
        echo "Stopping Trader application..."
        docker-compose down
        echo "Trader application stopped."
        ;;
    restart)
        echo "Restarting Trader application..."
        docker-compose restart
        echo "Trader application restarted."
        ;;
    logs)
        echo "Showing logs..."
        docker-compose logs -f
        ;;
    build)
        echo "Rebuilding containers..."
        docker-compose build --no-cache
        echo "Containers rebuilt. Use './deploy.sh start' to start the application."
        ;;
    *)
        echo "Usage: ./deploy.sh [command]"
        echo "Commands:"
        echo "  start - Build and start the containers"
        echo "  stop - Stop the containers"
        echo "  restart - Restart the containers"
        echo "  logs - View logs"
        echo "  build - Rebuild the containers"
        ;;
esac
