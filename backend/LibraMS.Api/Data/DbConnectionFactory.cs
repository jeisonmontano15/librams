using Npgsql;
using System.Data;

namespace LibraMS.Api.Data;

public class DbConnectionFactory(IConfiguration config)
{
    private readonly string _connectionString =
        config.GetConnectionString("Supabase")
        ?? throw new InvalidOperationException("Connection string 'Supabase' not found.");

    public IDbConnection Create() => new NpgsqlConnection(_connectionString);
}
