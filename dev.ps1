# Start the .NET backend
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd ./src/Trader.Api; dotnet run --environment Development"

# Start the Next.js frontend
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd ./src/Trader.Client; npm run dev" 