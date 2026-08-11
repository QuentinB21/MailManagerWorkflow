using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MailManager.Api.Data;

public sealed class MailManagerDbContextFactory : IDesignTimeDbContextFactory<MailManagerDbContext>
{
    public MailManagerDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? throw new InvalidOperationException(
                "Set ConnectionStrings__Postgres before running Entity Framework commands.");
        var options = new DbContextOptionsBuilder<MailManagerDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new MailManagerDbContext(options);
    }
}
