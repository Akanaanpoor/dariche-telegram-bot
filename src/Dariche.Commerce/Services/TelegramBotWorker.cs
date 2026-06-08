using System.Text.Json;
using Dariche.Commerce.Data;
using Dariche.Commerce.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dariche.Commerce.Services;

public sealed class TelegramBotWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TelegramClient _tg;
    private readonly ILogger<TelegramBotWorker> _logger;
    private long _offset;

    public TelegramBotWorker(IServiceScopeFactory scopeFactory, TelegramClient tg, ILogger<TelegramBotWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _tg = tg;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Telegram bot worker started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var doc = await _tg.GetUpdatesAsync(_offset, stoppingToken);
                if (!doc.RootElement.GetProperty("ok").GetBoolean())
                {
                    await Task.Delay(3000, stoppingToken);
                    continue;
                }

                foreach (var update in doc.RootElement.GetProperty("result").EnumerateArray())
                {
                    _offset = Math.Max(_offset, update.GetProperty("update_id").GetInt64() + 1);
                    if (!update.TryGetProperty("message", out var msg)) continue;
                    if (!msg.TryGetProperty("chat", out var chat)) continue;
                    var chatId = chat.GetProperty("id").GetInt64();
                    var text = msg.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
                    var username = msg.TryGetProperty("from", out var from) && from.TryGetProperty("username", out var un) ? un.GetString() : null;
                    var firstName = msg.TryGetProperty("from", out var from2) && from2.TryGetProperty("first_name", out var fn) ? fn.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(text)) await HandleAsync(chatId, username, firstName, text.Trim(), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Telegram polling failed");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task HandleAsync(long chatId, string? username, string? firstName, string text, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var factory = scope.ServiceProvider.GetRequiredService<ProvisioningJobFactory>();

        var user = await db.TelegramUsers.FindAsync([chatId], ct);
        if (user is null)
        {
            user = new TelegramUser { TelegramId = chatId, Username = username, FirstName = firstName, Status = UserStatus.Pending, Role = UserRole.Customer };
            db.TelegramUsers.Add(user);
            await db.SaveChangesAsync(ct);
        }
        else
        {
            user.Username = username ?? user.Username;
            user.FirstName = firstName ?? user.FirstName;
            await db.SaveChangesAsync(ct);
        }

        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts[0].ToLowerInvariant();

        if (cmd == "/id")
        {
            await _tg.SendTextAsync(chatId, $"Telegram ID: {chatId}", ct);
            return;
        }

        if (cmd is "/start" or "/help")
        {
            await SendStartAsync(user, ct);
            return;
        }

        if (user.Status != UserStatus.Approved)
        {
            await _tg.SendTextAsync(chatId, @"⛔ دسترسی شما هنوز تأیید نشده است.
برای بررسی، Telegram ID خود را برای ادمین ارسال کنید:
" + chatId, ct);
            return;
        }

        if (cmd == "/plans")
        {
            await SendPlansAsync(chatId, db, ct);
            return;
        }

        if (cmd == "/buy")
        {
            await BuyAsync(user, parts, db, ct);
            return;
        }

        if (cmd == "/paid")
        {
            await PaidAsync(user, parts, text, db, ct);
            return;
        }

        if (cmd == "/my_services")
        {
            await MyServicesAsync(user, db, ct);
            return;
        }

        if (user.Role >= UserRole.Admin)
        {
            if (cmd == "/approve_user")
            {
                await ApproveUserAsync(user, parts, db, ct);
                return;
            }

            if (cmd == "/block_user")
            {
                await BlockUserAsync(user, parts, db, ct);
                return;
            }

            if (cmd == "/orders")
            {
                await OrdersAsync(user, db, ct);
                return;
            }

            if (cmd == "/approve_order")
            {
                await ApproveOrderAsync(user, parts, db, factory, ct);
                return;
            }
        }

        await _tg.SendTextAsync(chatId, "دستور نامعتبر است. /help", ct);
    }

    private async Task SendStartAsync(TelegramUser user, CancellationToken ct)
    {
        if (user.Status != UserStatus.Approved)
        {
            await _tg.SendTextAsync(user.TelegramId, $@"سلام. درخواست شما ثبت شد.
Telegram ID: {user.TelegramId}
بعد از تأیید ادمین، امکانات خرید فعال می‌شود.", ct);
            return;
        }

        var txt = @"سلام، خوش آمدید.

دستورات:
/plans - مشاهده پلن‌ها
/buy PLAN_CODE - خرید پلن
/paid ORDER_ID توضیح رسید - اعلام پرداخت
/my_services - سرویس‌های من
/id - نمایش آیدی

ادمین:
/approve_user TELEGRAM_ID
/orders
/approve_order ORDER_ID";
        await _tg.SendTextAsync(user.TelegramId, txt, ct);
    }

    private async Task SendPlansAsync(long chatId, CommerceDbContext db, CancellationToken ct)
    {
        var plans = await db.Plans.Where(x => x.IsActive).OrderBy(x => x.PriceToman).ToListAsync(ct);
        if (plans.Count == 0)
        {
            await _tg.SendTextAsync(chatId, "فعلاً پلنی فعال نیست.", ct);
            return;
        }

        var lines = plans.Select(p => $"- {p.Code}: {p.Name} | {p.TrafficGb}GB | {p.DurationDays} روز | {p.PriceToman:N0} تومان");

        await _tg.SendTextAsync(chatId, $@"پلن‌های فعال:
{string.Join("\n", lines)}

برای خرید: /buy PLAN_CODE", ct);
    }

    private async Task BuyAsync(TelegramUser user, string[] parts, CommerceDbContext db, CancellationToken ct)
    {
        if (parts.Length < 2)
        {
            await _tg.SendTextAsync(user.TelegramId, "فرمت: /buy PLAN_CODE", ct);
            return;
        }

        var code = parts[1];
        var plan = await db.Plans.FirstOrDefaultAsync(x => x.Code == code && x.IsActive, ct);
        if (plan is null)
        {
            await _tg.SendTextAsync(user.TelegramId, "پلن پیدا نشد یا فعال نیست.", ct);
            return;
        }

        var order = new Order { TelegramUserId = user.TelegramId, PlanId = plan.Id, AmountToman = plan.PriceToman, Status = OrderStatus.PendingPayment };
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
        await _tg.SendTextAsync(user.TelegramId,
            $@"سفارش ساخته شد.
Order ID: {order.Id}
پلن: {plan.Name}
مبلغ: {plan.PriceToman:N0} تومان

پرداخت دستی:
بعد از پرداخت، رسید را با این فرمت ارسال کنید:
/paid {order.Id} توضیح یا کد پیگیری", ct);
    }

    private async Task PaidAsync(TelegramUser user, string[] parts, string fullText, CommerceDbContext db, CancellationToken ct)
    {
        if (parts.Length < 2 || !Guid.TryParse(parts[1], out var orderId))
        {
            await _tg.SendTextAsync(user.TelegramId, "فرمت: /paid ORDER_ID توضیح رسید", ct);
            return;
        }

        var order = await db.Orders.Include(x => x.Plan).FirstOrDefaultAsync(x => x.Id == orderId && x.TelegramUserId == user.TelegramId, ct);
        if (order is null)
        {
            await _tg.SendTextAsync(user.TelegramId, "سفارش پیدا نشد.", ct);
            return;
        }

        if (order.Status != OrderStatus.PendingPayment)
        {
            await _tg.SendTextAsync(user.TelegramId, $"وضعیت سفارش قابل اعلام پرداخت نیست: {order.Status}", ct);
            return;
        }

        order.Status = OrderStatus.AwaitingAdminApproval;
        order.UserReceiptText = fullText;
        await db.SaveChangesAsync(ct);
        await _tg.SendTextAsync(user.TelegramId, "✅ رسید شما ثبت شد و منتظر تأیید ادمین است.", ct);
        var admins = await db.TelegramUsers.Where(x => x.Role >= UserRole.Admin && x.Status == UserStatus.Approved).ToListAsync(ct);
        foreach (var admin in admins)
        {
            await _tg.SendTextAsync(admin.TelegramId, $@"پرداخت جدید در انتظار تأیید:
Order: {order.Id}
User: {user.TelegramId}
Plan: {order.Plan?.Name}
Amount: {order.AmountToman:N0}

Approve: /approve_order {order.Id}", ct);
        }
    }

    private async Task MyServicesAsync(TelegramUser user, CommerceDbContext db, CancellationToken ct)
    {
        var subs = await db.Subscriptions.Where(x => x.TelegramUserId == user.TelegramId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
        if (subs.Count == 0)
        {
            await _tg.SendTextAsync(user.TelegramId, "سرویسی برای شما ثبت نشده است.", ct);
            return;
        }

        var lines = subs.Select(s => $"- {s.ClientEmail} | {s.Status} | Expire: {s.ExpireAtUtc:yyyy-MM-dd} | {s.SubscriptionUrl}");
        await _tg.SendTextAsync(user.TelegramId, string.Join("\n", lines), ct);
    }

    private async Task ApproveUserAsync(TelegramUser admin, string[] parts, CommerceDbContext db, CancellationToken ct)
    {
        if (parts.Length < 2 || !long.TryParse(parts[1], out var id))
        {
            await _tg.SendTextAsync(admin.TelegramId, "فرمت: /approve_user TELEGRAM_ID", ct);
            return;
        }

        var user = await db.TelegramUsers.FindAsync([id], ct) ?? new TelegramUser { TelegramId = id };
        if (db.Entry(user).State == EntityState.Detached) db.TelegramUsers.Add(user);
        user.Status = UserStatus.Approved;
        user.ApprovedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await _tg.SendTextAsync(admin.TelegramId, "User approved.", ct);
        await _tg.SendTextAsync(id, "✅ دسترسی شما تأیید شد. /start", ct);
    }

    private async Task BlockUserAsync(TelegramUser admin, string[] parts, CommerceDbContext db, CancellationToken ct)
    {
        if (parts.Length < 2 || !long.TryParse(parts[1], out var id))
        {
            await _tg.SendTextAsync(admin.TelegramId, "فرمت: /block_user TELEGRAM_ID", ct);
            return;
        }

        var user = await db.TelegramUsers.FindAsync([id], ct);
        if (user is null)
        {
            await _tg.SendTextAsync(admin.TelegramId, "User not found.", ct);
            return;
        }

        user.Status = UserStatus.Blocked;
        await db.SaveChangesAsync(ct);
        await _tg.SendTextAsync(admin.TelegramId, "User blocked.", ct);
    }

    private async Task OrdersAsync(TelegramUser admin, CommerceDbContext db, CancellationToken ct)
    {
        var orders = await db.Orders.Include(x => x.Plan).OrderByDescending(x => x.CreatedAtUtc).Take(20).ToListAsync(ct);
        var txt = orders.Count == 0 ? "No orders." : string.Join("\n", orders.Select(o => $"{o.Id} | {o.TelegramUserId} | {o.Status} | {o.Plan?.Code} | {o.AmountToman:N0}"));
        await _tg.SendTextAsync(admin.TelegramId, txt, ct);
    }

    private async Task ApproveOrderAsync(TelegramUser admin, string[] parts, CommerceDbContext db, ProvisioningJobFactory factory, CancellationToken ct)
    {
        if (parts.Length < 2 || !Guid.TryParse(parts[1], out var id))
        {
            await _tg.SendTextAsync(admin.TelegramId, "فرمت: /approve_order ORDER_ID", ct);
            return;
        }

        var order = await db.Orders.Include(x => x.Plan).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (order is null)
        {
            await _tg.SendTextAsync(admin.TelegramId, "Order not found.", ct);
            return;
        }

        if (order.Status is not OrderStatus.AwaitingAdminApproval and not OrderStatus.PendingPayment)
        {
            await _tg.SendTextAsync(admin.TelegramId, $"Order status is {order.Status}; cannot approve.", ct);
            return;
        }

        order.Status = OrderStatus.Paid;
        order.PaidAtUtc = DateTimeOffset.UtcNow;
        var job = await factory.CreateClientJobForOrderAsync(order, ct);
        await _tg.SendTextAsync(admin.TelegramId, $"Order approved. Provisioning job created: {job.Id}", ct);
        await _tg.SendTextAsync(order.TelegramUserId, "✅ پرداخت شما تأیید شد. سرویس در حال ساخت است.", ct);
    }
}