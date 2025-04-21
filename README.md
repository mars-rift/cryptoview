# CryptoView

A desktop application built with WPF/.NET that displays cryptocurrency exchange data using the Coinlore API. This application allows users to select from various cryptocurrency exchanges and view their trading pairs with real-time or cached data.

![CryptoView Screenshot](image.png)

## Features

- **Live Exchange Data**: Fetch cryptocurrency exchange information from the Coinlore API
- **Multiple Exchange Support**: View trading pairs from various exchanges
- **Robust Parsing**: Handles multiple API response formats with fallback mechanisms
- **Real-time Data**: Automatically refreshes outdated price timestamps
- **Custom Styling**: Sleek cyberpunk-themed UI with custom controls
- **Responsive Interface**: Async operations keep the UI responsive during data loading

## Getting Started

### Prerequisites

- Windows operating system
- .NET 6.0 or later
- Visual Studio 2019/2022 (recommended) or other .NET IDE

### Installation

1. Clone the repository:
   ```
   git clone https://github.com/mars-rift/cryptoview.git
   ```

2. Open the solution file in Visual Studio:
   ```
   cryptoview.sln
   ```

3. Build and run the application:
   - Press F5 in Visual Studio, or
   - Use the command line: `dotnet build` followed by `dotnet run`

## Usage

1. When the application starts, it automatically loads a list of available exchanges
2. Select an exchange from the dropdown menu
3. Click the "LOAD DATA" button to fetch trading pairs for the selected exchange
4. View exchange information and trading pairs in the data grid
5. The status bar at the bottom provides feedback on operations

## Code Structure

- **MainWindow.xaml / MainWindow.xaml.cs**: Main application UI and logic
- **Data Models**:
  - `Exchange`: Basic exchange information
  - `ExchangeInfo`: Detailed exchange information including name, founding date, and URL
  - `TradingPair`: Trading pair data including base/quote currencies, price, volume, and timestamp
  - `ExchangeData`: Container for exchange info and pairs with JSON deserialization support

## Technical Details

- **HTTP Client**: Uses `HttpClient` for API requests
- **JSON Parsing**: Uses System.Text.Json for deserialization with fallback parsing strategies
- **Async/Await**: All network operations are asynchronous for UI responsiveness
- **Error Handling**: Comprehensive try/catch blocks with user feedback
- **Timestamp Management**: Converts Unix timestamps to readable datetime strings

## API Usage

The application uses the Coinlore API:
- Exchange list: `https://api.coinlore.net/api/exchanges/`
- Exchange details: `https://api.coinlore.net/api/exchange/?id={exchangeId}`

## Contributing

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Acknowledgments

- [Coinlore API](https://www.coinlore.com/cryptocurrency-data-api) for providing cryptocurrency exchange data
