using System.Data;
using BoltAI.Infrastructure;
using Microsoft.Data.SqlClient;
using Xunit;
namespace BoltAI.UnitTests;
public class SqlScaffoldTests
{
    [Fact] public void StoredProcedureCommandSeparatesValuesFromSql()
    {
        using var connection = new SqlConnection();
        using var command = SqlBusinessRepository.CreateCommand(connection, new("demo.ApprovedOrder", "@Scope", "demo.AccountList", "Account", "@Order"), "1; DROP TABLE Orders", new HashSet<string> { "C100" }, 15);
        Assert.Equal(CommandType.StoredProcedure, command.CommandType); Assert.Equal("demo.ApprovedOrder", command.CommandText);
        Assert.Equal("1; DROP TABLE Orders", command.Parameters["@Order"].Value); Assert.Equal(SqlDbType.NVarChar, command.Parameters["@Order"].SqlDbType);
        Assert.Equal(SqlDbType.Structured, command.Parameters["@Scope"].SqlDbType);
        Assert.Equal("C100", ((DataTable)command.Parameters["@Scope"].Value).Rows[0][0]);
    }
    [Fact] public void ArbitraryProcedureTextIsRejected()
    { using var c = new SqlConnection(); Assert.Throws<ArgumentException>(() => SqlBusinessRepository.CreateCommand(c, new("SELECT * FROM Orders", "@Scope", "demo.List", "Account", "@Order"), "45821", new HashSet<string> { "C100" }, 15)); }
}
