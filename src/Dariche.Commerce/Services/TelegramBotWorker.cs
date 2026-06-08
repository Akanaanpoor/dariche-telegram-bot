using System.Text.Json;
using Dariche.Commerce.Data;
using Dariche.Commerce.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dariche.Commerce.Services;

public sealed class TelegramBotWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TelegramClient _tg;
    private readonly ILogger<TelegramBotWorker> _logger;
    private readonly Dictionary<long, Guid> _waitingForReceipt = new();
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
    
        // تست اولیه: اطلاعات بات رو بگیر
        try
        {
            using var doc = await _tg.GetUpdatesAsync(0, stoppingToken);
            _logger.LogInformation("Initial test successful");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initial test failed");
        }
    
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var doc = await _tg.GetUpdatesAsync(_offset, stoppingToken);
            
                if (!doc.RootElement.GetProperty("ok").GetBoolean())
                {
                    var description = doc.RootElement.GetProperty("description").GetString();
                    _logger.LogWarning("Telegram API error: {Description}", description);
                    await Task.Delay(5000, stoppingToken);
                    continue;
                }
            
                var results = doc.RootElement.GetProperty("result");
                _logger.LogInformation("Received {Count} updates", results.GetArrayLength());
            
                // بقیه کد...
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Telegram polling failed");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task HandleCallbackDataAsync(long chatId, string callbackId, string data, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        
        await _tg.AnswerCallbackQueryAsync(callbackId, ct);
        
        switch (data)
        {
            case "menu_main":
                await _tg.SendTextAsync(chatId, "🏠 *منوی اصلی*\nلطفاً یکی از گزینه‌ها را انتخاب کنید:", ct, KeyboardBuilder.MainMenu());
                break;
                
            case "menu_plans":
                await SendPlansWithButtonsAsync(chatId, db, ct);
                break;
                
            case "menu_services":
                await MyServicesWithButtonsAsync(chatId, db, ct);
                break;
                
            case "menu_help":
                await SendHelpAsync(chatId, ct);
                break;
                
            case "menu_profile":
                await SendProfileAsync(chatId, db, ct);
                break;
                
            case "admin_menu":
                await _tg.SendTextAsync(chatId, "👑 *منوی مدیریت*", ct, KeyboardBuilder.AdminMenu());
                break;
                
            case "admin_users":
                await AdminUserMenuAsync(chatId, db, ct);
                break;
                
            case "admin_orders":
                await AdminOrdersMenuAsync(chatId, db, ct);
                break;
                
            default:
                if (data.StartsWith("buy_"))
                {
                    var planCode = data.Replace("buy_", "");
                    await BuyWithConfirmAsync(chatId, planCode, db, ct);
                }
                else if (data.StartsWith("confirm_buy_"))
                {
                    var planCode = data.Replace("confirm_buy_", "");
                    await ConfirmBuyAsync(chatId, planCode, db, ct);
                }
                else if (data.StartsWith("receipt_"))
                {
                    var orderId = Guid.Parse(data.Replace("receipt_", ""));
                    await RequestReceiptAsync(chatId, orderId, ct);
                }
                else if (data.StartsWith("cancel_order_"))
                {
                    var orderId = Guid.Parse(data.Replace("cancel_order_", ""));
                    await CancelOrderAsync(chatId, orderId, db, ct);
                }
                else if (data.StartsWith("approve_order_"))
                {
                    var orderId = Guid.Parse(data.Replace("approve_order_", ""));
                    await ApproveOrderFromMenuAsync(chatId, orderId, db, ct);
                }
                else if (data.StartsWith("toggle_user_"))
                {
                    var userId = long.Parse(data.Replace("toggle_user_", ""));
                    await ToggleUserStatusAsync(chatId, userId, db, ct);
                }
                else if (data.StartsWith("get_link_"))
                {
                    var subId = Guid.Parse(data.Replace("get_link_", ""));
                    await GetSubscriptionLinkAsync(chatId, subId, db, ct);
                }
                else
                {
                    await _tg.SendTextAsync(chatId, "❌ گزینه نامعتبر است.", ct);
                }
                break;
        }
    }

    private async Task HandleMessageAsync(long chatId, string? username, string? firstName, string text, CancellationToken ct)
    {
        // لاگ برای تایید ورود به متد
        _logger.LogError(">>> 1. Entered HandleMessageAsync for chatId: {ChatId}, text: {Text}", chatId, text);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            _logger.LogError(">>> 2. Scope created");

            var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            _logger.LogError(">>> 3. DbContext resolved");

            // بررسی وضعیت منتظر ماندن برای رسید
            if (_waitingForReceipt.ContainsKey(chatId))
            {
                _logger.LogError(">>> 4. User is waiting for receipt. Handling receipt.");
                await HandleTextForReceiptAsync(chatId, text, ct);
                return;
            }

            _logger.LogError(">>> 5. Getting or creating user...");
            var user = await GetOrCreateUserAsync(db, chatId, username, firstName, ct);
            _logger.LogError(">>> 6. User obtained/created. Status: {Status}", user?.Status);

            // اینجا یک پاسخ تستی ساده می‌فرستیم تا مطمئن شویم ربات می‌تواند پیام بفرستد
            _logger.LogError(">>> 7. Sending a test message...");
            await _tg.SendTextAsync(chatId, "✅ ربات فعال است و پیام شما را دریافت کرد! در حال پردازش...", ct);
            _logger.LogError(">>> 8. Test message sent successfully.");

            var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var cmd = parts[0].ToLowerInvariant();

            _logger.LogError(">>> 9. Command detected: {Command}", cmd);

            switch (cmd)
            {
                case "/id":
                    await _tg.SendTextAsync(chatId, $"🆔 Telegram ID: `{chatId}`", ct);
                    break;

                case "/start":
                    await _tg.SendTextAsync(chatId, "🌟 به ربات خوش آمدید! از منوی زیر استفاده کنید.", ct, KeyboardBuilder.MainMenu());
                    break;

                case "/menu":
                    await _tg.SendTextAsync(chatId, "🏠 *منوی اصلی*", ct, KeyboardBuilder.MainMenu());
                    break;

                default:
                    await _tg.SendTextAsync(chatId, "❌ دستور نامعتبر است.\nبرای مشاهده منو از /menu استفاده کنید.", ct, KeyboardBuilder.MainMenu());
                    break;
            }

            _logger.LogError(">>> 10. Message processed successfully!");
        }
        catch (Exception ex)
        {
            // این خط بسیار مهم است: هر خطایی را لاگ می‌کند
            _logger.LogError(ex, "!!! CRITICAL ERROR in HandleMessageAsync for chatId: {ChatId}, text: {Text}", chatId, text);

            // سعی می‌کنیم به کاربر خطای عمومی بفرستیم
            try
            {
                await _tg.SendTextAsync(chatId, "❌ متأسفانه خطایی در پردازش درخواست شما رخ داد. لطفاً بعداً تلاش کنید.", ct);
            }
            catch
            {
            }
        }
    }

    private async Task<TelegramUser> GetOrCreateUserAsync(CommerceDbContext db, long chatId, string? username, string? firstName, CancellationToken ct)
    {
        _logger.LogError("GetOrCreateUserAsync for {ChatId}", chatId); // لاگ
    
        var user = await db.TelegramUsers.FirstOrDefaultAsync(x => x.TelegramId == chatId, ct);
        if (user is null)
        {
            _logger.LogError("Creating new user for {ChatId}", chatId);
            user = new TelegramUser 
            { 
                TelegramId = chatId, 
                Username = username, 
                FirstName = firstName,
                Status = UserStatus.Pending,
                Role = UserRole.Customer,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                LastSeenAtUtc = DateTimeOffset.UtcNow
            };
            db.TelegramUsers.Add(user);
            await db.SaveChangesAsync(ct);
        }
        else
        {
            _logger.LogError("User found for {ChatId}, Status: {Status}", chatId, user.Status);
        }
        return user;
    }

    private async Task SendStartAsync(TelegramUser user, CancellationToken ct)
    {
        if (user.Status != UserStatus.Approved)
        {
            await _tg.SendTextAsync(user.TelegramId, 
                $@"👋 سلام به ربات خوش آمدید!

📌 Telegram ID: `{user.TelegramId}`

⏳ وضعیت حساب شما: در انتظار تأیید ادمین

پس از تأیید، می‌توانید از سرویس‌های ما استفاده کنید.", ct, KeyboardBuilder.MainMenu());
            return;
        }

        await _tg.SendTextAsync(user.TelegramId, 
            @"🌟 به ربات فروش سرویس خوش آمدید!

از منوی زیر می‌توانید اقدام به خرید سرویس کنید.", ct, KeyboardBuilder.MainMenu());
    }

    private async Task SendPlansWithButtonsAsync(long chatId, CommerceDbContext db, CancellationToken ct)
    {
        _logger.LogError("SendPlansWithButtonsAsync called for {ChatId}", chatId); // لاگ
    
        var plans = await db.Plans.Where(x => x.IsActive).ToListAsync(ct);
    
        _logger.LogError("Found {Count} plans", plans.Count); // لاگ
    
        if (!plans.Any())
        {
            await _tg.SendTextAsync(chatId, "📭 فعلاً پلنی فعال نیست.", ct, KeyboardBuilder.BackButton());
            return;
        }
    
        var text = "📦 *لیست پلن‌های موجود*\n\n";
        foreach (var plan in plans)
        {
            text += $"🔹 *{plan.Name}*\n";
            text += $"   حجم: {plan.TrafficGb}GB\n";
            text += $"   مدت: {plan.DurationDays} روز\n";
            text += $"   قیمت: {plan.PriceToman:N0} تومان\n\n";
        }
    
        var planList = plans.Select(p => (p.Code, p.Name, p.PriceToman)).ToList();
        await _tg.SendTextAsync(chatId, text, ct, KeyboardBuilder.PlanButtons(planList));
    }

    private async Task MyServicesWithButtonsAsync(long chatId, CommerceDbContext db, CancellationToken ct)
    {
        var subs = await db.Subscriptions
            .Where(x => x.TelegramUserId == chatId && x.Status == SubscriptionStatus.Active)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);
        
        if (!subs.Any())
        {
            await _tg.SendTextAsync(chatId, "📭 سرویس فعالی برای شما ثبت نشده است.\nبرای خرید از بخش خرید سرویس اقدام کنید.", ct, KeyboardBuilder.BackButton());
            return;
        }
        
        var text = "📋 *سرویس‌های فعال شما*\n\n";
        var buttons = new List<List<InlineKeyboardButton>>();
        
        foreach (var sub in subs)
        {
            var daysLeft = (sub.ExpireAtUtc - DateTimeOffset.UtcNow).Days;
            text += $"✅ *{sub.ClientEmail}*\n";
            text += $"   حجم: {sub.TrafficGb}GB\n";
            text += $"   باقی‌مانده: {daysLeft} روز\n";
            text += $"   انقضا: {sub.ExpireAtUtc:yyyy-MM-dd}\n\n";
            
            buttons.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData($"📥 دریافت لینک", $"get_link_{sub.Id}")
            });
        }
        
        buttons.Add(new List<InlineKeyboardButton> { InlineKeyboardButton.WithCallbackData("🔙 بازگشت", "menu_main") });
        
        await _tg.SendTextAsync(chatId, text, ct, new InlineKeyboardMarkup(buttons));
    }

    private async Task SendHelpAsync(long chatId, CancellationToken ct)
    {
        var helpText = @"📚 *راهنمای استفاده از ربات*

🔹 *خرید سرویس*
از منوی اصلی گزینه خرید سرویس را انتخاب کنید.

🔹 *پرداخت*
بعد از ثبت سفارش، مبلغ را به کارت اعلام شده واریز کنید و با استفاده از دکمه ارسال رسید، عکس یا کد پیگیری را ارسال نمایید.

🔹 *دریافت لینک*
از بخش سرویس‌های من، لینک اشتراک خود را دریافت کنید.

🔹 *پشتیبانی*
در صورت نیاز به راهنمایی بیشتر با ادمین تماس بگیرید.

📞 *ارتباط با ادمین*
@YourSupportUsername";

        await _tg.SendTextAsync(chatId, helpText, ct, KeyboardBuilder.BackButton());
    }

    private async Task SendProfileAsync(long chatId, CommerceDbContext db, CancellationToken ct)
    {
        var user = await db.TelegramUsers.FirstOrDefaultAsync(x => x.TelegramId == chatId, ct);
        if (user is null) return;
        
        var statusIcon = user.Status == UserStatus.Approved ? "✅ تأیید شده" : "⏳ در انتظار تأیید";
        var roleName = user.Role == UserRole.SuperAdmin ? "مدیر کل" : 
                       user.Role == UserRole.Admin ? "مدیر" : "کاربر عادی";
        
        var profileText = $@"👤 *پروفایل شما*

🆔 آیدی تلگرام: `{user.TelegramId}`
👤 نام: {user.FirstName ?? "نامشخص"} {user.LastName ?? ""}
📝 وضعیت: {statusIcon}
👑 نقش: {roleName}
📅 تاریخ عضویت: {user.CreatedAtUtc:yyyy-MM-dd HH:mm}";

        await _tg.SendTextAsync(chatId, profileText, ct, KeyboardBuilder.BackButton());
    }

    private async Task BuyWithConfirmAsync(long chatId, string planCode, CommerceDbContext db, CancellationToken ct)
    {
        var user = await db.TelegramUsers.FirstOrDefaultAsync(x => x.TelegramId == chatId, ct);
        if (user?.Status != UserStatus.Approved)
        {
            await _tg.SendTextAsync(chatId, "⛔ دسترسی شما تأیید نشده است. منتظر تأیید ادمین باشید.", ct, KeyboardBuilder.MainMenu());
            return;
        }
        
        var plan = await db.Plans.FirstOrDefaultAsync(x => x.Code == planCode && x.IsActive, ct);
        if (plan is null)
        {
            await _tg.SendTextAsync(chatId, "❌ پلن مورد نظر یافت نشد.", ct, KeyboardBuilder.BackButton("menu_plans"));
            return;
        }
        
        var confirmText = $@"🛒 *تایید خرید*

📦 پلن: {plan.Name}
💰 قیمت: {plan.PriceToman:N0} تومان
📊 حجم: {plan.TrafficGb}GB
⏰ مدت: {plan.DurationDays} روز

آیا از خرید این پلن مطمئن هستید؟";

        var confirmButtons = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ بله، ثبت سفارش", $"confirm_buy_{planCode}"),
                InlineKeyboardButton.WithCallbackData("❌ انصراف", "menu_plans")
            }
        });
        
        await _tg.SendTextAsync(chatId, confirmText, ct, confirmButtons);
    }

    private async Task ConfirmBuyAsync(long chatId, string planCode, CommerceDbContext db, CancellationToken ct)
    {
        var user = await db.TelegramUsers.FirstOrDefaultAsync(x => x.TelegramId == chatId, ct);
        if (user?.Status != UserStatus.Approved)
        {
            await _tg.SendTextAsync(chatId, "⛔ دسترسی شما تأیید نشده است.", ct, KeyboardBuilder.MainMenu());
            return;
        }
        
        var plan = await db.Plans.FirstOrDefaultAsync(x => x.Code == planCode && x.IsActive, ct);
        if (plan is null)
        {
            await _tg.SendTextAsync(chatId, "❌ پلن پیدا نشد.", ct, KeyboardBuilder.BackButton("menu_plans"));
            return;
        }
        
        var order = new Order
        {
            Id = Guid.NewGuid(),
            TelegramUserId = chatId,
            PlanId = plan.Id,
            AmountToman = plan.PriceToman,
            Status = OrderStatus.PendingPayment,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
        
        var paymentGuide = await db.Settings
            .Where(x => x.Key == SettingKeys.PaymentGuide)
            .Select(x => x.Value)
            .FirstOrDefaultAsync(ct) ?? 
            "💰 لطفاً مبلغ را به کارت زیر واریز کنید:\n`6037-9975-1234-5678`";
        
        var text = $@"✅ سفارش ساخته شد.

🆔 Order ID: `{order.Id}`
📦 پلن: {plan.Name}
💰 مبلغ: {plan.PriceToman:N0} تومان

{paymentGuide}

پس از پرداخت، دکمه زیر را بزنید و رسید را ارسال کنید:";

        var buttons = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("💰 ارسال رسید", $"receipt_{order.Id}") },
            new[] { InlineKeyboardButton.WithCallbackData("❌ انصراف", $"cancel_order_{order.Id}") },
            new[] { InlineKeyboardButton.WithCallbackData("🔙 منوی اصلی", "menu_main") }
        });
        
        await _tg.SendTextAsync(chatId, text, ct, buttons);
    }

    private async Task RequestReceiptAsync(long chatId, Guid orderId, CancellationToken ct)
    {
        _waitingForReceipt[chatId] = orderId;
        await _tg.SendTextAsync(chatId, 
            "💰 *ارسال رسید پرداخت*\n\n" +
            "لطفاً رسید یا کد پیگیری خود را ارسال کنید:\n\n" +
            "مثال:\n" +
            "`کد پیگیری: 123456789\nمبلغ: 250,000 تومان`\n\n" +
            "🔔 همچنین می‌توانید عکس رسید را ارسال کنید.", ct);
    }

    private async Task CancelOrderAsync(long chatId, Guid orderId, CommerceDbContext db, CancellationToken ct)
    {
        var order = await db.Orders.FirstOrDefaultAsync(x => x.Id == orderId && x.TelegramUserId == chatId, ct);
        if (order != null && order.Status == OrderStatus.PendingPayment)
        {
            order.Status = OrderStatus.Cancelled;
            await db.SaveChangesAsync(ct);
            await _tg.SendTextAsync(chatId, "✅ سفارش با موفقیت لغو شد.", ct, KeyboardBuilder.MainMenu());
        }
        else
        {
            await _tg.SendTextAsync(chatId, "❌ امکان لغو این سفارش وجود ندارد.", ct);
        }
    }

    private async Task HandleTextForReceiptAsync(long chatId, string text, CancellationToken ct)
    {
        if (!_waitingForReceipt.TryGetValue(chatId, out var orderId)) return;
        
        _waitingForReceipt.Remove(chatId);
        
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        
        var order = await db.Orders.Include(x => x.Plan).FirstOrDefaultAsync(x => x.Id == orderId, ct);
        if (order != null && order.Status == OrderStatus.PendingPayment)
        {
            order.Status = OrderStatus.AwaitingAdminApproval;
            order.UserReceiptText = text;
            await db.SaveChangesAsync(ct);
            
            await _tg.SendTextAsync(chatId, 
                "✅ رسید شما با موفقیت ثبت شد و در انتظار تأیید ادمین است.\n" +
                "به زودی سرویس شما فعال خواهد شد.", ct, KeyboardBuilder.MainMenu());
            
            await NotifyAdminsForOrderAsync(order, ct);
        }
    }

    private async Task AdminUserMenuAsync(long chatId, CommerceDbContext db, CancellationToken ct)
    {
        var users = await db.TelegramUsers.OrderByDescending(x => x.CreatedAtUtc).Take(20).ToListAsync(ct);
        
        var text = "👥 *لیست کاربران*\n\n";
        var buttons = new List<List<InlineKeyboardButton>>();
        
        foreach (var user in users)
        {
            var status = user.Status == UserStatus.Approved ? "✅" : "⏳";
            text += $"{status} [{user.TelegramId}]";
            if (!string.IsNullOrEmpty(user.Username)) text += $" @{user.Username}";
            text += $"\n   وضعیت: {user.Status}\n\n";
            
            buttons.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData(
                    user.Status == UserStatus.Approved ? "🔒 مسدود کن" : "✅ تأیید کن", 
                    $"toggle_user_{user.TelegramId}")
            });
        }
        
        buttons.Add(new List<InlineKeyboardButton> { InlineKeyboardButton.WithCallbackData("🔙 بازگشت", "admin_menu") });
        
        await _tg.SendTextAsync(chatId, text, ct, new InlineKeyboardMarkup(buttons));
    }

    private async Task AdminOrdersMenuAsync(long chatId, CommerceDbContext db, CancellationToken ct)
    {
        var orders = await db.Orders
            .Include(x => x.Plan)
            .Where(x => x.Status == OrderStatus.AwaitingAdminApproval)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(20)
            .ToListAsync(ct);
        
        if (!orders.Any())
        {
            await _tg.SendTextAsync(chatId, "💰 سفارش در انتظار تأییدی وجود ندارد.", ct, KeyboardBuilder.AdminMenu());
            return;
        }
        
        var text = "💰 *سفارشات در انتظار تأیید*\n\n";
        var buttons = new List<List<InlineKeyboardButton>>();
        
        foreach (var order in orders)
        {
            text += $"🆔 `{order.Id}`\n";
            text += $"👤 کاربر: {order.TelegramUserId}\n";
            text += $"📦 پلن: {order.Plan?.Name}\n";
            text += $"💰 مبلغ: {order.AmountToman:N0} تومان\n";
            text += $"📝 رسید: {(order.UserReceiptText?.Length > 50 ? order.UserReceiptText[..50] + "..." : order.UserReceiptText)}\n\n";
            
            buttons.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData($"✅ تأیید", $"approve_order_{order.Id}"),
                InlineKeyboardButton.WithCallbackData($"❌ رد", $"reject_order_{order.Id}")
            });
        }
        
        buttons.Add(new List<InlineKeyboardButton> { InlineKeyboardButton.WithCallbackData("🔙 منوی ادمین", "admin_menu") });
        
        await _tg.SendTextAsync(chatId, text, ct, new InlineKeyboardMarkup(buttons));
    }

    private async Task ApproveOrderFromMenuAsync(long chatId, Guid orderId, CommerceDbContext db, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ProvisioningJobFactory>();
        
        var order = await db.Orders.Include(x => x.Plan).FirstOrDefaultAsync(x => x.Id == orderId, ct);
        
        if (order is null)
        {
            await _tg.SendTextAsync(chatId, "❌ سفارش پیدا نشد.", ct);
            return;
        }
        
        if (order.Status != OrderStatus.AwaitingAdminApproval)
        {
            await _tg.SendTextAsync(chatId, $"⚠️ وضعیت سفارش {order.Status} است. نمی‌توان تأیید کرد.", ct);
            return;
        }
        
        order.Status = OrderStatus.Paid;
        order.PaidAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        
        var job = await factory.CreateClientJobForOrderAsync(order, ct);
        
        await _tg.SendTextAsync(chatId, $"✅ سفارش {orderId} تأیید شد.\n🆔 Job: {job.Id}", ct, KeyboardBuilder.AdminMenu());
        await _tg.SendTextAsync(order.TelegramUserId, "✅ پرداخت شما تأیید شد. سرویس در حال ساخت است...", ct, KeyboardBuilder.MainMenu());
    }

    private async Task ToggleUserStatusAsync(long adminChatId, long targetUserId, CommerceDbContext db, CancellationToken ct)
    {
        var admin = await db.TelegramUsers.FirstOrDefaultAsync(x => x.TelegramId == adminChatId, ct);
        if (admin?.Role < UserRole.Admin)
        {
            await _tg.SendTextAsync(adminChatId, "⛔ شما دسترسی مدیریت ندارید.", ct);
            return;
        }
        
        var user = await db.TelegramUsers.FirstOrDefaultAsync(x => x.TelegramId == targetUserId, ct);
        if (user is null)
        {
            await _tg.SendTextAsync(adminChatId, "❌ کاربر پیدا نشد.", ct);
            return;
        }
        
        user.Status = user.Status == UserStatus.Approved ? UserStatus.Blocked : UserStatus.Approved;
        user.ApprovedAtUtc = user.Status == UserStatus.Approved ? DateTimeOffset.UtcNow : null;
        await db.SaveChangesAsync(ct);
        
        var statusText = user.Status == UserStatus.Approved ? "تأیید شد" : "مسدود شد";
        await _tg.SendTextAsync(adminChatId, $"✅ کاربر {targetUserId} {statusText}.", ct);
        
        await _tg.SendTextAsync(targetUserId, 
            user.Status == UserStatus.Approved 
                ? "✅ دسترسی شما تأیید شد. از /menu استفاده کنید." 
                : "⛔ دسترسی شما مسدود شده است.", ct);
    }

    private async Task NotifyAdminsForOrderAsync(Order order, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        
        var admins = await db.TelegramUsers
            .Where(x => x.Role >= UserRole.Admin && x.Status == UserStatus.Approved)
            .ToListAsync(ct);
        
        var text = $@"💰 *سفارش جدید در انتظار تأیید*

🆔 Order: `{order.Id}`
👤 User: {order.TelegramUserId}
📦 Plan: {order.Plan?.Name}
💰 Amount: {order.AmountToman:N0} تومان
📝 Receipt: {order.UserReceiptText}";

        var buttons = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("✅ تأیید سفارش", $"approve_order_{order.Id}") }
        });
        
        foreach (var admin in admins)
        {
            await _tg.SendTextAsync(admin.TelegramId, text, ct, buttons);
        }
    }

    private async Task GetSubscriptionLinkAsync(long chatId, Guid subId, CommerceDbContext db, CancellationToken ct)
    {
        var sub = await db.Subscriptions.FirstOrDefaultAsync(x => x.Id == subId && x.TelegramUserId == chatId, ct);
        if (sub == null)
        {
            await _tg.SendTextAsync(chatId, "❌ سرویس پیدا نشد.", ct);
            return;
        }
        
        await _tg.SendTextAsync(chatId, 
            $"🔗 *لینک اشتراک شما*\n\n" +
            $"`{sub.SubscriptionUrl}`\n\n" +
            $"این لینک را در کلاینت خود وارد کنید.\n" +
            $"اعتبار تا: {sub.ExpireAtUtc:yyyy-MM-dd}", ct, KeyboardBuilder.BackButton("menu_services"));
    }
}