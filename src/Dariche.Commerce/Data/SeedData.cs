using Dariche.Commerce.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dariche.Commerce.Data;

public static class SeedData
{
    public static async Task InitializeAsync(CommerceDbContext db, IConfiguration cfg, CancellationToken ct = default)
    {
        await db.Database.EnsureCreatedAsync(ct);

        // Seed Super Admins
        var adminIds = cfg.GetSection("Bot:AdminTelegramIds").Get<long[]>();
        if (adminIds is { Length: > 0 })
        {
            foreach (var id in adminIds)
            {
                var user = await db.TelegramUsers.FirstOrDefaultAsync(x => x.TelegramId == id, ct);
                if (user is null)
                {
                    db.TelegramUsers.Add(new TelegramUser
                    {
                        TelegramId = id,
                        Status = UserStatus.Approved,
                        Role = UserRole.SuperAdmin,
                        ApprovedAtUtc = DateTimeOffset.UtcNow,
                        CreatedAtUtc = DateTimeOffset.UtcNow,
                        LastSeenAtUtc = DateTimeOffset.UtcNow
                    });
                }
                else if (user.Role < UserRole.SuperAdmin)
                {
                    user.Status = UserStatus.Approved;
                    user.Role = UserRole.SuperAdmin;
                    user.ApprovedAtUtc ??= DateTimeOffset.UtcNow;
                }
            }
        }

        // Seed Agent
        var agentId = cfg["Agent:DefaultAgentId"] ?? "iran-main";
        var agentSecret = cfg["Agent:DefaultAgentSecret"] ?? "CHANGE_ME_AGENT_SECRET";
        if (!await db.Agents.AnyAsync(x => x.AgentId == agentId, ct))
        {
            db.Agents.Add(new AgentNode 
            { 
                AgentId = agentId, 
                Secret = agentSecret, 
                IsEnabled = true,
                LastSeenUtc = null
            });
        }

        // Seed Inbound Group
        if (!await db.InboundGroups.AnyAsync(ct))
        {
            var group = new InboundGroup 
            { 
                Code = "default", 
                Name = "Default Iran inbound pool",
                IsActive = true
            };
            group.Items.Add(new InboundGroupItem { InboundTag = "in-20010-tcp", SortOrder = 10 });
            group.Items.Add(new InboundGroupItem { InboundTag = "in-20011-tcp", SortOrder = 20 });
            group.Items.Add(new InboundGroupItem { InboundTag = "in-20012-tcp", SortOrder = 30 });
            db.InboundGroups.Add(group);
        }

        // Seed Sample Plans
        if (!await db.Plans.AnyAsync(ct))
        {
            db.Plans.AddRange(new[]
            {
                new Plan
                {
                    Code = "monthly_30gb",
                    Name = "30 روز / 30GB",
                    DurationDays = 30,
                    TrafficGb = 30,
                    PriceToman = 150000,
                    PriceStars = 300,
                    InboundGroupCode = "default",
                    IsActive = true,
                    Description = "پلن اقتصادی یک ماهه"
                },
                new Plan
                {
                    Code = "monthly_50gb",
                    Name = "30 روز / 50GB",
                    DurationDays = 30,
                    TrafficGb = 50,
                    PriceToman = 250000,
                    PriceStars = 500,
                    InboundGroupCode = "default",
                    IsActive = true,
                    Description = "پلن پرطرفدار یک ماهه"
                },
                new Plan
                {
                    Code = "quarterly_150gb",
                    Name = "90 روز / 150GB",
                    DurationDays = 90,
                    TrafficGb = 150,
                    PriceToman = 600000,
                    PriceStars = 1200,
                    InboundGroupCode = "default",
                    IsActive = true,
                    Description = "پلن سه ماهه با تخفیف ویژه"
                }
            });
        }

        // Seed Default Settings
        if (!await db.Settings.AnyAsync(ct))
        {
            db.Settings.AddRange(new[]
            {
                new Settings
                {
                    Key = SettingKeys.WelcomeMessage,
                    Value = "به ربات فروش سرویس خوش آمدید! برای مشاهده پلن‌ها از /plans استفاده کنید.",
                    IsPublic = true,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                },
                new Settings
                {
                    Key = SettingKeys.PaymentGuide,
                    Value = "راهنمای پرداخت:\n1. مبلغ را به کارت 1234-5678-9012-3456 واریز کنید\n2. رسید را با /paid ORDER_ID ارسال کنید",
                    IsPublic = true,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                }
            });
        }

        await db.SaveChangesAsync(ct);
    }
}