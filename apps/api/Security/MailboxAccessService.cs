using MailManager.Api.Data;
using MailManager.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace MailManager.Api.Security;

public sealed class MailboxAccessService(
    MailManagerDbContext dbContext,
    CurrentUser currentUser)
{
    public IQueryable<MailboxConnection> OwnedMailboxes(bool tracking = false)
    {
        var query = tracking
            ? dbContext.MailboxConnections.AsQueryable()
            : dbContext.MailboxConnections.AsNoTracking();

        return currentUser.IsAutomation
            ? query
            : query.Where(mailbox => mailbox.OwnerSubject == currentUser.Subject);
    }

    public Task<bool> CanAccessAsync(Guid mailboxConnectionId, CancellationToken cancellationToken = default) =>
        OwnedMailboxes().AnyAsync(mailbox => mailbox.Id == mailboxConnectionId, cancellationToken);

    public Task<MailboxConnection?> FindAsync(
        Guid mailboxConnectionId,
        bool tracking,
        CancellationToken cancellationToken = default) =>
        OwnedMailboxes(tracking).FirstOrDefaultAsync(
            mailbox => mailbox.Id == mailboxConnectionId,
            cancellationToken);
}

