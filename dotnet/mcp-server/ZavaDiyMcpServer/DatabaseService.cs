using System.Data.Common;
using Npgsql;

namespace ZavaDiyMcpServer;

public interface IDatabaseService
{
    DbConnection CreateConnection();
}

public class DatabaseService : IDatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("PostgreSQL")
            ?? throw new InvalidOperationException("PostgreSQL connection string is not configured.");
    }

    public DbConnection CreateConnection() => new NpgsqlConnection(_connectionString);
}