namespace Dariche.Commerce.Services;

// کلاس ساده برای دکمه‌ها
public class InlineKeyboardButton
{
    public string Text { get; set; } = "";
    public string? CallbackData { get; set; }
    
    public static InlineKeyboardButton WithCallbackData(string text, string callbackData)
    {
        return new InlineKeyboardButton
        {
            Text = text,
            CallbackData = callbackData
        };
    }
}

// کلاس ساده برای کیبورد
public class InlineKeyboardMarkup
{
    public List<List<InlineKeyboardButton>> InlineKeyboard { get; set; } = new();
    
    public InlineKeyboardMarkup(List<List<InlineKeyboardButton>> keyboard)
    {
        InlineKeyboard = keyboard;
    }
    
    public InlineKeyboardMarkup(InlineKeyboardButton[][] keyboard)
    {
        InlineKeyboard = keyboard.Select(row => row.ToList()).ToList();
    }
}

public static class KeyboardBuilder
{
    public static InlineKeyboardMarkup MainMenu()
    {
        var keyboard = new List<List<InlineKeyboardButton>>
        {
            new()
            {
                InlineKeyboardButton.WithCallbackData("📦 خرید سرویس", "menu_plans"),
                InlineKeyboardButton.WithCallbackData("📋 سرویس‌های من", "menu_services"),
            },
            new()
            {
                InlineKeyboardButton.WithCallbackData("ℹ️ راهنما", "menu_help"),
                InlineKeyboardButton.WithCallbackData("👤 پروفایل", "menu_profile"),
            },
            new()
            {
                InlineKeyboardButton.WithCallbackData("👑 پنل مدیریت", "admin_menu"),
            }
        };
        
        return new InlineKeyboardMarkup(keyboard);
    }

    public static InlineKeyboardMarkup BackButton(string callbackData = "menu_main")
    {
        var keyboard = new List<List<InlineKeyboardButton>>
        {
            new() { InlineKeyboardButton.WithCallbackData("🔙 بازگشت", callbackData) }
        };
        
        return new InlineKeyboardMarkup(keyboard);
    }

    public static InlineKeyboardMarkup PlanButtons(List<(string Code, string Name, decimal Price)> plans)
    {
        var buttons = new List<List<InlineKeyboardButton>>();
        
        foreach (var plan in plans)
        {
            buttons.Add(new()
            {
                InlineKeyboardButton.WithCallbackData(
                    $"{plan.Name} - {plan.Price:N0} تومان", 
                    $"buy_{plan.Code}")
            });
        }
        
        buttons.Add(new() { InlineKeyboardButton.WithCallbackData("🔙 منوی اصلی", "menu_main") });
        
        return new InlineKeyboardMarkup(buttons);
    }

    public static InlineKeyboardMarkup AdminMenu()
    {
        var keyboard = new List<List<InlineKeyboardButton>>
        {
            new()
            {
                InlineKeyboardButton.WithCallbackData("👥 کاربران", "admin_users"),
                InlineKeyboardButton.WithCallbackData("💰 سفارشات", "admin_orders"),
            },
            new()
            {
                InlineKeyboardButton.WithCallbackData("📦 پلن‌ها", "admin_plans"),
                InlineKeyboardButton.WithCallbackData("📊 آمار", "admin_stats"),
            },
            new()
            {
                InlineKeyboardButton.WithCallbackData("🔙 منوی اصلی", "menu_main")
            }
        };
        
        return new InlineKeyboardMarkup(keyboard);
    }
}