using MailManager.Api.Contracts;
using MailManager.Api.Domain;
using MailManager.Api.Services;

namespace MailManager.Api.Tests;

public sealed class MailboxProviderResolverTests
{
    [Fact]
    public void Resolve_returns_the_adapter_matching_the_provider()
    {
        var gmail = new StubAdapter(MailProvider.Gmail);
        var outlook = new StubAdapter(MailProvider.Outlook);
        var resolver = new MailboxProviderResolver([gmail, outlook]);

        Assert.Same(gmail, resolver.Resolve(MailProvider.Gmail));
        Assert.Same(outlook, resolver.Resolve(MailProvider.Outlook));
    }

    private sealed class StubAdapter(MailProvider provider) : IMailboxProviderAdapter
    {
        public MailProvider Provider { get; } = provider;
        public Task<MailboxConnectionTestResponse?> TestConnectionAsync(Guid mailboxConnectionId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<bool> SynchronizeDestinationAsync(Guid labelDefinitionId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<MailboxSyncResponse?> SyncAsync(Guid mailboxConnectionId, int maxResults, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
