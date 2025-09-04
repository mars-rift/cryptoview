using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using cryptoview.Models;

namespace cryptoview.Services
{
    public class DataService
    {
        private readonly string _connectionString;
        private readonly string _dbPath;
        private bool _migrationSuccessful = false;

        public DataService()
        {
            _dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CryptoView", "cryptoview.db");
            _connectionString = $"Data Source={_dbPath}";
            InitializeDatabase();
            
            // Clean up any invalid entries after initialization (fire-and-forget with error handling)
            _ = Task.Run(async () => 
            {
                try 
                { 
                    await CleanupFavoritesAsync(); 
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error during cleanup: {ex.Message}");
                }
            });
        }

        public Task ResetDatabaseAsync()
        {
            try
            {
                if (File.Exists(_dbPath))
                {
                    File.Delete(_dbPath);
                }
                InitializeDatabase();
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error resetting database: {ex.Message}");
                throw;
            }
        }

        private void InitializeDatabase()
        {
            var directory = Path.GetDirectoryName(_dbPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory!);
            }

            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();

            // First, create tables with basic schema that will always work
            var createBasicTables = @"
                CREATE TABLE IF NOT EXISTS HistoricalPrices (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Symbol TEXT NOT NULL,
                    Price DECIMAL NOT NULL,
                    Timestamp DATETIME NOT NULL
                );

                CREATE TABLE IF NOT EXISTS PriceAlerts (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Symbol TEXT NOT NULL,
                    TargetPrice DECIMAL NOT NULL,
                    AlertType INTEGER NOT NULL,
                    IsEnabled BOOLEAN NOT NULL,
                    CreatedAt DATETIME NOT NULL,
                    Message TEXT
                );

                CREATE TABLE IF NOT EXISTS Favorites (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Symbol TEXT NOT NULL UNIQUE
                );

                CREATE TABLE IF NOT EXISTS Settings (
                    Key TEXT PRIMARY KEY,
                    Value TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_historical_symbol_timestamp 
                ON HistoricalPrices(Symbol, Timestamp);
            ";

            using var command = new SQLiteCommand(createBasicTables, connection);
            command.ExecuteNonQuery();

            // Handle migration for Favorites table - add new columns if they don't exist
            try
            {
                var checkColumns = "PRAGMA table_info(Favorites)";
                using var checkCommand = new SQLiteCommand(checkColumns, connection);
                using var reader = checkCommand.ExecuteReader();
                
                var existingColumns = new HashSet<string>();
                
                while (reader.Read())
                {
                    string columnName = reader.GetString(1); // Column name is at index 1
                    existingColumns.Add(columnName.ToLower());
                }
                reader.Close();

                // Add missing columns one by one
                if (!existingColumns.Contains("base"))
                {
                    using var addBase = new SQLiteCommand("ALTER TABLE Favorites ADD COLUMN Base TEXT", connection);
                    addBase.ExecuteNonQuery();
                    System.Diagnostics.Debug.WriteLine("Added Base column to Favorites table");
                }
                if (!existingColumns.Contains("quote"))
                {
                    using var addQuote = new SQLiteCommand("ALTER TABLE Favorites ADD COLUMN Quote TEXT", connection);
                    addQuote.ExecuteNonQuery();
                    System.Diagnostics.Debug.WriteLine("Added Quote column to Favorites table");
                }
                if (!existingColumns.Contains("lastprice"))
                {
                    using var addPrice = new SQLiteCommand("ALTER TABLE Favorites ADD COLUMN LastPrice DECIMAL", connection);
                    addPrice.ExecuteNonQuery();
                    System.Diagnostics.Debug.WriteLine("Added LastPrice column to Favorites table");
                }
                if (!existingColumns.Contains("lastexchange"))
                {
                    using var addExchange = new SQLiteCommand("ALTER TABLE Favorites ADD COLUMN LastExchange TEXT", connection);
                    addExchange.ExecuteNonQuery();
                    System.Diagnostics.Debug.WriteLine("Added LastExchange column to Favorites table");
                }
                if (!existingColumns.Contains("createdat"))
                {
                    using var addCreatedAt = new SQLiteCommand("ALTER TABLE Favorites ADD COLUMN CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP", connection);
                    addCreatedAt.ExecuteNonQuery();
                    System.Diagnostics.Debug.WriteLine("Added CreatedAt column to Favorites table");
                }
                
                _migrationSuccessful = true;
                System.Diagnostics.Debug.WriteLine("Database migration completed successfully");
            }
            catch (Exception ex)
            {
                // If migration fails, try to recreate the table with the full schema
                System.Diagnostics.Debug.WriteLine($"Database migration failed: {ex.Message}");
                
                try
                {
                    using var dropTable = new SQLiteCommand("DROP TABLE IF EXISTS Favorites", connection);
                    dropTable.ExecuteNonQuery();
                    
                    var createNewFavorites = @"
                        CREATE TABLE Favorites (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Symbol TEXT NOT NULL UNIQUE,
                            Base TEXT,
                            Quote TEXT,
                            LastPrice DECIMAL,
                            LastExchange TEXT,
                            CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                        );
                    ";
                    
                    using var createCommand = new SQLiteCommand(createNewFavorites, connection);
                    createCommand.ExecuteNonQuery();
                    
                    _migrationSuccessful = true;
                    System.Diagnostics.Debug.WriteLine("Recreated Favorites table with full schema");
                }
                catch (Exception recreateEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to recreate Favorites table: {recreateEx.Message}");
                    _migrationSuccessful = false;
                }
            }
        }

