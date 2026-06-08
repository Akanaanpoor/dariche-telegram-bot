# Dariche Commerce

Dariche Commerce is a .NET 8 MVP for Telegram-based service sales and provisioning.

The intended flow is:

```text
Telegram user buys a plan
→ Admin confirms manual payment, or later Telegram Stars confirms automatically
→ Commerce Server creates a provisioning job
→ Iran Agent pulls the job from the Commerce Server
→ Iran Agent creates one x-ui client on the Iran server and assigns it to selected inbound tags
→ Iran Agent returns the subscription URL
→ Bot sends the subscription link to the user
```

## Projects

```text
src/Dariche.Commerce      ASP.NET Core app: Telegram bot, database, admin commands, agent API
src/Dariche.IranAgent     Worker service running on the Iran server; pull-based provisioning agent
src/Dariche.Shared        Shared DTOs and HMAC signing contracts
```

## Why pull-based Agent?

The Commerce Server never uses SSH to connect to Iran. The Iran Agent initiates outbound HTTPS requests to the Commerce Server, pulls jobs, executes local x-ui operations, and posts the result back with HMAC authentication.

## MVP features

- Telegram user approval
- Admin-only commands
- Manual payment flow
- Plans
- Orders
- Provisioning jobs
- Iran Agent job pull
- x-ui SQLite client creation
- Assignment to multiple inbound tags
- Subscription URL delivery

## Initial setup for local development

Requirements:

- .NET 8 SDK
- PostgreSQL, or Docker

Run PostgreSQL and Commerce with Docker:

```bash
cd deploy
cp ../.env.example .env
# edit .env first
# Important: ADMIN_TELEGRAM_IDS_FIRST currently supports one seeded admin in docker-compose.
docker compose up -d --build
```

Or run manually:

```bash
dotnet restore
dotnet run --project src/Dariche.Commerce
```

## Configuration

Commerce appsettings keys:

```json
{
  "ConnectionStrings": { "Default": "Host=localhost;Port=5432;Database=dariche_commerce;Username=dariche;Password=dariche" },
  "Bot": {
    "Token": "CHANGE_ME_TELEGRAM_BOT_TOKEN",
    "AdminTelegramIds": [123456789],
    "ManualPaymentText": "After payment send /paid ORDER_ID receipt."
  },
  "Agent": {
    "DefaultAgentId": "iran-main",
    "DefaultAgentSecret": "CHANGE_ME_AGENT_SECRET"
  }
}
```

Iran Agent appsettings keys:

```json
{
  "Agent": {
    "AgentId": "iran-main",
    "AgentSecret": "same secret as Commerce",
    "CommerceBaseUrl": "https://commerce.example.com"
  },
  "Xui": {
    "DbPath": "/etc/x-ui/x-ui.db",
    "SubscriptionBaseUrl": "https://landing.example.com/cli"
  }
}
```

## Bot commands

Customer:

```text
/id
/start
/plans
/buy PLAN_CODE
/paid ORDER_ID receipt-text
/my_services
```

Admin:

```text
/approve_user TELEGRAM_ID
/block_user TELEGRAM_ID
/orders
/approve_order ORDER_ID
```

## Important production warning

This is an MVP scaffold. Test on a copy of `/etc/x-ui/x-ui.db` first. The Iran Agent creates a backup before each write, but you should still run it on a staging server before production.
