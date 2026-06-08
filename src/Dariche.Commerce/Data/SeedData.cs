using Dariche.Commerce.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dariche.Commerce.Data;

public static class SeedData
{
    public static async Task InitializeAsync(CommerceDbContext db, IConfiguration cfg, CancellationToken ct = default)
    {
        await db.Database.EnsureCreatedAsync(ct);

        var adminIds = cfg.GetSection("Bot:AdminTelegramIds").Get<long[]>();
        if (adminIds is { Length: > 0 })
        {
            foreach (var id in adminIds)
            {
                var user = await db.TelegramUsers.FindAsync([id], ct);
                if (user is null)
                {
                    db.TelegramUsers.Add(new TelegramUser
                    {
                        TelegramId = id,
                        Status = UserStatus.Approved,
                        Role = UserRole.SuperAdmin,
                        ApprovedAtUtc = DateTimeOffset.UtcNow
                    });
                }
                else
                {
                    user.Status = UserStatus.Approved;
                    user.Role = UserRole.SuperAdmin;
                    user.ApprovedAtUtc ??= DateTimeOffset.UtcNow;
                }
            }
        }

        var agentId = cfg["Agent:DefaultAgentId"] ?? "iran-main";
        var agentSecret = cfg["Agent:DefaultAgentSecret"] ?? "CHANGE_ME_AGENT_SECRET";
        if (!await db.Agents.AnyAsync(x => x.AgentId == agentId, ct))
        {
            db.Agents.Add(new AgentNode { AgentId = agentId, Secret = agentSecret, IsEnabled = true });
        }

        if (!await db.InboundGroups.AnyAsync(ct))
        {
            var group = new InboundGroup { Code = "default", Name = "Default Iran inbound pool" };
            group.Items.Add(new InboundGroupItem { InboundTag = "in-20010-tcp", SortOrder = 10 });
            group.Items.Add(new InboundGroupItem { InboundTag = "in-20011-tcp", SortOrder = 20 });
            db.InboundGroups.Add(group);
        }

        if (!await db.Plans.AnyAsync(ct))
        {
            db.Plans.Add(new Plan
            {
                Code = "monthly_50gb",
                Name = "30 days / 50GB",
                DurationDays = 30,
                TrafficGb = 50,
                PriceToman = 0,
                PriceStars = null,
                InboundGroupCode = "default",
                IsActive = true
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
