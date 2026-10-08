using System.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Primworth_API.Repository
{
    /// <summary>
    /// The platform's database handle. Applications receive it by injection and call
    /// <see cref="CreateConnection"/>; they never hold a connection string of their own.
    /// </summary>
    public class DapperContext
    {
        private readonly string _connectionString;

        public DapperContext(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DBConnection")
                ?? throw new InvalidOperationException("ConnectionStrings:DBConnection is not configured.");
        }

        public IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);
    }
}
