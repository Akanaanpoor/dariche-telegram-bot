using System.Text.Json;
using Dariche.Commerce.Data;
using Dariche.Commerce.Domain;
using Dariche.Shared.Provisioning;
using Microsoft.EntityFrameworkCore;

namespace Dariche.Commerce.Services;

public sealed class ProvisioningResultDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProvisioningResultDispatcher> _logger;

    public ProvisioningResultDispatcher(IServiceScopeFactory scopeFactory, ILogger<ProvisioningResultDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Provisioning result dispatcher started");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchOnce(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Provisioning result dispatcher failed");
            }
            
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task DispatchOnce(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var tg = scope.ServiceProvider.GetRequiredService<TelegramClient>();

        var jobs = await db.ProvisioningJobs
            .Where(x => !x.UserNotified && (x.Status == ProvisioningJobStatus.Succeeded || x.Status == ProvisioningJobStatus.Failed))
            .OrderBy(x => x.FinishedAtUtc)
            .Take(10)
            .ToListAsync(ct);

        foreach (var job in jobs)
        {
            var order = job.OrderId.HasValue 
                ? await db.Orders
                    .Include(x => x.Plan)
                    .FirstOrDefaultAsync(x => x.Id == job.OrderId.Value, ct) 
                : null;
                
            if (order is null)
            {
                job.UserNotified = true;
                continue;
            }

            if (job.Status == ProvisioningJobStatus.Succeeded && 
                job.Type == ProvisioningJobType.CreateClient && 
                !string.IsNullOrWhiteSpace(job.ResultJson))
            {
                var result = JsonSerializer.Deserialize<CreateClientResult>(job.ResultJson)!;
                
                var existingSub = await db.Subscriptions
                    .FirstOrDefaultAsync(x => x.ClientEmail == result.ClientEmail, ct);
                    
                if (existingSub is null)
                {
                    var sub = new Subscription
                    {
                        Id = Guid.NewGuid(),
                        TelegramUserId = order.TelegramUserId,
                        OrderId = order.Id,
                        AgentId = job.TargetAgentId,
                        ClientEmail = result.ClientEmail,
                        ClientUuid = result.ClientUuid,
                        SubId = result.SubId,
                        SubscriptionUrl = result.SubscriptionUrl,
                        ExpireAtUtc = result.ExpireAtUtc,
                        TrafficGb = result.TrafficGb,
                        Status = SubscriptionStatus.Active,
                        AssignedInboundTags = JsonSerializer.Serialize(result.AssignedInboundTags),
                        CreatedAtUtc = DateTimeOffset.UtcNow
                    };
                    db.Subscriptions.Add(sub);
                }

                order.Status = OrderStatus.Completed;
                order.CompletedAtUtc = DateTimeOffset.UtcNow;
                
                await tg.SendTextAsync(order.TelegramUserId, 
                    $@"✅ سرویس شما فعال شد.

📦 پلن: {order.Plan?.Name}
⏰ اعتبار تا: {result.ExpireAtUtc:yyyy-MM-dd HH:mm} UTC
📊 حجم: {result.TrafficGb}GB
🔗 لینک اشتراک:
`{result.SubscriptionUrl}`

نکته: لینک را در کلاینت خود وارد کنید.", ct);
            }
            else
            {
                order.Status = OrderStatus.Failed;
                
                await tg.SendTextAsync(order.TelegramUserId,
                    $@"❌ ساخت سرویس با خطا مواجه شد.

🆔 Order: {order.Id}
❌ خطا: {job.ErrorMessage ?? "Unknown"}

پشتیبانی موضوع را بررسی می‌کند. لطفاً صبور باشید.", ct);
            }

            job.UserNotified = true;
            await db.SaveChangesAsync(ct);
        }
    }
}