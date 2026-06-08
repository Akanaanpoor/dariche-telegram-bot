namespace Dariche.Commerce.Domain;

public sealed class Settings
{
    public Guid Id { get; set; }

    public string Key { get; set; } = default!;

    public string Value { get; set; } = default!;

    public string? Description { get; set; }

    public bool IsPublic { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public static class SettingKeys
{
    public const string WelcomeMessage = "Bot.WelcomeMessage";

    public const string SupportUsername = "Bot.SupportUsername";

    public const string PaymentGuide = "Bot.PaymentGuide";

    public const string ChannelUrl = "Bot.ChannelUrl";

    public const string TermsText = "Bot.TermsText";

    public const string FaqText = "Bot.FaqText";

    public const string DefaultTrafficUnit = "System.DefaultTrafficUnit";
}