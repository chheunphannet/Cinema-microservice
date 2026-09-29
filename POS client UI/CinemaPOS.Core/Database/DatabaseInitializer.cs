using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CinemaPOS.Core.Database
{
    public class DatabaseInitializer
    {
        private readonly string _connectionString;

        public DatabaseInitializer(string connectionString)
        {
            _connectionString = connectionString;
        }

        public void Initialize()
        {
            var builder = new SqlConnectionStringBuilder(_connectionString);
            string dbName = builder.InitialCatalog;
            builder.InitialCatalog = "master";
            
            using (var masterConn = new SqlConnection(builder.ConnectionString))
            {
                masterConn.Open();
                
                var exists = masterConn.ExecuteScalar<int?>($"SELECT 1 FROM sys.databases WHERE name = '{dbName}'");
                if (exists == null)
                {
                    masterConn.Execute($"CREATE DATABASE [{dbName}]");
                }
            }

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                string createShifts = @"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='LocalShifts' and xtype='U')
                CREATE TABLE LocalShifts (
                    ShiftId UNIQUEIDENTIFIER PRIMARY KEY,
                    BranchId UNIQUEIDENTIFIER,
                    CashierId UNIQUEIDENTIFIER,
                    TerminalCode NVARCHAR(50),
                    OpeningFloat DECIMAL(18,2),
                    Status NVARCHAR(20),
                    OpenedAt DATETIME
                );";

                string createParked = @"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ParkedTransactions' and xtype='U')
                CREATE TABLE ParkedTransactions (
                    TransactionId UNIQUEIDENTIFIER PRIMARY KEY,
                    HoldId UNIQUEIDENTIFIER,
                    ShowtimeId UNIQUEIDENTIFIER,
                    SeatIdsJson NVARCHAR(MAX),
                    Subtotal DECIMAL(18,2),
                    ParkedAt DATETIME
                );";

                string createMovies = @"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='LocalMovies' and xtype='U')
                CREATE TABLE LocalMovies (
                    MovieId UNIQUEIDENTIFIER PRIMARY KEY,
                    Title NVARCHAR(255),
                    PosterUrl NVARCHAR(500),
                    Genre NVARCHAR(100),
                    CachedAt DATETIME
                );";

                string createShowtimes = @"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='LocalShowtimes' and xtype='U')
                CREATE TABLE LocalShowtimes (
                    ShowtimeId UNIQUEIDENTIFIER PRIMARY KEY,
                    MovieId UNIQUEIDENTIFIER,
                    MovieTitle NVARCHAR(255),
                    AuditoriumName NVARCHAR(100),
                    ScreenType NVARCHAR(50),
                    StartTime DATETIME,
                    BasePrice DECIMAL(18,2),
                    Status NVARCHAR(20),
                    CachedAt DATETIME
                );";

                string createProducts = @"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='LocalProducts' and xtype='U')
                CREATE TABLE LocalProducts (
                    ProductId UNIQUEIDENTIFIER PRIMARY KEY,
                    Name NVARCHAR(255),
                    Category NVARCHAR(100),
                    Price DECIMAL(18,2),
                    ImageUrl NVARCHAR(500),
                    StockLevel INT,
                    CachedAt DATETIME
                );";

                string createAudit = @"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='LocalOrderAudit' and xtype='U')
                CREATE TABLE LocalOrderAudit (
                    AuditId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
                    OrderId UNIQUEIDENTIFIER,
                    ShiftId UNIQUEIDENTIFIER,
                    TotalAmount DECIMAL(18,2),
                    PaymentMethod NVARCHAR(50),
                    IsVoided BIT DEFAULT 0,
                    CreatedAt DATETIME
                );";

                string createTicketTypes = @"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='LocalTicketTypes' and xtype='U')
                CREATE TABLE LocalTicketTypes (
                    TicketTypeId UNIQUEIDENTIFIER PRIMARY KEY,
                    Name NVARCHAR(50),
                    PriceModifier DECIMAL(18,2),
                    CachedAt DATETIME
                );";

                connection.Execute(createShifts);
                connection.Execute(createParked);
                connection.Execute(createMovies);
                connection.Execute(createShowtimes);
                connection.Execute(createProducts);
                connection.Execute(createAudit);
                connection.Execute(createTicketTypes);
            }
        }
    }
}
