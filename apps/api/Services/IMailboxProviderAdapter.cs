using MailManager.Api.Contracts;
using MailManager.Api.Domain;

namespace MailManager.Api.Services;

public interface IMailboxProviderAdapter
{
    MailProvider Provider { get; }
    Task<MailboxConnectionTestResponse?> TestConnectionAsync(Guid mailboxConnectionId, CancellationToken cancellationToken);
    Task<MailboxSyncResponse?> SyncAsync(Guid mailboxConnectionId, int maxResults, CancellationToken cancellationToken);
}

public sealed class MailboxProviderResolver(IEnumerable<IMailboxProviderAdapter> adapters)
{
    private readonly IReadOnlyDictionary<MailProvider, IMailboxProviderAdapter> _adapters =
        adapters.ToDictionary(adapter => adapter.Provider);

    public IMailboxProviderAdapter Resolve(MailProvider provider) =>
        _adapters.TryGetValue(provider, out var adapter)
            ? adapter
            : throw new NotSupportedException($"Le fournisseur {provider} n’est pas pris en charge.");
}
