namespace Dariche.Commerce.Options;

public sealed class BotOptions
{
    public string Token { get; set; } = "";
    public long[] AdminTelegramIds { get; set; } = [111589150,531314412];
    public string ManualPaymentText { get; set; } = "Please pay manually and send /paid ORDER_ID receipt.";
}
