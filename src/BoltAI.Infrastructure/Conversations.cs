using BoltAI.Application;
using BoltAI.Domain;
namespace BoltAI.Infrastructure;
public sealed class InMemoryConversationStore : IConversationStore
{
    private sealed record Entry(string Owner, List<ChatMessage> Messages, DateTimeOffset Updated);
    private readonly Dictionary<string, Entry> entries = [];
    private readonly object gate = new();
    private static string Owner(UserIdentity user) => System.Text.Json.JsonSerializer.Serialize(new { user.UserId, Accounts = user.AccountIds.Order().ToArray(), Roles = user.Roles.Order().ToArray() });
    public ConversationLease Open(string? id, UserIdentity user)
    {
        lock (gate)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var key in entries.Where(e => now - e.Value.Updated > TimeSpan.FromMinutes(30)).Select(e => e.Key).ToArray()) entries.Remove(key);
            var owner = Owner(user);
            if (id is not null)
            {
                if (!Guid.TryParseExact(id, "N", out _) || !entries.TryGetValue(id, out var existing) || existing.Owner != owner)
                    throw new SafeFailure("conversation_unavailable", "The conversation is unavailable.");
                return new(id, owner, existing.Messages.ToArray());
            }
            if (entries.Count >= 1000) throw new SafeFailure("capacity", "The assistant is currently at capacity.");
            id = Guid.NewGuid().ToString("N"); entries[id] = new(owner, [], now);
            return new(id, owner, []);
        }
    }
    public void Append(ConversationLease lease, string question, string answer)
    {
        lock (gate)
        {
            if (!entries.TryGetValue(lease.Id, out var entry) || entry.Owner != lease.Owner) throw new SafeFailure("conversation_unavailable", "The conversation is unavailable.");
            entry.Messages.Add(new("user", question)); entry.Messages.Add(new("assistant", answer));
            if (entry.Messages.Count > 12) entry.Messages.RemoveRange(0, entry.Messages.Count - 12);
            entries[lease.Id] = entry with { Updated = DateTimeOffset.UtcNow };
        }
    }
}
