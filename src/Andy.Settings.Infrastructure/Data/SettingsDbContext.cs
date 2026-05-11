using Andy.Settings.Domain.Entities;
using Andy.Settings.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Andy.Settings.Infrastructure.Data;

public class SettingsDbContext : DbContext
{
    public SettingsDbContext(DbContextOptions<SettingsDbContext> options) : base(options) { }

    public DbSet<SettingDefinition> SettingDefinitions => Set<SettingDefinition>();
    public DbSet<SettingAssignment> SettingAssignments => Set<SettingAssignment>();
    public DbSet<EncryptedSecret> EncryptedSecrets => Set<EncryptedSecret>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<OutboxEntry> Outbox => Set<OutboxEntry>();
    public DbSet<SeenMessage> SeenMessages => Set<SeenMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // SettingDefinition
        modelBuilder.Entity<SettingDefinition>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Key, e.ApplicationCode }).IsUnique();
            entity.HasIndex(e => e.ApplicationCode);
            entity.HasIndex(e => e.Category);

            entity.Property(e => e.Key).IsRequired().HasMaxLength(256);
            entity.Property(e => e.ApplicationCode).IsRequired().HasMaxLength(64);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Description).HasMaxLength(1024);
            entity.Property(e => e.Category).HasMaxLength(64);
            entity.Property(e => e.DataType).HasConversion<string>().HasMaxLength(32);

            entity.HasMany(e => e.Assignments)
                .WithOne(a => a.Definition)
                .HasForeignKey(a => a.DefinitionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Secrets)
                .WithOne(s => s.Definition)
                .HasForeignKey(s => s.DefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // SettingAssignment
        modelBuilder.Entity<SettingAssignment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.DefinitionId, e.ScopeType, e.ScopeId });
            entity.HasIndex(e => e.ScopeType);

            entity.Property(e => e.ValueJson).IsRequired();
            entity.Property(e => e.Etag).IsRequired().HasMaxLength(64);
            entity.Property(e => e.ScopeType).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.ScopeId).HasMaxLength(256);
            entity.Property(e => e.UpdatedBy).HasMaxLength(256);
        });

        // EncryptedSecret
        modelBuilder.Entity<EncryptedSecret>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.DefinitionId, e.ScopeType, e.ScopeId }).IsUnique();

            entity.Property(e => e.EncryptedValue).IsRequired();
            entity.Property(e => e.ScopeType).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.ScopeId).HasMaxLength(256);
            entity.Property(e => e.UpdatedBy).HasMaxLength(256);
        });

        // AuditEvent
        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.DefinitionKey);
            entity.HasIndex(e => e.ActorId);

            entity.Property(e => e.EventType).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.DefinitionKey).IsRequired().HasMaxLength(256);
            entity.Property(e => e.ScopeType).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.ScopeId).HasMaxLength(256);
            entity.Property(e => e.ActorType).HasMaxLength(64);
            entity.Property(e => e.ActorId).HasMaxLength(256);
            entity.Property(e => e.CorrelationId).HasMaxLength(128);
        });

        // OutboxEntry — transactional outbox per ADR 0001 §3.
        modelBuilder.Entity<OutboxEntry>(entity =>
        {
            entity.ToTable("Outbox");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Subject).IsRequired().HasMaxLength(256);
            entity.Property(e => e.PayloadType).HasMaxLength(256);
            entity.Property(e => e.PayloadJson).IsRequired();
            entity.Property(e => e.LastError).HasMaxLength(2000);

            // Composite index over the dispatcher's hot query:
            //   WHERE PublishedAt IS NULL ORDER BY CreatedAt
            // Plain (non-filtered) so the same DDL works on Postgres and SQLite.
            entity.HasIndex(e => new { e.PublishedAt, e.CreatedAt });
            entity.HasIndex(e => e.CorrelationId);
        });

        // SeenMessage — consumer-side dedup per ADR 0001 AK3.
        modelBuilder.Entity<SeenMessage>(entity =>
        {
            entity.ToTable("SeenMessages");
            entity.HasKey(e => e.MsgId);
            entity.Property(e => e.Subject).IsRequired().HasMaxLength(256);
            entity.HasIndex(e => e.ExpiresAt);
        });
    }
}
