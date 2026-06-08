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
        b.Entity<TelegramUser>(x =>
        {
            x.HasKey(y => y.Id);
            x.HasIndex(y => y.TelegramId).IsUnique();
            x.Property(y => y.TelegramId).IsRequired();
        });
        
        b.Entity<Plan>(x =>
        {
            x.HasKey(y => y.Id);
            x.HasIndex(y => y.Code).IsUnique();
        });
        
        b.Entity<InboundGroup>(x =>
        {
            x.HasKey(y => y.Id);
            x.HasIndex(y => y.Code).IsUnique();
            x.HasMany(y => y.Items)
                .WithOne(y => y.InboundGroup)
                .HasForeignKey(y => y.InboundGroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        b.Entity<AgentNode>(x =>
        {
            x.HasKey(y => y.AgentId);
        });
        
        b.Entity<Subscription>(x =>
        {
            x.HasKey(y => y.Id);
            x.HasIndex(y => y.ClientEmail).IsUnique();
        });
        
        b.Entity<ProvisioningJob>(x =>
        {
            x.HasKey(y => y.Id);
            x.HasIndex(y => new { y.TargetAgentId, y.Status, y.CreatedAtUtc });
            x.HasOne(y => y.Order)
                .WithMany()
                .HasForeignKey(y => y.OrderId)
                .OnDelete(DeleteBehavior.SetNull);
        });
        
        b.Entity<Order>(x =>
        {
            x.HasKey(y => y.Id);
            x.HasOne(y => y.TelegramUser)
                .WithMany()
                .HasForeignKey(y => y.TelegramUserId)
                .HasPrincipalKey(y => y.TelegramId);
                
            x.HasOne(y => y.Plan)
                .WithMany()
                .HasForeignKey(y => y.PlanId);
        });
        
        b.Entity<Settings>(x =>
        {
            x.HasKey(y => y.Id);
            x.Property(y => y.Key).HasMaxLength(200);
            x.HasIndex(y => y.Key).IsUnique();
        });
    }
}