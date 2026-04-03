using Dapper;
using LibraMS.Api.Models;

namespace LibraMS.Api.Data;

public interface IUserRepository
{
    Task<LibraryUser?> GetByIdAsync(Guid id);
    Task EnsureExistsAsync(Guid id, string email, string? displayName);
}

public class UserRepository(DbConnectionFactory db) : IUserRepository
{
    public async Task<LibraryUser?> GetByIdAsync(Guid id)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<LibraryUser>(
            "SELECT * FROM public.library_users WHERE id = @id", new { id });
    }

    public async Task EnsureExistsAsync(Guid id, string email, string? displayName)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync("""
            INSERT INTO public.library_users (id, email, display_name)
            VALUES (@id, @email, @displayName)
            ON CONFLICT (id) DO UPDATE
                SET email        = EXCLUDED.email,
                    display_name = COALESCE(EXCLUDED.display_name, public.library_users.display_name)
            """, new { id, email, displayName });
    }
}
