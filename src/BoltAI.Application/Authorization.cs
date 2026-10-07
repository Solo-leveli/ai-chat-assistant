using BoltAI.Domain;
namespace BoltAI.Application;
public sealed class CustomerScopeService : ICustomerScopeService
{
    public IReadOnlySet<string> GetScope(UserIdentity user) => user.AccountIds;
}
public sealed class CustomerAuthorizationService : IAuthorizationService
{
    public bool CanRead(UserIdentity user) => user.AccountIds.Count > 0 && user.Roles.Contains("BoltReader");
    public bool CanReadLocations(UserIdentity user) => CanRead(user) && user.Roles.Contains("InventoryLocation");
}
