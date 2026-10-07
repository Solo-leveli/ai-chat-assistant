using System.Data;
using System.Text.RegularExpressions;
using BoltAI.Application;
using BoltAI.Domain;
using Microsoft.Data.SqlClient;
namespace BoltAI.Infrastructure;

// Caller supplies CONFIRMED procedure/parameter/result contracts. No NAIMS schema is assumed.
public sealed record ApprovedProcedure(string Name, string ScopeParameter, string AccountTableType, string AccountColumn, string? IdentifierParameter);
public sealed record SqlRepositoryContracts(
    ApprovedProcedure Order, ApprovedProcedure History, ApprovedProcedure Stock, ApprovedProcedure Shipment,
    Func<SqlDataReader, OrderRecord> ReadOrder, Func<SqlDataReader, StockRecord> ReadStock);
public sealed class SqlBusinessRepository(string connectionString, SqlRepositoryContracts contracts, int commandTimeoutSeconds = 15) : IBusinessRepository
{
    public async Task<OrderRecord?> GetOrderAsync(string number, IReadOnlySet<string> accounts, CancellationToken ct)
        => (await Execute(contracts.Order, number, accounts, contracts.ReadOrder, ct)).FirstOrDefault(o => accounts.Contains(o.AccountId) && o.OrderNumber == number);
    public async Task<IReadOnlyList<OrderRecord>> GetHistoryAsync(IReadOnlySet<string> accounts, CancellationToken ct)
        => (await Execute(contracts.History, null, accounts, contracts.ReadOrder, ct)).Where(o => accounts.Contains(o.AccountId)).OrderByDescending(o => o.LastUpdated).Take(10).ToArray();
    public async Task<StockRecord?> GetStockAsync(string code, IReadOnlySet<string> accounts, CancellationToken ct)
    {
        var rows = (await Execute(contracts.Stock, code, accounts, contracts.ReadStock, ct)).Where(s => accounts.Contains(s.AccountId) && s.ProductCode == code).ToArray();
        return rows.Length == 0 ? null : rows[0] with { Quantity = checked(rows.Sum(s => s.Quantity)), Location = string.Join("; ", rows.Select(s => s.Location)) };
    }
    public async Task<OrderRecord?> GetShipmentAsync(string shipment, IReadOnlySet<string> accounts, CancellationToken ct)
        => (await Execute(contracts.Shipment, shipment, accounts, contracts.ReadOrder, ct)).FirstOrDefault(o => accounts.Contains(o.AccountId) && o.ShipmentId == shipment);
    private async Task<IReadOnlyList<T>> Execute<T>(ApprovedProcedure procedure, string? identifier, IReadOnlySet<string> accounts, Func<SqlDataReader, T> map, CancellationToken ct)
    {
        if (accounts.Count == 0) return [];
        if (accounts.Count > 100 || commandTimeoutSeconds is < 1 or > 60) throw new SafeFailure("dependency_unavailable", "Verified business information is currently unavailable.");
        ValidateContract(procedure);
        // Secure defaults even if deployment mistakenly passes a weaker connection string.
        var settings = new SqlConnectionStringBuilder(connectionString) { Encrypt = SqlConnectionEncryptOption.Mandatory, TrustServerCertificate = false };
        await using var connection = new SqlConnection(settings.ConnectionString);
        await using var command = CreateCommand(connection, procedure, identifier, accounts, commandTimeoutSeconds);
        try
        {
            await connection.OpenAsync(ct);
            await using var reader = await command.ExecuteReaderAsync(ct);
            var results = new List<T>();
            while (await reader.ReadAsync(ct)) { if (results.Count == 100) throw new SafeFailure("dependency_unavailable", "The approved operation exceeded its result limit."); results.Add(map(reader)); }
            return results;
        }
        catch (OperationCanceledException) { throw; }
        catch (SafeFailure) { throw; }
        catch { throw new SafeFailure("dependency_unavailable", "Verified business information is currently unavailable."); }
    }
    public static SqlCommand CreateCommand(SqlConnection connection, ApprovedProcedure procedure, string? identifier, IReadOnlySet<string> accounts, int timeout)
    {
        ValidateContract(procedure);
        var table = new DataTable(); table.Columns.Add(procedure.AccountColumn, typeof(string));
        foreach (var account in accounts) { if (account.Length > 64) throw new ArgumentException("Invalid server account scope."); table.Rows.Add(account); }
        var command = new SqlCommand(procedure.Name, connection) { CommandType = CommandType.StoredProcedure, CommandTimeout = timeout };
        command.Parameters.Add(new SqlParameter(procedure.ScopeParameter, SqlDbType.Structured) { TypeName = procedure.AccountTableType, Value = table });
        if (procedure.IdentifierParameter is not null)
        {
            if (identifier is null || identifier.Length > 64) { command.Dispose(); throw new ArgumentException("Invalid resource identifier."); }
            command.Parameters.Add(new SqlParameter(procedure.IdentifierParameter, SqlDbType.NVarChar, 64) { Value = identifier });
        }
        return command;
    }
    private static void ValidateContract(ApprovedProcedure p)
    {
        static bool Name(string s) => Regex.IsMatch(s, @"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)?$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        static bool Parameter(string s) => Regex.IsMatch(s, @"^@[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        if (!Name(p.Name) || !Name(p.AccountTableType) || !Name(p.AccountColumn) || !Parameter(p.ScopeParameter) || (p.IdentifierParameter is not null && (!Parameter(p.IdentifierParameter) || p.IdentifierParameter == p.ScopeParameter)))
            throw new ArgumentException("Unconfirmed or invalid stored procedure contract.");
    }
}