        public async Task SaveHistoricalPriceAsync(HistoricalPrice price)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"INSERT INTO HistoricalPrices (Symbol, Price, Timestamp) 
                       VALUES (@Symbol, @Price, @Timestamp)";

            using var command = new SQLiteCommand(sql, connection);
            command.Parameters.AddWithValue("@Symbol", price.Symbol);
            command.Parameters.AddWithValue("@Price", price.Price);
            command.Parameters.AddWithValue("@Timestamp", price.Timestamp);

            await command.ExecuteNonQueryAsync();
        }

        public async Task<List<HistoricalPrice>> GetHistoricalPricesAsync(string symbol, DateTime from, DateTime to)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"SELECT Symbol, Price, Timestamp FROM HistoricalPrices 
                       WHERE Symbol = @Symbol AND Timestamp BETWEEN @From AND @To
                       ORDER BY Timestamp";

            using var command = new SQLiteCommand(sql, connection);
            command.Parameters.AddWithValue("@Symbol", symbol);
            command.Parameters.AddWithValue("@From", from);
            command.Parameters.AddWithValue("@To", to);

            var prices = new List<HistoricalPrice>();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                prices.Add(new HistoricalPrice
                {
                    Symbol = reader.GetString(0),
                    Price = reader.GetDecimal(1),
                    Timestamp = reader.GetDateTime(2)
                });
            }

            return prices;
        }

        public async Task SavePriceAlertAsync(PriceAlert alert)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"INSERT INTO PriceAlerts (Symbol, TargetPrice, AlertType, IsEnabled, CreatedAt, Message) 
                       VALUES (@Symbol, @TargetPrice, @AlertType, @IsEnabled, @CreatedAt, @Message)";

            using var command = new SQLiteCommand(sql, connection);
            command.Parameters.AddWithValue("@Symbol", alert.Symbol);
            command.Parameters.AddWithValue("@TargetPrice", alert.TargetPrice);
            command.Parameters.AddWithValue("@AlertType", (int)alert.Type);
            command.Parameters.AddWithValue("@IsEnabled", alert.IsEnabled);
            command.Parameters.AddWithValue("@CreatedAt", alert.CreatedAt);
            command.Parameters.AddWithValue("@Message", alert.Message ?? "");

            await command.ExecuteNonQueryAsync();
        }

        public async Task<List<PriceAlert>> GetPriceAlertsAsync()
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "SELECT Symbol, TargetPrice, AlertType, IsEnabled, CreatedAt, Message FROM PriceAlerts WHERE IsEnabled = 1";
            using var command = new SQLiteCommand(sql, connection);

            var alerts = new List<PriceAlert>();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                alerts.Add(new PriceAlert
                {
                    Symbol = reader.GetString(0),
                    TargetPrice = reader.GetDecimal(1),
                    Type = (AlertType)reader.GetInt32(2),
                    IsEnabled = reader.GetBoolean(3),
                    CreatedAt = reader.GetDateTime(4),
                    Message = reader.IsDBNull(5) ? null : reader.GetString(5)
                });
            }

            return alerts;
        }

        public async Task<List<string>> GetFavoriteSymbolsAsync()
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "SELECT Symbol FROM Favorites";
            using var command = new SQLiteCommand(sql, connection);

            var favorites = new List<string>();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                favorites.Add(reader.GetString(0));
            }

            return favorites;
        }

        public async Task<List<TradingPair>> GetDetailedFavoritesAsync()
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var favorites = new List<TradingPair>();

            // Use basic query if migration wasn't successful
            if (!_migrationSuccessful)
            {
                try
                {
                    var basicSql = "SELECT Symbol FROM Favorites";
                    using var basicCommand = new SQLiteCommand(basicSql, connection);
                    using var basicReader = await basicCommand.ExecuteReaderAsync();

                    while (await basicReader.ReadAsync())
                    {
                        var symbol = basicReader.GetString(0);
                        var parts = symbol.Split('/');
                        
                        var favorite = new TradingPair
                        {
                            Base = parts.Length > 0 ? parts[0] : "Unknown",
                            Quote = parts.Length > 1 && !string.IsNullOrEmpty(parts[1]) ? parts[1] : "Unknown",
                            PriceUsd = 0,
                            FormattedTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " (Saved)",
                            IsFavorite = true
                        };

                        favorites.Add(favorite);
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"Used basic query, got {favorites.Count} favorites");
                    return favorites;
                }
                catch (Exception basicEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Error with basic favorites query: {basicEx.Message}");
                    return favorites;
                }
            }

            // Try enhanced query first
            try
            {
                var sql = @"SELECT Symbol, Base, Quote, LastPrice, LastExchange, CreatedAt 
                           FROM Favorites ORDER BY CreatedAt DESC";
                using var command = new SQLiteCommand(sql, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var storedSymbol = reader.GetString(0); // The Symbol column as stored in DB
                    var base_ = reader.IsDBNull(1) ? "Unknown" : reader.GetString(1);
                    var quote = reader.IsDBNull(2) ? "Unknown" : reader.GetString(2);
                    
                    // Ensure we reconstruct Base/Quote from the stored symbol if they're missing
                    if ((base_ == "Unknown" || quote == "Unknown") && !string.IsNullOrEmpty(storedSymbol))
                    {
                        var parts = storedSymbol.Split('/');
                        if (parts.Length >= 2)
                        {
                            base_ = parts[0];
                            quote = parts[1];
                        }
                    }
                    
                    var favorite = new TradingPair
                    {
                        Base = base_,
                        Quote = quote,
                        PriceUsd = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                        FormattedTime = reader.IsDBNull(5) ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") : 
                                       DateTime.Parse(reader.GetString(5)).ToString("yyyy-MM-dd HH:mm:ss"),
                        IsFavorite = true
                    };

                    // Add exchange info to formatted time for clarity
                    if (!reader.IsDBNull(4))
                    {
                        var exchange = reader.GetString(4);
                        favorite.FormattedTime += $" ({exchange})";
                    }

                    favorites.Add(favorite);
                }
                
                System.Diagnostics.Debug.WriteLine($"Used enhanced query, got {favorites.Count} favorites");
            }
            catch (Exception ex) when (ex.Message.Contains("no column named") || ex.Message.Contains("no such column"))
            {
                // Fallback to basic symbol-only query
                System.Diagnostics.Debug.WriteLine($"Enhanced query failed, using fallback: {ex.Message}");
                try
                {
                    var fallbackSql = "SELECT Symbol FROM Favorites";
                    using var fallbackCommand = new SQLiteCommand(fallbackSql, connection);
                    using var fallbackReader = await fallbackCommand.ExecuteReaderAsync();

                    while (await fallbackReader.ReadAsync())
                    {
                        var symbol = fallbackReader.GetString(0);
                        var parts = symbol.Split('/');
                        
                        var favorite = new TradingPair
                        {
                            Base = parts.Length > 0 ? parts[0] : "Unknown",
                            Quote = parts.Length > 1 && !string.IsNullOrEmpty(parts[1]) ? parts[1] : "Unknown",
                            PriceUsd = 0,
                            FormattedTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " (Legacy)",
                            IsFavorite = true
                        };

                        favorites.Add(favorite);
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"Used fallback query, got {favorites.Count} favorites");
                }
                catch (Exception fallbackEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Error getting favorites: {ex.Message}, Fallback error: {fallbackEx.Message}");
                }
            }

            return favorites;
        }

        public async Task AddFavoriteAsync(string symbol, string? baseCurrency = null, string? quoteCurrency = null, decimal? lastPrice = null, string? exchange = null)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            // Use basic insert if migration wasn't successful
            if (!_migrationSuccessful)
            {
                try
                {
                    var basicSql = "INSERT OR REPLACE INTO Favorites (Symbol) VALUES (@Symbol)";
                    using var basicCommand = new SQLiteCommand(basicSql, connection);
                    basicCommand.Parameters.AddWithValue("@Symbol", symbol);
                    await basicCommand.ExecuteNonQueryAsync();
                    System.Diagnostics.Debug.WriteLine($"Added favorite using basic insert: {symbol}");
                    return;
                }
                catch (Exception basicEx)
                {
                    throw new Exception($"Failed to add favorite with basic insert: {basicEx.Message}");
                }
            }

            try
            {
                // Try the full insert with all columns
                var sql = @"INSERT OR REPLACE INTO Favorites (Symbol, Base, Quote, LastPrice, LastExchange, CreatedAt) 
                           VALUES (@Symbol, @Base, @Quote, @LastPrice, @LastExchange, @CreatedAt)";
                using var command = new SQLiteCommand(sql, connection);
                command.Parameters.AddWithValue("@Symbol", symbol);
                command.Parameters.AddWithValue("@Base", baseCurrency ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Quote", quoteCurrency ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@LastPrice", lastPrice ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@LastExchange", exchange ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

                await command.ExecuteNonQueryAsync();
                System.Diagnostics.Debug.WriteLine($"Added favorite using enhanced insert: {symbol}");
            }
            catch (Exception ex) when (ex.Message.Contains("no column named") || ex.Message.Contains("no such column"))
            {
                // Fallback to basic insert if columns don't exist yet
                try
                {
                    var fallbackSql = "INSERT OR REPLACE INTO Favorites (Symbol) VALUES (@Symbol)";
                    using var fallbackCommand = new SQLiteCommand(fallbackSql, connection);
                    fallbackCommand.Parameters.AddWithValue("@Symbol", symbol);
                    await fallbackCommand.ExecuteNonQueryAsync();
                    System.Diagnostics.Debug.WriteLine($"Added favorite using fallback insert: {symbol}");
                }
                catch (Exception fallbackEx)
                {
                    throw new Exception($"Failed to add favorite. Original error: {ex.Message}, Fallback error: {fallbackEx.Message}");
                }
            }
        }

        public async Task RemoveFavoriteAsync(string symbol)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            // Delete all entries with this symbol (in case of duplicates)
            var sql = "DELETE FROM Favorites WHERE Symbol = @Symbol";
            using var command = new SQLiteCommand(sql, connection);
            command.Parameters.AddWithValue("@Symbol", symbol);

            var rowsAffected = await command.ExecuteNonQueryAsync();
            System.Diagnostics.Debug.WriteLine($"RemoveFavoriteAsync: Deleted {rowsAffected} rows for symbol {symbol}");
        }

        public async Task CleanupFavoritesAsync()
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            // Remove duplicates and invalid entries
            var cleanupSql = @"
                DELETE FROM Favorites 
                WHERE rowid NOT IN (
                    SELECT MIN(rowid) 
                    FROM Favorites 
                    GROUP BY Symbol
                )
                OR Symbol IS NULL 
                OR Symbol = ''
                OR Symbol LIKE '%/'
                OR Symbol LIKE '/%'";
            
            using var command = new SQLiteCommand(cleanupSql, connection);
            var rowsAffected = await command.ExecuteNonQueryAsync();
            System.Diagnostics.Debug.WriteLine($"CleanupFavoritesAsync: Cleaned up {rowsAffected} invalid/duplicate entries");
        }

        public async Task DeletePriceAlertAsync(PriceAlert alert)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"DELETE FROM PriceAlerts 
                       WHERE Symbol = @Symbol AND TargetPrice = @TargetPrice AND AlertType = @AlertType AND CreatedAt = @CreatedAt";
            using var command = new SQLiteCommand(sql, connection);
            command.Parameters.AddWithValue("@Symbol", alert.Symbol);
            command.Parameters.AddWithValue("@TargetPrice", alert.TargetPrice);
            command.Parameters.AddWithValue("@AlertType", (int)alert.Type);
            command.Parameters.AddWithValue("@CreatedAt", alert.CreatedAt);

            await command.ExecuteNonQueryAsync();
        }

        public async Task ClearAllPriceAlertsAsync()
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "DELETE FROM PriceAlerts";
            using var command = new SQLiteCommand(sql, connection);

            await command.ExecuteNonQueryAsync();
        }

        public async Task UpdatePriceAlertAsync(PriceAlert alert)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"UPDATE PriceAlerts 
                       SET IsEnabled = @IsEnabled 
                       WHERE Symbol = @Symbol AND TargetPrice = @TargetPrice AND AlertType = @AlertType AND CreatedAt = @CreatedAt";
            using var command = new SQLiteCommand(sql, connection);
            command.Parameters.AddWithValue("@IsEnabled", alert.IsEnabled);
            command.Parameters.AddWithValue("@Symbol", alert.Symbol);
            command.Parameters.AddWithValue("@TargetPrice", alert.TargetPrice);
            command.Parameters.AddWithValue("@AlertType", (int)alert.Type);
            command.Parameters.AddWithValue("@CreatedAt", alert.CreatedAt);

            await command.ExecuteNonQueryAsync();
        }

        public async Task SaveSettingAsync(string key, string value)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"INSERT OR REPLACE INTO Settings (Key, Value) VALUES (@Key, @Value)";
            using var command = new SQLiteCommand(sql, connection);
            command.Parameters.AddWithValue("@Key", key);
            command.Parameters.AddWithValue("@Value", value);

            await command.ExecuteNonQueryAsync();
        }

        public async Task<string?> GetSettingAsync(string key)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "SELECT Value FROM Settings WHERE Key = @Key";
            using var command = new SQLiteCommand(sql, connection);
            command.Parameters.AddWithValue("@Key", key);

            var result = await command.ExecuteScalarAsync();
            return result?.ToString();
        }
    }
}
