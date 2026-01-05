using DtekMonitor.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Spacebar.Bedrock.Persistence.EntityFramework.Abstractions;

namespace DtekMonitor.Database;

/// <summary>
/// Application database context with Bedrock SDK integration.
/// </summary>
public class AppDbContext : DbContext
{
    private readonly IEfEntityConfigurator? _bedrockConfigurator;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        IEfEntityConfigurator bedrockConfigurator) : base(options)
    {
        _bedrockConfigurator = bedrockConfigurator;
    }

    /// <summary>
    /// Subscribers table - stores user's DTEK group subscription (business data).
    /// </summary>
    public DbSet<Subscriber> Subscribers { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        // STRICT MODE: Throw exception if there are pending model changes (migrations)
        optionsBuilder.ConfigureWarnings(warnings =>
            warnings.Throw(RelationalEventId.PendingModelChangesWarning));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all Bedrock SDK entity configurations (TelegramUser, MessageLog, CallbackLog, etc.)
        _bedrockConfigurator?.ApplyConfigurations(modelBuilder);

        // Configure Subscriber entity (PRESERVED - existing business data)
        modelBuilder.Entity<Subscriber>(entity =>
        {
            entity.HasKey(e => e.ChatId);
            entity.Property(e => e.ChatId).ValueGeneratedNever();
            entity.Property(e => e.GroupName).IsRequired().HasMaxLength(10);
            entity.Property(e => e.Username).HasMaxLength(100);
            entity.HasIndex(e => e.GroupName);
        });
    }
}
