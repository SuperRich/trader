     1	# Trading Analysis Platform
     2	
     3	A C# backend application for analyzing forex, crypto and stock charts at different timeframes (1m, 5m, 15m, 1h, 4h, 1d) and generating AI-powered trading predictions with stop loss and take profit levels.
     4	
     5	## Project Structure
     6	
     7	- `src/Trader.Api` - ASP.NET Core Web API application
     8	- `src/Trader.Core` - Business logic and domain models
     9	- `src/Trader.Infrastructure` - Data access and external services integration
    10	- `tests/Trader.Tests` - Unit and integration tests
    11	
    12	## Features
    13	
    14	- View candlestick data from multiple sources:
    15	  - Polygon.io market data API for forex and crypto (requires API key)
    16	  - TraderMade API for real-time forex and crypto data (requires API key)
    17	  - TwelveData API for forex and crypto data (requires API key)
    18	  - Simulated data provider (fallback when no API key provided)
    19	- Analyze charts for multiple asset types:
    20	  - Forex pairs (EURUSD, GBPJPY, etc.)
    21	  - Cryptocurrencies (BTCUSD, ETHUSD, etc.)
    22	- Generate trading predictions with:
    23	  - Buy/Sell recommendation
    24	  - Entry price
    25	  - Stop loss level
    26	  - Take profit level
    27	  - Risk/reward ratio
    28	- Support for multiple timeframes:
    29	  - 1 minute
    30	  - 5 minute
    31	  - 15 minute
    32	  - 1 hour
    33	  - 4 hour
    34	  - 1 day
    35	- AI-powered market analysis:
    36	  - TradingView-style chart analysis (with Polygon.io data)
    37	  - Multi-timeframe technical analysis
    38	  - Sentiment analysis for any tradable asset
    39	  - Trading recommendations with precise TP/SL levels
    40	  - Risk/reward calculation and trade rationale
    41	- Market Movers Analysis:
    42	  - Identify top market movers based on pip movement
    43	  - Apply EMA filters (10, 20, 50) to find trading opportunities
    44	  - Detect EMA crossovers, bounces, and breakouts
    45	  - Analyze both forex and crypto markets
    46	
    47	## API Endpoints
    48	
    49	### Chart Data Endpoints
    50	
    51	- `GET /api/forex/candles/{symbol}/{timeframe}/{count}` - Get historical candle data for any symbol
    52	  - `symbol`: Trading symbol (e.g., "EURUSD", "BTCUSD", "XRPUSD")
    53	  - `timeframe`: Chart timeframe (Minutes5, Minutes15, Hours1, Hours4, Day1)
    54	  - `count`: Number of candles to retrieve (1-1000)
    55	
    56	- `GET /api/forex/candles/{symbol}/{timeframe}/{count}/{provider}` - Get historical candle data using a specific provider
    57	  - `symbol`: Trading symbol (e.g., "EURUSD", "BTCUSD", "XRPUSD")
    58	  - `timeframe`: Chart timeframe (Minutes5, Minutes15, Hours1, Hours4, Day1)
    59	  - `count`: Number of candles to retrieve (1-1000)
    60	  - `provider`: Data provider to use (Polygon, TraderMade, TwelveData, Mock)
    61	
    62	### Analysis Endpoints
    63	
    64	- `GET /api/trading/analyze/{symbol}` - Get AI analysis of a trading chart with buy/sell recommendation
    65	  - `symbol`: Trading symbol to analyze (e.g., "EURUSD", "BTCUSD")
    66	
    67	- `GET /api/trading/analyze/{symbol}/{provider}` - Get AI analysis using a specific data provider
    68	  - `symbol`: Trading symbol to analyze (e.g., "EURUSD", "BTCUSD")
    69	  - `provider`: Data provider to use (Polygon, TraderMade, TwelveData, Mock)
    70	
    71	- `GET /api/forex/sentiment/{symbol}` - Get market sentiment analysis for a symbol
    72	  - `symbol`: Symbol to analyze (e.g., "EURUSD", "BTCUSD")
    73	
    74	- `GET /api/trading/recommendations?count=3` - Get top trading recommendations with entry, stop loss, and take profit
    75	  - `count`: Number of recommendations to return (default: 3, max: 5)
    76	
    77	### Legacy Endpoints
    78	
    79	- `GET /api/forex/prediction/{currencyPair}/{timeframe}` - Get a prediction for a specific currency pair and timeframe
    80	- `GET /api/forex/multi-timeframe/{currencyPair}` - Get predictions across all timeframes for a currency pair
    81	
    82	### Market Movers Endpoints
    83	
    84	- `GET /api/market-movers/forex` - Get top forex market movers by pip movement
    85	  - `count`: Number of market movers to return (default: 10, max: 25)
    86	  - `timeframe`: Chart timeframe for analysis (default: Hours1)
    87	  - `provider`: Data provider to use (default: TwelveData)
    88	  - Example: `GET /api/market-movers/forex?count=5&timeframe=Hours1&provider=TwelveData`
    89	
    90	- `GET /api/market-movers/crypto` - Get top crypto market movers by price movement
    91	  - `count`: Number of market movers to return (default: 10, max: 25)
    92	  - `timeframe`: Chart timeframe for analysis (default: Hours1)
    93	  - `provider`: Data provider to use (default: TwelveData)
    94	  - Example: `GET /api/market-movers/crypto?count=5&timeframe=Hours1&provider=TwelveData`
    95	
    96	- `GET /api/market-movers/forex/ema-filtered` - Get top forex market movers with EMA filters applied
    97	  - `count`: Number of market movers to return (default: 10, max: 25)
    98	  - `shortTermTimeframe`: Timeframe for short-term analysis (default: Hours1)
    99	  - `longTermTimeframe`: Timeframe for long-term analysis (default: Day1)
    100	  - `provider`: Data provider to use (default: TwelveData)
    101	  - Example: `GET /api/market-movers/forex/ema-filtered?count=5&shortTermTimeframe=Hours1&longTermTimeframe=Day1&provider=TwelveData`
    102	
    103	- `GET /api/market-movers/crypto/ema-filtered` - Get top crypto market movers with EMA filters applied
    104	  - `count`: Number of market movers to return (default: 10, max: 25)
    105	  - `shortTermTimeframe`: Timeframe for short-term analysis (default: Hours1)
    106	  - `longTermTimeframe`: Timeframe for long-term analysis (default: Day1)
    107	  - `provider`: Data provider to use (default: TwelveData)
    108	  - Example: `GET /api/market-movers/crypto/ema-filtered?count=5&shortTermTimeframe=Hours1&longTermTimeframe=Day1&provider=TwelveData`
    109	
    110	#### Market Movers Response Format
    111	
    112	The market movers endpoints return a JSON array of market mover objects with the following structure:
    113	
    114	```json
    115	[
    116	  {
    117	    "symbol": "EURUSD",
    118	    "currentPrice": 1.0923,
    119	    "previousPrice": 1.0897,
    120	    "priceChange": 0.0026,
    121	    "percentageChange": 0.24,
    122	    "pipMovement": 26.0,
    123	    "direction": "Up",
    124	    "timeframe": "Hours1",
    125	    "emaValues": {
    126	      "10": 1.0915,
    127	      "20": 1.0905,
    128	      "50": 1.0890
    129	    },
    130	    "emaStatus": {
    131	      "isAboveEma10": true,
    132	      "isAboveEma20": true,
    133	      "isAboveEma50": true,
    134	      "isEma10CrossingAboveEma20": false,
    135	      "isEma10CrossingBelowEma20": false,
    136	      "isBouncingOffEma10": false,
    137	      "isBouncingOffEma20": false,
    138	      "isBouncingOffEma50": false,
    139	      "isBreakingThroughEma10": false,
    140	      "isBreakingThroughEma20": false,
    141	      "isBreakingThroughEma50": false
    142	    },
    143	    "assetType": "Forex",
    144	    "timestamp": "2023-06-15T14:30:00Z"
    145	  },
    146	  // More market movers...
    147	]
    148	```
    149	
    150	The EMA-filtered endpoints also include trading signals in the response, which can be accessed through the `emaStatus` property. The signals indicate whether the price is:
    151	- Above/below key EMAs (10, 20, 50)
    152	- Experiencing EMA crossovers (10 crossing 20)
    153	- Bouncing off EMAs (price within 0.1% of an EMA)
    154	- Breaking through EMAs (crossed within last 3 candles)
    155	
    156	These signals can be used to identify potential trading opportunities based on EMA strategies.
    157	
    158	## Setup Instructions
    159	
    160	### Prerequisites
    161	
    162	- .NET 8.0 SDK or later
    163	- Perplexity API key (for AI analysis)
    164	- Polygon.io API key (for real market data) or
    165	- TraderMade API key (for real market data) or
    166	- TwelveData API key (for real market data)
    167	
    168	### Configuring API Keys
    169	
    170	#### Option 1: Environment Variables
    171	
    172	Set the environment variables before running the application:
    173	
    174	```bash
    175	# For Windows PowerShell
    176	$env:TRADER_PERPLEXITY_API_KEY="your-perplexity-key-here"
    177	$env:TRADER_POLYGON_API_KEY="your-polygon-key-here"
    178	$env:TRADER_TRADERMADE_API_KEY="your-tradermade-key-here"
    179	$env:TRADER_TWELVEDATA_API_KEY="your-twelvedata-key-here"
    180	
    181	# For Windows Command Prompt
    182	set TRADER_PERPLEXITY_API_KEY=your-perplexity-key-here
    183	set TRADER_POLYGON_API_KEY=your-polygon-key-here
    184	set TRADER_TRADERMADE_API_KEY=your-tradermade-key-here
    185	set TRADER_TWELVEDATA_API_KEY=your-twelvedata-key-here
    186	
    187	# For Linux/macOS
    188	export TRADER_PERPLEXITY_API_KEY=your-perplexity-key-here
    189	export TRADER_POLYGON_API_KEY=your-polygon-key-here
    190	export TRADER_TRADERMADE_API_KEY=your-tradermade-key-here
    191	export TRADER_TWELVEDATA_API_KEY=your-twelvedata-key-here
    192	```
    193	
    194	#### Option 2: User Secrets (Development)
    195	
    196	Use .NET User Secrets for development environments:
    197	
    198	```bash
    199	cd src/Trader.Api
    200	dotnet user-secrets init  # Only needed if you haven't set up user secrets yet
    201	dotnet user-secrets set "Perplexity:ApiKey" "your-perplexity-key-here"
    202	dotnet user-secrets set "Polygon:ApiKey" "your-polygon-key-here"
    203	dotnet user-secrets set "TraderMade:ApiKey" "your-tradermade-key-here"
    204	dotnet user-secrets set "TwelveData:ApiKey" "your-twelvedata-key-here"
    205	```
    206	
    207	Verify your secrets are set correctly:
    208	```bash
    209	dotnet user-secrets list
    210	```
    211	
    212	#### Option 3: Using API Setup Endpoints
    213	
    214	For easier setup, use our configuration endpoints:
    215	
    216	1. Set up Perplexity API key:
    217	```bash
    218	curl -X POST https://localhost:7001/api/diagnostics/set-perplexity-key \
    219	  -H "Content-Type: application/json" \
    220	  -d '{"apiKey": "your-pplx-key-here", "saveToUserSecrets": true}'
    221	```
    222	
    223	2. Set up Polygon.io API key:
    224	```bash
    225	curl -X POST https://localhost:7001/api/diagnostics/set-polygon-key \
    226	  -H "Content-Type: application/json" \
    227	  -d '{"apiKey": "your-polygon-key-here", "saveToUserSecrets": true}'
    228	```
    229	
    230	3. Set up TraderMade API key:
    231	```bash
    232	curl -X POST https://localhost:7001/api/diagnostics/set-tradermade-key \
    233	  -H "Content-Type: application/json" \
    234	  -d '{"apiKey": "your-tradermade-key-here", "saveToUserSecrets": true}'
    235	```
    236	
    237	4. Set up TwelveData API key:
    238	```bash
    239	curl -X POST https://localhost:7001/api/diagnostics/set-twelvedata-key \
    240	  -H "Content-Type: application/json" \
    241	  -d '{"apiKey": "your-twelvedata-key-here", "saveToUserSecrets": true}'
    242	```
    243	
    244	### Obtaining API Keys
    245	
    246	#### Perplexity API
    247	
    248	1. Create an account at [Perplexity.ai](https://www.perplexity.ai/)
    249	2. Go to the [API section](https://www.perplexity.ai/settings/api) in your account settings
    250	3. Generate a new API key
    251	4. The key will start with "pplx-"
    252	
    253	#### Polygon.io API
    254	
    255	1. Sign up for an account at [Polygon.io](https://polygon.io/)
    256	2. Choose a plan (they offer a free tier with limited requests)
    257	3. Find your API key in your dashboard
    258	4. The key will be a long alphanumeric string
    259	
    260	#### TraderMade API
    261	
    262	1. Sign up for an account at [TraderMade](https://tradermade.com/)
    263	2. Choose a plan (they offer a free tier with limited requests)
    264	3. Find your API key in your dashboard
    265	4. The key will be a long alphanumeric string
    266	
    267	To test your TraderMade API key:
    268	
    269	```bash
    270	# Test your TraderMade API key
    271	curl -X POST https://localhost:7001/api/diagnostics/set-tradermade-key \
    272	  -H "Content-Type: application/json" \
    273	  -d '{"apiKey": "your-tradermade-key-here", "saveToUserSecrets": true}'
    274	```
    275	
    276	Once configured, you can use TraderMade as your data provider:
    277	
    278	```bash
    279	# Get EURUSD data using TraderMade
    280	curl https://localhost:7001/api/forex/candles/EURUSD/Hours1/100/TraderMade
    281	
    282	# Analyze BTCUSD using TraderMade data
    283	curl https://localhost:7001/api/trading/analyze/BTCUSD/TraderMade
    284	```
    285	
    286	#### TwelveData API
    287	
    288	1. Sign up for an account at [TwelveData](https://twelvedata.com/)
    289	2. Choose a plan (they offer a free tier with limited requests)
    290	3. Find your API key in your dashboard
    291	4. The key will be a long alphanumeric string
    292	
    293	To test your TwelveData API key:
    294	
    295	```bash
    296	# Test your TwelveData API key
    297	curl -X POST https://localhost:7001/api/diagnostics/set-twelvedata-key \
    298	  -H "Content-Type: application/json" \
    299	  -d '{"apiKey": "your-twelvedata-key-here", "saveToUserSecrets": true}'
    300	```
    301	
    302	Once configured, you can use TwelveData as your data provider:
    303	
    304	```bash
    305	# Get EURUSD data using TwelveData
    306	curl https://localhost:7001/api/forex/candles/EURUSD/Hours1/100/TwelveData
    307	
    308	# Analyze BTCUSD using TwelveData
    309	curl https://localhost:7001/api/trading/analyze/BTCUSD/TwelveData
    310	```
    311	
    312	### Running the Application
    313	
    314	1. Clone the repository
    315	2. Navigate to the project directory
    316	3. Build the solution:
    317	   ```
    318	   dotnet build
    319	   ```
    320	4. Run the API:
    321	   ```
    322	   cd src/Trader.Api
    323	   dotnet run
    324	   ```
    325	5. Access the Swagger UI at `https://localhost:7001/swagger`
    326	
    327	## Using the TradingView Analysis Feature
    328	
    329	With the TradingView analysis feature, you can analyze chart patterns and get AI-powered trading recommendations for any forex pair or cryptocurrency. Here's how to use it:
    330	
    331	1. Ensure you have both API keys set up:
    332	   - A market data API key (Polygon.io, TraderMade, or TwelveData) for fetching real market data
    333	   - Perplexity API key (for AI analysis)
    334	
    335	2. Test your setup with the diagnostic endpoint:
    336	   ```bash
    337	   curl https://localhost:7001/api/diagnostics/config
    338	   ```
    339	   Confirm both API keys are configured.
    340	
    341	3. Get chart analysis for a symbol:
    342	   ```bash
    343	   curl https://localhost:7001/api/trading/analyze/BTCUSD
    344	   ```
    345	   This will return a detailed analysis with:
    346	   - Current price
    347	   - Buy/Sell recommendation
    348	   - Stop loss level
    349	   - Take profit level
    350	   - Supporting factors for the recommendation
    351	   - Market sentiment
    352	   - Current market session information and liquidity
    353	
    354	4. Get multiple trading recommendations:
    355	   ```bash
    356	   curl https://localhost:7001/api/trading/recommendations?count=3
    357	   ```
    358	   This will return the top 3 trading opportunities across both forex and crypto.
    359	
    360	5. View raw chart data:
    361	   ```bash
    362	   # Get the last 50 candles on the 1-hour timeframe for BTCUSD
    363	   curl https://localhost:7001/api/forex/candles/BTCUSD/Hours1/50
    364	   ```
    365	
    366	### Example Trading Recommendation Response
    367	
    368	```json
    369	{
    370	  "currencyPair": "BTCUSD",
    371	  "direction": "Buy",
    372	  "sentiment": "Bullish",
    373	  "confidence": 0.85,
    374	  "currentPrice": 45120.50,
    375	  "bestEntryPrice": 44850.00,
    376	  "takeProfitPrice": 46500.00,
    377	  "stopLossPrice": 44200.00,
    378	  "orderType": "LimitBuy",
    379	  "timeToBestEntry": "2-3 hours",
    380	  "validUntil": "2025-03-11T12:34:56Z",
    381	  "isSafeToEnterAtCurrentPrice": true,
    382	  "currentEntryReason": "While the best entry is at 44850.00, entering at the current price of 45120.50 still provides a favorable risk-reward ratio of 1.4:1. Price is currently consolidating above key support at 45000 with strong momentum indicators.",
    383	  "riskRewardRatio": 1.5,
    384	  "factors": [
    385	    "Price broke above key resistance at 45000",
    386	    "Strong bullish momentum on 1h and 4h timeframes",
    387	    "Support confluence at current levels"
    388	  ],
    389	  "rationale": "BTC is showing strong upside momentum after breaking the key psychological level of $45,000. Multiple timeframes align for a bullish continuation pattern.",
    390	  "timestamp": "2025-03-10T12:34:56Z",
    391	  "marketSession": {
    392	    "currentSession": "NewYork",
    393	    "description": "New York Session (North American markets) - 12:00-21:00 UTC - High liquidity, often volatile movements",
    394	    "liquidityLevel": 4,
    395	    "recommendedSession": "NewYork",
    396	    "recommendationReason": "The New York session (12:00-21:00 UTC) offers strong liquidity for BTCUSD with US economic data releases often creating trading opportunities. Volatility can be high during this period.",
    397	    "timeUntilNextSession": "5h 30m",
    398	    "nextSession": "Asian",
    399	    "currentTimeUtc": "2025-03-10T15:30:00Z",
    400	    "nextSessionStartTimeUtc": "2025-03-10T23:00:00Z"
    401	  },
    402	  "sessionWarning": "Warning: Current market session (NewYork) is not optimal for trading BTCUSD. Consider waiting for the London session for better liquidity and trading conditions."
    403	}
    404	```
    405	
    406	## New Features
    407	
    408	### Order Types
    409	
    410	The analysis now includes an `orderType` field that specifies the recommended type of order to place:
    411	
    412	- **MarketBuy/MarketSell**: Execute the trade immediately at the current market price. Use when the current price is already at a good entry point.
    413	  
    414	- **LimitBuy/LimitSell**: Wait for the price to reach a better level before entering.
    41	
    42	## API Endpoints
    43	
    44	### Chart Data Endpoints
    45	
    46	- `GET /api/forex/candles/{symbol}/{timeframe}/{count}` - Get historical candle data for any symbol
    47	  - `symbol`: Trading symbol (e.g., "EURUSD", "BTCUSD", "XRPUSD")
    48	  - `timeframe`: Chart timeframe (Minutes5, Minutes15, Hours1, Hours4, Day1)
    49	  - `count`: Number of candles to retrieve (1-1000)
    50	
    51	- `GET /api/forex/candles/{symbol}/{timeframe}/{count}/{provider}` - Get historical candle data using a specific provider
    52	  - `symbol`: Trading symbol (e.g., "EURUSD", "BTCUSD", "XRPUSD")
    53	  - `timeframe`: Chart timeframe (Minutes5, Minutes15, Hours1, Hours4, Day1)
    54	  - `count`: Number of candles to retrieve (1-1000)
    55	  - `provider`: Data provider to use (Polygon, TraderMade, TwelveData, Mock)
    56	
    57	### Analysis Endpoints
    58	
    59	- `GET /api/trading/analyze/{symbol}` - Get AI analysis of a trading chart with buy/sell recommendation
    60	  - `symbol`: Trading symbol to analyze (e.g., "EURUSD", "BTCUSD")
    61	
    62	- `GET /api/trading/analyze/{symbol}/{provider}` - Get AI analysis using a specific data provider
    63	  - `symbol`: Trading symbol to analyze (e.g., "EURUSD", "BTCUSD")
    64	  - `provider`: Data provider to use (Polygon, TraderMade, TwelveData, Mock)
    65	
    66	- `GET /api/forex/sentiment/{symbol}` - Get market sentiment analysis for a symbol
    67	  - `symbol`: Symbol to analyze (e.g., "EURUSD", "BTCUSD")
    68	
    69	- `GET /api/trading/recommendations?count=3` - Get top trading recommendations with entry, stop loss, and take profit
    70	  - `count`: Number of recommendations to return (default: 3, max: 5)
    71	
    72	### Legacy Endpoints
    73	
    74	- `GET /api/forex/prediction/{currencyPair}/{timeframe}` - Get a prediction for a specific currency pair and timeframe
    75	- `GET /api/forex/multi-timeframe/{currencyPair}` - Get predictions across all timeframes for a currency pair
    76	
    77	## Setup Instructions
    78	
    79	### Prerequisites
    80	
    81	- .NET 8.0 SDK or later
    82	- Perplexity API key (for AI analysis)
    83	- Polygon.io API key (for real market data) or
    84	- TraderMade API key (for real market data) or
    85	- TwelveData API key (for real market data)
    86	
    87	### Configuring API Keys
    88	
    89	#### Option 1: Environment Variables
    90	
    91	Set the environment variables before running the application:
    92	
    93	```bash
    94	# For Windows PowerShell
    95	$env:TRADER_PERPLEXITY_API_KEY="your-perplexity-key-here"
    96	$env:TRADER_POLYGON_API_KEY="your-polygon-key-here"
    97	$env:TRADER_TRADERMADE_API_KEY="your-tradermade-key-here"
    98	$env:TRADER_TWELVEDATA_API_KEY="your-twelvedata-key-here"
    99	
   100	# For Windows Command Prompt
   101	set TRADER_PERPLEXITY_API_KEY=your-perplexity-key-here
   102	set TRADER_POLYGON_API_KEY=your-polygon-key-here
   103	set TRADER_TRADERMADE_API_KEY=your-tradermade-key-here
   104	set TRADER_TWELVEDATA_API_KEY=your-twelvedata-key-here
   105	
   106	# For Linux/macOS
   107	export TRADER_PERPLEXITY_API_KEY=your-perplexity-key-here
   108	export TRADER_POLYGON_API_KEY=your-polygon-key-here
   109	export TRADER_TRADERMADE_API_KEY=your-tradermade-key-here
   110	export TRADER_TWELVEDATA_API_KEY=your-twelvedata-key-here
   111	```
   112	
   113	#### Option 2: User Secrets (Development)
   114	
   115	Use .NET User Secrets for development environments:
   116	
   117	```bash
   118	cd src/Trader.Api
   119	dotnet user-secrets init  # Only needed if you haven't set up user secrets yet
   120	dotnet user-secrets set "Perplexity:ApiKey" "your-perplexity-key-here"
   121	dotnet user-secrets set "Polygon:ApiKey" "your-polygon-key-here"
   122	dotnet user-secrets set "TraderMade:ApiKey" "your-tradermade-key-here"
   123	dotnet user-secrets set "TwelveData:ApiKey" "your-twelvedata-key-here"
   124	```
   125	
   126	Verify your secrets are set correctly:
   127	```bash
   128	dotnet user-secrets list
   129	```
   130	
   131	#### Option 3: Using API Setup Endpoints
   132	
   133	For easier setup, use our configuration endpoints:
   134	
   135	1. Set up Perplexity API key:
   136	```bash
   137	curl -X POST https://localhost:7001/api/diagnostics/set-perplexity-key \
   138	  -H "Content-Type: application/json" \
   139	  -d '{"apiKey": "your-pplx-key-here", "saveToUserSecrets": true}'
   140	```
   141	
   142	2. Set up Polygon.io API key:
   143	```bash
   144	curl -X POST https://localhost:7001/api/diagnostics/set-polygon-key \
   145	  -H "Content-Type: application/json" \
   146	  -d '{"apiKey": "your-polygon-key-here", "saveToUserSecrets": true}'
   147	```
   148	
   149	3. Set up TraderMade API key:
   150	```bash
   151	curl -X POST https://localhost:7001/api/diagnostics/set-tradermade-key \
   152	  -H "Content-Type: application/json" \
   153	  -d '{"apiKey": "your-tradermade-key-here", "saveToUserSecrets": true}'
   154	```
   155	
   156	4. Set up TwelveData API key:
   157	```bash
   158	curl -X POST https://localhost:7001/api/diagnostics/set-twelvedata-key \
   159	  -H "Content-Type: application/json" \
   160	  -d '{"apiKey": "your-twelvedata-key-here", "saveToUserSecrets": true}'
   161	```
   162	
   163	### Obtaining API Keys
   164	
   165	#### Perplexity API
   166	
   167	1. Create an account at [Perplexity.ai](https://www.perplexity.ai/)
   168	2. Go to the [API section](https://www.perplexity.ai/settings/api) in your account settings
   169	3. Generate a new API key
   170	4. The key will start with "pplx-"
   171	
   172	#### Polygon.io API
   173	
   174	1. Sign up for an account at [Polygon.io](https://polygon.io/)
   175	2. Choose a plan (they offer a free tier with limited requests)
   176	3. Find your API key in your dashboard
   177	4. The key will be a long alphanumeric string
   178	
   179	#### TraderMade API
   180	
   181	1. Sign up for an account at [TraderMade](https://tradermade.com/)
   182	2. Choose a plan (they offer a free tier with limited requests)
   183	3. Find your API key in your dashboard
   184	4. The key will be a long alphanumeric string
   185	
   186	To test your TraderMade API key:
   187	
   188	```bash
   189	# Test your TraderMade API key
   190	curl -X POST https://localhost:7001/api/diagnostics/set-tradermade-key \
   191	  -H "Content-Type: application/json" \
   192	  -d '{"apiKey": "your-tradermade-key-here", "saveToUserSecrets": true}'
   193	```
   194	
   195	Once configured, you can use TraderMade as your data provider:
   196	
   197	```bash
   198	# Get EURUSD data using TraderMade
   199	curl https://localhost:7001/api/forex/candles/EURUSD/Hours1/100/TraderMade
   200	
   201	# Analyze BTCUSD using TraderMade data
   202	curl https://localhost:7001/api/trading/analyze/BTCUSD/TraderMade
   203	```
   204	
   205	#### TwelveData API
   206	
   207	1. Sign up for an account at [TwelveData](https://twelvedata.com/)
   208	2. Choose a plan (they offer a free tier with limited requests)
   209	3. Find your API key in your dashboard
   210	4. The key will be a long alphanumeric string
   211	
   212	To test your TwelveData API key:
   213	
   214	```bash
   215	# Test your TwelveData API key
   216	curl -X POST https://localhost:7001/api/diagnostics/set-twelvedata-key \
   217	  -H "Content-Type: application/json" \
   218	  -d '{"apiKey": "your-twelvedata-key-here", "saveToUserSecrets": true}'
   219	```
   220	
   221	Once configured, you can use TwelveData as your data provider:
   222	
   223	```bash
   224	# Get EURUSD data using TwelveData
   225	curl https://localhost:7001/api/forex/candles/EURUSD/Hours1/100/TwelveData
   226	
   227	# Analyze BTCUSD using TwelveData
   228	curl https://localhost:7001/api/trading/analyze/BTCUSD/TwelveData
   229	```
   230	
   231	### Running the Application
   232	
   233	1. Clone the repository
   234	2. Navigate to the project directory
   235	3. Build the solution:
   236	   ```
   237	   dotnet build
   238	   ```
   239	4. Run the API:
   240	   ```
   241	   cd src/Trader.Api
   242	   dotnet run
   243	   ```
   244	5. Access the Swagger UI at `https://localhost:7001/swagger`
   245	
   246	## Using the TradingView Analysis Feature
   247	
   248	With the TradingView analysis feature, you can analyze chart patterns and get AI-powered trading recommendations for any forex pair or cryptocurrency. Here's how to use it:
   249	
   250	1. Ensure you have both API keys set up:
   251	   - A market data API key (Polygon.io, TraderMade, or TwelveData) for fetching real market data
   252	   - Perplexity API key (for AI analysis)
   253	
   254	2. Test your setup with the diagnostic endpoint:
   255	   ```bash
   256	   curl https://localhost:7001/api/diagnostics/config
   257	   ```
   258	   Confirm both API keys are configured.
   259	
   260	3. Get chart analysis for a symbol:
   261	   ```bash
   262	   curl https://localhost:7001/api/trading/analyze/BTCUSD
   263	   ```
   264	   This will return a detailed analysis with:
   265	   - Current price
   266	   - Buy/Sell recommendation
   267	   - Stop loss level
   268	   - Take profit level
   269	   - Supporting factors for the recommendation
   270	   - Market sentiment
   271	   - Current market session information and liquidity
   272	
   273	4. Get multiple trading recommendations:
   274	   ```bash
   275	   curl https://localhost:7001/api/trading/recommendations?count=3
   276	   ```
   277	   This will return the top 3 trading opportunities across both forex and crypto.
   278	
   279	5. View raw chart data:
   280	   ```bash
   281	   # Get the last 50 candles on the 1-hour timeframe for BTCUSD
   282	   curl https://localhost:7001/api/forex/candles/BTCUSD/Hours1/50
   283	   ```
   284	
   285	### Example Trading Recommendation Response
   286	
   287	```json
   288	{
   289	  "currencyPair": "BTCUSD",
   290	  "direction": "Buy",
   291	  "sentiment": "Bullish",
   292	  "confidence": 0.85,
   293	  "currentPrice": 45120.50,
   294	  "bestEntryPrice": 44850.00,
   295	  "takeProfitPrice": 46500.00,
   296	  "stopLossPrice": 44200.00,
   297	  "orderType": "LimitBuy",
   298	  "timeToBestEntry": "2-3 hours",
   299	  "validUntil": "2025-03-11T12:34:56Z",
   300	  "isSafeToEnterAtCurrentPrice": true,
   301	  "currentEntryReason": "While the best entry is at 44850.00, entering at the current price of 45120.50 still provides a favorable risk-reward ratio of 1.4:1. Price is currently consolidating above key support at 45000 with strong momentum indicators.",
   302	  "riskRewardRatio": 1.5,
   303	  "factors": [
   304	    "Price broke above key resistance at 45000",
   305	    "Strong bullish momentum on 1h and 4h timeframes",
   306	    "Support confluence at current levels"
   307	  ],
   308	  "rationale": "BTC is showing strong upside momentum after breaking the key psychological level of $45,000. Multiple timeframes align for a bullish continuation pattern.",
   309	  "timestamp": "2025-03-10T12:34:56Z",
   310	  "marketSession": {
   311	    "currentSession": "NewYork",
   312	    "description": "New York Session (North American markets) - 12:00-21:00 UTC - High liquidity, often volatile movements",
   313	    "liquidityLevel": 4,
   314	    "recommendedSession": "NewYork",
   315	    "recommendationReason": "The New York session (12:00-21:00 UTC) offers strong liquidity for BTCUSD with US economic data releases often creating trading opportunities. Volatility can be high during this period.",
   316	    "timeUntilNextSession": "5h 30m",
   317	    "nextSession": "Asian",
   318	    "currentTimeUtc": "2025-03-10T15:30:00Z",
   319	    "nextSessionStartTimeUtc": "2025-03-10T23:00:00Z"
   320	  },
   321	  "sessionWarning": "Warning: Current market session (NewYork) is not optimal for trading BTCUSD. Consider waiting for the London session for better liquidity and trading conditions."
   322	}
   323	```
   324	
   325	## New Features
   326	
   327	### Order Types
   328	
   329	The analysis now includes an `orderType` field that specifies the recommended type of order to place:
   330	
   331	- **MarketBuy/MarketSell**: Execute the trade immediately at the current market price. Use when the current price is already at a good entry point.
   332	  
   333	- **LimitBuy/LimitSell**: Wait for the price to reach a better level before entering.
   334	  - LimitBuy: Place a buy order at a price lower than the current price (waiting for price to drop)
   335	  - LimitSell: Place a sell order at a price higher than the current price (waiting for price to rise)
   336	  
   337	- **StopBuy/StopSell**: Wait for a breakout/breakdown confirmation before entering.
   338	  - StopBuy: Place a buy order at a price higher than the current price (waiting for upward breakout)
   339	  - StopSell: Place a sell order at a price lower than the current price (waiting for downward breakdown)
   340	
   341	The system automatically determines the appropriate order type based on:
   342	- The relationship between current price and best entry price:
   343	  - For buy orders: If best entry < current price → LimitBuy; if best entry > current price → StopBuy
   344	  - For sell orders: If best entry > current price → LimitSell; if best entry < current price → StopSell
   345	- The market context and trading setup
   346	- The risk-reward profile of the trade
   347	
   348	For example, if the recommendation is to buy BTCUSD with a current price of $45,000 but the best entry price is $44,500, the system will suggest a "LimitBuy" order type, indicating you should place a limit order to buy at the lower price of $44,500.
   349	
   350	```json
   351	"currentPrice": 45000.00,
   352	"bestEntryPrice": 44500.00,
   353	"orderType": "LimitBuy"
   354	```
   355	
   356	Similarly, if the recommendation is to sell BTCUSD with a current price of $45,000 but the best entry price is $46,000, the system will suggest a "LimitSell" order type, indicating you should place a limit order to sell at the higher price of $46,000.
   357	
   358	```json
   359	"currentPrice": 45000.00,
   360	"bestEntryPrice": 46000.00,
   361	"orderType": "LimitSell"
   362	```
   363	
   364	This feature helps traders execute trades more effectively by specifying not just what to trade, but how to enter the position.
   365	
   366	### Entry Safety Indicator
   367	
   368	The analysis now includes two fields that help you decide whether to enter a trade at the current market price:
   369	
   370	1. `isSafeToEnterAtCurrentPrice`: A boolean flag indicating whether it's still acceptable to enter at the current price
   371	2. `currentEntryReason`: A detailed explanation of why it's safe or unsafe to enter at the current price
   372	
   373	```json
   374	"currentPrice": 45120.50,
   375	"bestEntryPrice": 44850.00,
   376	"isSafeToEnterAtCurrentPrice": true,
   377	"currentEntryReason": "While the best entry is at 44850.00, entering at the current price of 45120.50 still provides a favorable risk-reward ratio of 1.4:1. Price is currently consolidating above key support at 45000 with strong momentum indicators."
   378	```
   379	
   380	This feature helps traders make more informed decisions about:
   381	
   382	- **When to wait vs. when to act immediately**: If `isSafeToEnterAtCurrentPrice` is `true`, you can enter at the current price without significantly compromising the trade setup, even if waiting for the best entry would be slightly better.
   383	
   384	- **Risk management**: If `isSafeToEnterAtCurrentPrice` is `false`, it indicates that entering at the current price would significantly reduce the risk-reward ratio or increase risk compared to waiting for the best entry price.
   385	
   386	- **Understanding the specific risks**: The `currentEntryReason` field provides detailed reasoning about why it's safe or unsafe to enter at the current price, including specific price levels, risk-reward calculations, or technical factors.
   387	
   388	The AI considers several factors when determining entry safety:
   389	
   390	- **Distance from best entry**: How far the current price is from the optimal entry point
   391	- **Volatility**: Higher volatility may make immediate entry more risky
   392	- **Proximity to key levels**: Whether the current price is near important support/resistance levels
   393	- **Overall market conditions**: Trend strength, momentum, and other contextual factors
   394	
   395	For example, if a buy recommendation shows:
   396	```json
   397	"currentPrice": 45120.50,
   398	"bestEntryPrice": 44850.00,
   399	"isSafeToEnterAtCurrentPrice": false,
   400	"currentEntryReason": "Current price is too far from optimal entry, reducing risk-reward ratio from 2:1 to 1.2:1. Price is approaching resistance at 45200 and showing signs of short-term exhaustion. Wait for pullback to 44850 support level."
   401	```
   402	
   403	This detailed explanation helps you understand exactly why you should wait for a better entry point rather than entering immediately, providing specific technical reasons and risk-reward calculations.
   404	
   405	### Best Entry Price
   406	
   407	The analysis now includes a `bestEntryPrice` field that provides the optimal entry price for the trade, which may differ from the current price. This is especially useful when:
   408	
   409	- The current price is in the middle of a range and a better entry would be at support/resistance
   410	- A pullback to a key level would provide a better risk-reward ratio
   411	- The market is overextended and a retracement would offer a better entry
   412	
   413	### Time to Best Entry
   414	
   415	The analysis now includes a `timeToBestEntry` field that provides an estimate of how long it might take for the price to reach the optimal entry level. This helps traders:
   416	
   417	- Plan their trading schedule and set appropriate alerts
   418	- Understand the urgency (or lack thereof) of the trading opportunity
   419	- Make informed decisions about whether to wait for the best entry or execute immediately
   420	
   421	For example:
   422	```json
   423	"timeToBestEntry": "2-3 hours"
   424	```
   425	
   426	This indicates that based on current market conditions and recent price action, the price is expected to reach the best entry level within 2-3 hours. If the estimate is "Unknown", it means the system cannot reliably predict when the price might reach the best entry level.
   427	
   428	### Recommendation Validity Period
   429	
   430	The analysis now includes a `validUntil` field that specifies when the recommendation expires. This helps traders:
   431	
   432	- Understand how long the analysis remains relevant
   433	- Know when to seek updated recommendations
   434	- Avoid acting on outdated information
   435	
   436	For example:
   437	```json
   438	"validUntil": "2025-03-11T12:34:56Z"
   439	```
   440	
   441	This indicates that the recommendation is valid until March 11, 2025, at 12:34:56 UTC. After this time, market conditions may have changed significantly, and the recommendation should be considered outdated.
   442	
   443	The validity period is determined based on:
   444	- The timeframes used in the analysis (higher timeframes typically result in longer validity periods)
   445	- Market volatility (more volatile markets typically have shorter validity periods)
   446	- Upcoming economic events or news that might impact the market
   447	- The nature of the trading setup (some patterns have naturally shorter lifespans than others)
   448	
   449	### Market Session Warnings
   450	
   451	When the current market session isn't the recommended one for trading a particular currency pair, the API will now include a `sessionWarning` field with a message explaining:
   452	
   453	- Which session is currently active
   454	- Which session would be better for trading this pair
   455	- A recommendation to consider waiting for the optimal session
   456	
   457	This helps traders make more informed decisions about timing their trades for better liquidity and market conditions.
   458	
   459	## Forex Market Sessions
   460	
   461	The application now provides information about forex market sessions to help you time your trades for optimal liquidity:
   462	
   463	### Market Session Information
   464	
   465	Each analysis and recommendation includes market session data:
   466	
   467	```json
   468	"marketSession": {
   469	  "currentSession": "LondonNewYorkOverlap",
   470	  "description": "London-New York Overlap - 12:00-16:00 UTC - Highest liquidity period, often largest price movements",
   471	  "liquidityLevel": 5,
   472	  "recommendedSession": "LondonNewYorkOverlap",
   473	  "recommendationReason": "The London-New York overlap (12:00-16:00 UTC) provides the highest liquidity for EURUSD, with maximum market participation and often the largest price movements of the day. This is generally the optimal trading window.",
   474	  "timeUntilNextSession": "3h 45m",
   475	  "nextSession": "Asian",
   476	  "currentTimeUtc": "2025-03-10T19:15:00Z",
   477	  "nextSessionStartTimeUtc": "2025-03-10T23:00:00Z"
   478	}
   479	```
   480	
   481	### Understanding Market Sessions
   482	
   483	The forex market operates 24 hours a day, but is divided into major sessions (all times in UTC/GMT):
   484	
   485	1. **Asian Session** (23:00-08:00 UTC)
   486	   - Tokyo, Singapore, Hong Kong markets
   487	   - Moderate liquidity, often range-bound trading
   488	   - Best for JPY, AUD, NZD pairs
   489	
   490	2. **London Session** (07:00-16:00 UTC)
   491	   - European markets
   492	   - High liquidity, often trending movements
   493	   - Best for EUR, GBP pairs
   494	
   495	3. **New York Session** (12:00-21:00 UTC)
   496	   - North American markets
   497	   - High liquidity, often volatile movements
   498	   - Best for USD, CAD pairs
   499	
   500	4. **Session Overlaps**
   501	   - Asian-London Overlap (07:00-08:00 UTC): Increasing liquidity
   502	   - London-New York Overlap (12:00-16:00 UTC): Highest liquidity period
   503	
   504	### Cryptocurrencies and Market Sessions
   505	
   506	**Important Note**: Unlike forex, cryptocurrencies trade 24/7 with consistent liquidity across all sessions. For crypto pairs like BTCUSD and ETHUSD:
   507	
   508	- No session warnings will be displayed as they can be traded at any time
   509	- Session information is still provided for context, but all sessions have high liquidity
   510	- While trading volume may vary slightly across different times of day, cryptocurrencies don't follow the traditional forex session restrictions
   511	- The system automatically detects cryptocurrency pairs and treats them appropriately
   512	
   513	### Using Session Information
   514	
   515	- **Liquidity Level**: A rating from 1-5 indicating current liquidity (5 being highest)
   516	- **Recommended Session**: The optimal session for trading a specific currency pair
   517	- **Time Until Next Session**: Helps you plan when to check back for better conditions
   518	- **Next Session Start Time**: The exact UTC time when the next session will begin
   519	
   520	This information can help you:
   521	- Time your trades for optimal liquidity
   522	- Understand why certain pairs move more during specific sessions
   523	- Plan your trading schedule around the most active periods for your preferred pairs
   524	
   525	> **Note**: All times are provided in UTC/GMT for consistency. Convert to your local time zone as needed.
   526	
   527	## Using Multiple Data Providers
   528	
   529	This application supports multiple data providers for fetching market data:
   530	
   531	1. **Polygon.io** - A comprehensive market data API with support for stocks, forex, and crypto
   532	2. **TraderMade** - A real-time and historical data provider for forex and crypto
   533	3. **TwelveData** - A full market data API with support for forex, crypto, stocks and more
   534	4. **Mock Provider** - A simulated data provider for testing without API keys
   535	
   536	### Fetching Candle Data
   537	
   538	You can specify which provider to use when fetching candle data:
   539	
   540	```bash
   541	# Using Polygon.io
   542	curl https://localhost:7001/api/forex/candles/EURUSD/Hours1/100/Polygon
   543	
   544	# Using TraderMade
   545	curl https://localhost:7001/api/forex/candles/EURUSD/Hours1/100/TraderMade
   546	
   547	# Using TwelveData
   548	curl https://localhost:7001/api/forex/candles/EURUSD/Hours1/100/TwelveData
   549	
   550	# Using Mock data
   551	curl https://localhost:7001/api/forex/candles/EURUSD/Hours1/100/Mock
   552	```
   553	
   554	### Different Timeframes
   555	
   556	The API supports various timeframes for candle data:
   557	
   558	```bash
   559	# 5-minute timeframe
   560	curl https://localhost:7001/api/forex/candles/EURUSD/Minutes5/100/TraderMade
   561	
   562	# 15-minute timeframe
   563	curl https://localhost:7001/api/forex/candles/EURUSD/Minutes15/100/TraderMade
   564	
   565	# 1-hour timeframe
   566	curl https://localhost:7001/api/forex/candles/EURUSD/Hours1/100/TraderMade
   567	
   568	# 4-hour timeframe
   569	curl https://localhost:7001/api/forex/candles/EURUSD/Hours4/100/TraderMade
   570	
   571	# Daily timeframe
   572	curl https://localhost:7001/api/forex/candles/EURUSD/Day1/100/TraderMade
   573	```
   574	
   575	Note: When using TraderMade as the provider, the Minutes15 timeframe is implemented using the 'minute' interval with a period of 15.
   576	
   577	#### TraderMade API Limitations
   578	
   579	TraderMade has the following limitations:
   580	- For minute-based timeframes (Minutes5, Minutes15), the API limits data to 2 working days per request
   581	- Valid interval values are 'minute', 'hourly', and 'daily' only
   582	- For minute intervals, you can specify a period (5, 15, etc.)
   583	- For hourly intervals, you can specify a period (4 for 4-hour timeframe)
   584	
   585	#### TraderMade Live Rates
   586	
   587	Our implementation uses TraderMade's Live Rates API to enhance the accuracy of current prices:
   588	- Historical data is fetched from the timeseries endpoint
   589	- Current prices are updated using the live rates endpoint
   590	- This provides more accurate and up-to-date closing prices for the most recent candle
   591	- The system automatically combines historical and live data for the best accuracy
   592	
   593	### Chart Analysis
   594	
   595	You can also specify which provider to use for chart analysis:
   596	
   597	```bash
   598	# Using Polygon.io
   599	curl https://localhost:7001/api/trading/analyze/BTCUSD/Polygon
   600	
   601	# Using TraderMade
   602	curl https://localhost:7001/api/trading/analyze/BTCUSD/TraderMade
   603	
   604	# Using TwelveData
   605	curl https://localhost:7001/api/trading/analyze/BTCUSD/TwelveData
   606	
   607	# Using Mock data
   608	curl https://localhost:7001/api/trading/analyze/BTCUSD/Mock
   609	```
   610	
   611	If you don't specify a provider, the system will use the default provider based on available API keys:
   612	1. Polygon.io (if configured)
   613	2. TraderMade (if configured)
   614	3. TwelveData (if configured)
   615	4. Mock provider (fallback)
   616	
   617	## Troubleshooting
   618	
   619	### API Key Issues
   620	
   621	If you encounter problems with the API keys:
   622	
   623	1. Use the configuration check endpoint to verify your keys:
   624	   ```bash
   625	   curl https://localhost:7001/api/diagnostics/config
   626	   ```
   627	
   628	2. For Perplexity API issues:
   629	   - Ensure the API key starts with "pplx-"
   630	   - Check rate limits on your Perplexity account
   631	   - Try the test endpoint to verify key validity:
   632	     ```bash
   633	     curl -X POST https://localhost:7001/api/diagnostics/set-perplexity-key \
   634	       -H "Content-Type: application/json" \
   635	       -d '{"apiKey": "your-pplx-key-here", "saveToUserSecrets": false}'
   636	     ```
   637	
   638	3. For Polygon.io API issues:
   639	   - Verify your subscription is active
   640	   - Check usage limits on your Polygon.io dashboard
   641	   - Try the test endpoint to verify key validity:
   642	     ```bash
   643	     curl -X POST https://localhost:7001/api/diagnostics/set-polygon-key \
   644	       -H "Content-Type: application/json" \
   645	       -d '{"apiKey": "your-polygon-key-here", "saveToUserSecrets": false}'
   646	     ```
   647	
   648	4. For TraderMade API issues:
   649	   - Verify your subscription is active
   650	   - Check usage limits on your TraderMade dashboard
   651	   - Try the test endpoint to verify key validity:
   652	     ```bash
   653	     curl -X POST https://localhost:7001/api/diagnostics/set-tradermade-key \
   654	       -H "Content-Type: application/json" \
   655	       -d '{"apiKey": "your-tradermade-key-here", "saveToUserSecrets": false}'
   656	     ```
   657	
   658	5. For TwelveData API issues:
   659	   - Verify your subscription is active
   660	   - Check usage limits on your TwelveData dashboard
   661	   - Try the test endpoint to verify key validity:
   662	     ```bash
   663	     curl -X POST https://localhost:7001/api/diagnostics/set-twelvedata-key \
   664	       -H "Content-Type: application/json" \
   665	       -d '{"apiKey": "your-twelvedata-key-here", "saveToUserSecrets": false}'
   666	     ```
   667	
   668	### Data Provider Fallback
   669	
   670	If no data provider API keys are configured or they return errors, the system will automatically fall back to using the mock data provider. This allows you to test the API functionality without real market data.
   671	
   672	### TraderMade API Limitations
   673	
   674	If you encounter errors when using TraderMade, be aware of these limitations:
   675	
   676	1. **Minute Data Restriction**: TraderMade limits minute-based data (5min, 15min) to 2 working days per request. If you need more historical data, consider using hourly or daily timeframes.
   677	
   678	2. **Error Message**: If you see `"max 2 working days of 1 and 5 minute data allowed per request"`, it means you're trying to fetch too much minute-based data.
   679	
   680	3. **Response Format Variations**: TraderMade API may return OHLC values as either strings or numbers in different responses. Our implementation handles both formats.
   681	
   682	4. **Live Rates Enhancement**: The system automatically fetches live rates to update the most recent candle's close price for better accuracy. If you see different prices compared to historical data, this is expected and provides more accurate current prices.
   683	
   684	5. **Free Tier Limitations**: The free tier has additional restrictions on the number of API calls and data points. Check your usage on the TraderMade dashboard.
   685	
   686	6. **Workaround**: For analysis that requires more historical minute data, use Polygon.io or TwelveData instead:
   687	   ```bash
   688	   curl https://localhost:7001/api/trading/analyze/BTCUSD/Polygon
   689	   # or
   690	   curl https://localhost:7001/api/trading/analyze/BTCUSD/TwelveData
   691	   ```
   692	
   693	## Position Sizing Calculator
   694	
   695	The API now includes a position sizing calculator that helps traders determine:
   696	
   697	1. The maximum position size they can take based on their account balance and leverage
   698	2. The position size required to achieve specific profit targets
   699	3. The risk associated with each position
   700	
   701	### Using the Position Sizing Calculator
   702	
   703	By default, the calculator assumes:
   704	- Account balance: 201 GBP
   705	- Leverage: 1:1000
   706	- Default profit targets: 50, 100, 200, 500, and 1000 GBP
   707	
   708	You can customize these parameters in your API requests using query parameters:
   709	
   710	- `accountBalance`: Your trading account balance in GBP (default: 201)
   711	- `leverage`: Your account leverage as a number (e.g., 1000 for 1:1000 leverage)
   712	- `targetProfits`: Comma-separated list of profit targets in GBP
   713	
   714	#### Example cURL Commands
   715	
   716	**Basic usage with default parameters:**
   717	```bash
   718	curl "https://localhost:7001/api/trading/analyze/EURUSD/TraderMade"
   719	```
   720	
   721	**Custom account balance and leverage:**
   722	```bash
   723	curl "https://localhost:7001/api/trading/analyze/EURUSD/TraderMade?accountBalance=500&leverage=500"
   724	```
   725	
   726	**Custom profit targets:**
   727	```bash
   728	curl "https://localhost:7001/api/trading/analyze/EURUSD/TraderMade?targetProfits=100,200,300"
   729	```
   730	
   731	**All parameters combined:**
   732	```bash
   733	curl "https://localhost:7001/api/trading/analyze/EURUSD/TraderMade?accountBalance=500&leverage=500&targetProfits=100,200,300"
   734	```
   735	
   736	**With TwelveData provider:**
   737	```bash
   738	curl "https://localhost:7001/api/trading/analyze/NZDCAD/TwelveData?accountBalance=201&leverage=1000&targetProfits=1,2,3"
   739	```
   740	
   741	**With trading recommendations endpoint:**
   742	```bash
   743	curl "https://localhost:7001/api/trading/recommendations?count=3&accountBalance=1000&leverage=200&targetProfits=500,1000,2000"
   744	```
   745	
   746	### Specifying Multiple Target Profits
   747	
   748	The `targetProfits` parameter accepts a comma-separated list of values, allowing you to calculate position sizes for multiple profit targets simultaneously. For example:
   749	
   750	- `targetProfits=50,100,200` - Calculate position sizes needed to make 50, 100, and 200 GBP profit
   751	- `targetProfits=100,500,1000,5000` - Calculate for larger profit targets
   752	- `targetProfits=10,25,50,75,100` - Calculate for smaller, more granular profit targets
   753	
   754	This is particularly useful for:
   755	- Planning different profit scenarios (conservative, moderate, aggressive)
   756	- Understanding how position size scales with profit targets
   757	- Finding the optimal risk-reward balance for your trading style
   758	
   759	### Position Sizing Response Example
   760	
   761	The API response includes a `positionSizing` object with the following information:
   762	
   763	```json
   764	{
   765	  // ... other response fields ...
   766	  "positionSizing": {
   767	    "accountBalance": 201,
   768	    "leverage": 1000,
   769	    "symbol": "EURUSD",
   770	    "currentPrice": 1.0876,
   771	    "maxPositionSize": 201000,
   772	    "maxLotSize": 2.01,
   773	    "profitTargets": {
   774	      "50": {
   775	        "targetProfit": 50,
   776	        "requiredPositionSize": 50000,
   777	        "requiredLotSize": 0.5,
   778	        "priceMovementRequired": 0.00108,
   779	        "priceMovementPercent": 0.1,
   780	        "riskAmount": 50,
   781	        "riskPercentage": 24.88
   782	      },
   783	      "100": {
   784	        "targetProfit": 100,
   785	        "requiredPositionSize": 100000,
   786	        "requiredLotSize": 1.0,
   787	        "priceMovementRequired": 0.00217,
   788	        "priceMovementPercent": 0.2,
   789	        "riskAmount": 100,
   790	        "riskPercentage": 49.75
   791	      }
   792	      // Additional targets...
   793	    }
   794	  }
   795	}
   796	```
   797	
   798	### Understanding the Position Sizing Response
   799	
   800	- **accountBalance**: Your trading account balance in GBP
   801	- **leverage**: Your account leverage (e.g., 1000 for 1:1000)
   802	- **symbol**: The trading pair being analyzed
   803	- **currentPrice**: The current market price of the symbol
   804	- **maxPositionSize**: The maximum position size you can take with your account and leverage
   805	- **maxLotSize**: The maximum position size expressed in standard lots
   806	
   807	For each profit target, you'll see:
   808	- **targetProfit**: The profit target in GBP
   809	- **requiredPositionSize**: The position size needed to achieve this profit target
   810	- **requiredLotSize**: The position size expressed in standard lots
   811	- **priceMovementRequired**: The price movement needed to achieve the profit target
   812	- **priceMovementPercent**: The price movement as a percentage
   813	- **riskAmount**: The amount at risk in GBP (assuming 1:1 risk-reward)
   814	- **riskPercentage**: The risk as a percentage of your account balance
   815	
   816	This information helps traders understand:
   817	- How much they can trade with their current account
   818	- What position size is needed to achieve specific profit targets
   819	- How much risk they're taking on with each position
   820	- What price movement is required to reach their profit goals