using MailManager.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace MailManager.Api.Data;

public sealed class MailManagerDbContext(DbContextOptions<MailManagerDbContext> options)
    : DbContext(options)
{
    public static readonly Guid DemoMailboxId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid DemoLabelId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid PublicDemoMailboxId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid PublicDemoLabelId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    public static readonly Guid PublicDemoRuleId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    public const string LocalOwnerSubject = "10000000-0000-0000-0000-000000000001";
    public const string DemoOwnerSubject = "10000000-0000-0000-0000-000000000002";

    public DbSet<MailboxConnection> MailboxConnections => Set<MailboxConnection>();
    public DbSet<LabelDefinition> LabelDefinitions => Set<LabelDefinition>();
    public DbSet<ClassificationRule> ClassificationRules => Set<ClassificationRule>();
    public DbSet<ProcessingLog> ProcessingLogs => Set<ProcessingLog>();
    public DbSet<GmailOAuthConfiguration> GmailOAuthConfigurations => Set<GmailOAuthConfiguration>();
    public DbSet<LegalAcceptance> LegalAcceptances => Set<LegalAcceptance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MailboxConnection>(entity =>
        {
            entity.Property(x => x.OwnerSubject).HasMaxLength(200);
            entity.Property(x => x.DisplayName).HasMaxLength(200);
            entity.Property(x => x.Provider).HasConversion<string>().HasMaxLength(50);
            entity.Property(x => x.EmailAddress).HasMaxLength(320);
            entity.Property(x => x.EncryptedRefreshToken).HasColumnType("text");
            entity.Property(x => x.GrantedScopes).HasMaxLength(1000);
            entity.Property(x => x.LastSyncError).HasMaxLength(1000);
            entity.HasIndex(x => x.OwnerSubject);
        });

        modelBuilder.Entity<GmailOAuthConfiguration>(entity =>
        {
            entity.Property(x => x.ClientId).HasMaxLength(500);
            entity.Property(x => x.EncryptedClientSecret).HasColumnType("text");
        });

        modelBuilder.Entity<LabelDefinition>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(150);
            entity.Property(x => x.ExternalLabelId).HasMaxLength(200);
            entity.Property(x => x.Color).HasMaxLength(20);
            entity.HasIndex(x => new { x.MailboxConnectionId, x.Name }).IsUnique();
            entity.HasOne(x => x.MailboxConnection)
                .WithMany(x => x.Labels)
                .HasForeignKey(x => x.MailboxConnectionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ClassificationRule>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.MatchMode).HasConversion<string>().HasMaxLength(10);
            entity.Property(x => x.SenderAddresses).HasColumnType("text[]");
            entity.Property(x => x.SenderDomains).HasColumnType("text[]");
            entity.Property(x => x.SubjectKeywords).HasColumnType("text[]");
            entity.Property(x => x.BodyKeywords).HasColumnType("text[]");
            entity.HasIndex(x => new { x.MailboxConnectionId, x.Priority });
            entity.HasOne(x => x.MailboxConnection)
                .WithMany(x => x.Rules)
                .HasForeignKey(x => x.MailboxConnectionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.DestinationLabel)
                .WithMany(x => x.Rules)
                .HasForeignKey(x => x.DestinationLabelId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProcessingLog>(entity =>
        {
            entity.Property(x => x.ExternalMessageId).HasMaxLength(300);
            entity.Property(x => x.SubjectPreview).HasMaxLength(250);
            entity.Property(x => x.DestinationLabelName).HasMaxLength(150);
            entity.Property(x => x.MatchedRuleName).HasMaxLength(200);
            entity.Property(x => x.MatchedCriteria).HasColumnType("text[]");
            entity.Property(x => x.NoMatchReason).HasMaxLength(500);
            entity.Property(x => x.ProviderActionError).HasMaxLength(1000);
            entity.HasIndex(x => new { x.MailboxConnectionId, x.ExternalMessageId }).IsUnique();
            entity.HasOne(x => x.MailboxConnection)
                .WithMany(x => x.ProcessingLogs)
                .HasForeignKey(x => x.MailboxConnectionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        var seedDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        modelBuilder.Entity<MailboxConnection>().HasData(new MailboxConnection
        {
            Id = DemoMailboxId,
            OwnerSubject = LocalOwnerSubject,
            DisplayName = "Boîte Gmail de démonstration",
            Provider = MailProvider.Gmail,
            IsActive = true,
            CreatedAt = seedDate
        });
        modelBuilder.Entity<LabelDefinition>().HasData(new LabelDefinition
        {
            Id = DemoLabelId,
            MailboxConnectionId = DemoMailboxId,
            Name = "Projet Démo",
            Color = "#2563eb",
            IsActive = true,
            CreatedAt = seedDate
        });

        modelBuilder.Entity<LegalAcceptance>(entity =>
        {
            entity.Property(x => x.OwnerSubject).HasMaxLength(200);
            entity.Property(x => x.TermsVersion).HasMaxLength(30);
            entity.Property(x => x.PrivacyVersion).HasMaxLength(30);
            entity.HasIndex(x => x.OwnerSubject).IsUnique();
        });
        modelBuilder.Entity<ClassificationRule>().HasData(new ClassificationRule
        {
            Id = PublicDemoRuleId,
            MailboxConnectionId = PublicDemoMailboxId,
            DestinationLabelId = PublicDemoLabelId,
            Name = "Projet Alpha",
            Priority = 10,
            IsActive = true,
            MatchMode = MatchMode.Any,
            SenderAddresses = [],
            SenderDomains = ["client.fr"],
            SubjectKeywords = ["projet alpha"],
            BodyKeywords = [],
            CreatedAt = seedDate,
            UpdatedAt = seedDate
        });
        modelBuilder.Entity<MailboxConnection>().HasData(new MailboxConnection
        {
            Id = PublicDemoMailboxId,
            OwnerSubject = DemoOwnerSubject,
            DisplayName = "Boîte de démonstration",
            Provider = MailProvider.Gmail,
            IsActive = true,
            CreatedAt = seedDate
        });
        modelBuilder.Entity<LabelDefinition>().HasData(new LabelDefinition
        {
            Id = PublicDemoLabelId,
            MailboxConnectionId = PublicDemoMailboxId,
            Name = "Projet Démo",
            Color = "#c64a2f",
            IsActive = true,
            CreatedAt = seedDate
        });
    }
}
