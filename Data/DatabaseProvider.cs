namespace DirectorComercialIA.Data;

public static class DatabaseProvider
{
    public static bool IsSqlServer(string connectionString) =>
        connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase)
        || connectionString.Contains("User ID=", StringComparison.OrdinalIgnoreCase)
        || connectionString.Contains("Uid=", StringComparison.OrdinalIgnoreCase);
}
