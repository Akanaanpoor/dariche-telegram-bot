using System.Text.Json;
using Dariche.Commerce.Data;
using Dariche.Commerce.Domain;
using Dariche.Shared.Provisioning;
using Microsoft.EntityFrameworkCore;

namespace Dariche.Commerce.Services;

public sealed class ProvisioningJobFactory
{
    private readonly CommerceDbContext _db;
    private readonly IConfiguration _cfg;

    public ProvisioningJobFactory(CommerceDbContext db, IConfiguration cfg)
    {
        _db = db;
        _cfg = cfg;
    }

    public async Task<ProvisioningJob> CreateClientJobForOrderAsync(Order order, CancellationToken ct)
    {
        var plan = await _db.Plans.FirstAsync(x => x.Id == order.PlanId, ct);
        var group = await _db.InboundGroups
            .Include(x => x.Items)
            .FirstAsync(x => x.Code == plan.InboundGroupCode, ct);
            
        var tags = group.Items
            .OrderBy(x => x.SortOrder)
            .Select(x => x.InboundTag)
            .ToArray();
            
        var email = $"tg_{order.TelegramUserId}_{order.Id.ToString("N")[..8]}";
        var subId = Guid.NewGuid().ToString("N")[..16];
        
        // ClientLabel برای نمایش در x-ui
        var clientLabel = $"tg_{order.TelegramUserId}";
        
        // AssignedInboundTags در زمان ایجاد مشخص نیست (بعداً توسط Agent پر می‌شود)
        var assignedInboundTags = Array.Empty<string>();
        
        var cmd = new CreateClientCommand(
            OrderId: order.Id,
            TelegramUserId: order.TelegramUserId,
            PlanCode: plan.Code,
            DurationDays: plan.DurationDays,
            TrafficGb: plan.TrafficGb,
            InboundTags: tags,
            ClientEmail: email,
            SubId: subId,
            Remark: $"tg:{order.TelegramUserId} plan:{plan.Code} order:{order.Id}",
            ClientLabel: clientLabel,
            AssignedInboundTags: assignedInboundTags
        );

        var job = new ProvisioningJob
        {
            Id = Guid.NewGuid(),
            Type = ProvisioningJobType.CreateClient,
            TargetAgentId = _cfg["Agent:DefaultAgentId"] ?? "iran-main",
            PayloadJson = JsonSerializer.Serialize(cmd),
            OrderId = order.Id,
            Status = ProvisioningJobStatus.Pending,
            Attempt = 0,
            UserNotified = false,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        
        _db.ProvisioningJobs.Add(job);
        order.Status = OrderStatus.Provisioning;
        await _db.SaveChangesAsync(ct);
        
        return job;
    }
}