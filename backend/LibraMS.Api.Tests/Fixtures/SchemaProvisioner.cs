using System.Text.RegularExpressions;

namespace LibraMS.Api.Tests.Fixtures;

/// <summary>
/// Loads the committed <c>schema.sql</c> so tests can provision a database from the same
/// file an evaluator would run, rather than from a hand-maintained copy that can drift.
/// BUG-1 shipped precisely because no test exercised the committed schema.
/// </summary>
public static class SchemaProvisioner
{
    /// <summary>
    /// Statements that exist only to wire into Supabase-managed objects and cannot run against a
    /// plain Postgres instance: the <c>auth.users</c> sign-up trigger and its function, and the
    /// RLS policies, which are all expressed in terms of <c>auth.uid()</c>.
    /// </summary>
    private static readonly string[] SupabaseOnlyMarkers =
    [
        "auth.uid()",
        "ON auth.users",
        "handle_new_user",
        "ENABLE ROW LEVEL SECURITY",
        "CREATE POLICY",
    ];

    public static string SchemaPath
    {
        get
        {
            var dir = AppContext.BaseDirectory;
            while (dir is not null)
            {
                var candidate = Path.Combine(dir, "LibraMS.Api", "Data", "schema.sql");
                if (File.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            throw new FileNotFoundException(
                "Could not locate LibraMS.Api/Data/schema.sql by walking up from " + AppContext.BaseDirectory);
        }
    }

    /// <summary>
    /// Returns the portable statements from <c>schema.sql</c>, in file order.
    /// </summary>
    public static IReadOnlyList<string> PortableStatements()
    {
        var sql = File.ReadAllText(SchemaPath);

        // Strip dollar-quoted function bodies before splitting, so a ';' inside a body is
        // not mistaken for a statement terminator.
        var bodies = new List<string>();
        sql = Regex.Replace(sql, @"\$\$.*?\$\$", m =>
        {
            bodies.Add(m.Value);
            return $"$$PLACEHOLDER_{bodies.Count - 1}$$";
        }, RegexOptions.Singleline);

        var statements = new List<string>();
        foreach (var raw in sql.Split(';'))
        {
            var stmt = Regex.Replace(raw, @"^\s*--.*$", "", RegexOptions.Multiline).Trim();
            if (stmt.Length == 0) continue;

            stmt = Regex.Replace(stmt, @"\$\$PLACEHOLDER_(\d+)\$\$", m => bodies[int.Parse(m.Groups[1].Value)]);

            if (SupabaseOnlyMarkers.Any(marker => stmt.Contains(marker, StringComparison.OrdinalIgnoreCase)))
                continue;

            // library_users.id is a FK into Supabase's auth.users, which does not exist on a
            // plain Postgres. Drop just that clause so the table itself is still created from
            // the committed definition.
            stmt = Regex.Replace(stmt, @"\s+REFERENCES\s+auth\.users\s*\([^)]*\)(\s+ON\s+DELETE\s+CASCADE)?", "",
                RegexOptions.IgnoreCase);

            statements.Add(stmt);
        }
        return statements;
    }
}
