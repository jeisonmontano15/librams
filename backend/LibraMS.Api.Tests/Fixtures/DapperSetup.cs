using System.Runtime.CompilerServices;
using Dapper;

namespace LibraMS.Api.Tests.Fixtures;

/// <summary>
/// The API sets <see cref="DefaultTypeMap.MatchNamesWithUnderscores"/> in <c>Program.cs</c>,
/// which never runs under the test host — the repository tests construct repositories directly.
/// Without it every snake_case column (<c>book_id</c>, <c>user_id</c>, <c>status_raw</c>) maps
/// to its default, so assertions on mapped values compare against zero rather than real data.
/// This initializer applies the same global Dapper configuration for the test assembly.
/// </summary>
internal static class DapperSetup
{
    [ModuleInitializer]
    internal static void Init() => DefaultTypeMap.MatchNamesWithUnderscores = true;
}
