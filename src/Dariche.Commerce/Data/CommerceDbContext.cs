using Dariche.Commerce.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dariche.Commerce.Data;

public sealed class CommerceDbContext : DbContext
{
    public CommerceDbContext(DbContextOptions<CommerceDbContext> options) : base(options)
    {
    }

    public DbSet<TelegramUser> TelegramUsers => Set<TelegramUser>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<InboundGroup> InboundGroups => Set<InboundGroup>();
    public DbSet<InboundGroupItem> InboundGroupItems => Set<InboundGroupItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<ProvisioningJob> ProvisioningJobs => Set<ProvisioningJob>();
    public DbSet<AgentNode> Agents => Set<AgentNode>();

    public DbSet<Settings> Settings => Set<Settings>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<TelegramUser>().HasKey(x => x.TelegramId);
        b.Entity<Plan>().HasIndex(x => x.Code).IsUnique();
        b.Entity<InboundGroup>().HasIndex(x => x.Code).IsUnique();
        b.Entity<AgentNode>().HasKey(x => x.AgentId);
        b.Entity<Subscription>().HasIndex(x => x.ClientEmail).IsUnique();
        b.Entity<ProvisioningJob>().HasIndex(x => new { x.TargetAgentId, x.Status, x.CreatedAtUtc });
        b.Entity<Settings>(x =>
        {
            x.HasKey(y => y.Id);

            x.Property(y => y.Key)
                .HasMaxLength(200);

            x.HasIndex(y => y.Key)
                .IsUnique();
        });
    }
}