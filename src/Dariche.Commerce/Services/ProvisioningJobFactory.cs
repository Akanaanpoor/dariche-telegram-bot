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
        var group = await _db.InboundGroups.Include(x => x.Items).FirstAsync(x => x.Code == plan.InboundGroupCode, ct);
        var tags = group.Items.OrderBy(x => x.SortOrder).Select(x => x.InboundTag).ToArray();
        var email = $"tg_{order.TelegramUserId}_{order.Id.ToString("N")[..8]}";
        var cmd = new CreateClientCommand(
            order.Id,
            order.TelegramUserId,
            plan.Code,
            plan.DurationDays,
            plan.TrafficGb,
            tags,
            email,
            $"tg:{order.TelegramUserId} plan:{plan.Code} order:{order.Id}");

        var job = new ProvisioningJob
        {
            Type = ProvisioningJobTypes.CreateClient,
            TargetAgentId = _cfg["Agent:DefaultAgentId"] ?? "iran-main",
            PayloadJson = JsonSerializer.Serialize(cmd),
            OrderId = order.Id,
            Status = ProvisioningJobStatus.Pending
        };
        _db.ProvisioningJobs.Add(job);
        order.Status = OrderStatus.Provisioning;
        await _db.SaveChangesAsync(ct);
        return job;
    }
}
