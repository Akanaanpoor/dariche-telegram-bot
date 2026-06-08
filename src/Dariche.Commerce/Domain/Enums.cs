namespace Dariche.Commerce.Domain;

public enum UserStatus
{
    Pending = 0,
    Approved = 1,
    Blocked = 2
}

public enum UserRole
{
    Customer = 0,
    Admin = 10,
    SuperAdmin = 20
}

public enum OrderStatus
{
    Pending = 0,
    PendingPayment = 1,
    AwaitingAdminApproval = 2,
    Paid = 3,
    Provisioning = 4,
    Completed = 5,
    Failed = 6,
    Cancelled = 7
}

public enum PaymentMethod
{
    ManualCard = 0,
    TelegramStars = 1
}

public enum ProvisioningJobStatus
{
    Pending = 0,
    Picked = 1,
    Succeeded = 2,
    Failed = 3,
    Cancelled = 4
}

public enum ProvisioningJobType
{
    CreateClient = 0,
    RenewClient = 1,
    DisableClient = 2
}

public enum SubscriptionStatus
{
    Pending = 0,
    Active = 1,
    Suspended = 2,
    Expired = 3,
    Disabled = 4
}

public enum PaymentStatus
{
    Pending = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3
}

public enum PaymentProvider
{
    CardToCard = 0,
    ZarinPal = 1,
    IdPay = 2
}

public enum DiscountType
{
    FixedAmount = 0,
    Percentage = 1
}

public enum TicketStatus
{
    Open = 0,
    WaitingForUser = 1,
    WaitingForAdmin = 2,
    Closed = 3
}