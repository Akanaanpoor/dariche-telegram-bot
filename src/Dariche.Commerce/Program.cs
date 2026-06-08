using Dariche.Commerce.AgentApi;
using Dariche.Commerce.Data;
using Dariche.Commerce.Options;
using Dariche.Commerce.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<BotOptions>(builder.Configuration.GetSection("Bot"));
builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection("Agent"));

var conn = builder.Configuration.GetConnectionString("Default")
           ?? "Host=localhost;Port=5432;Database=dariche_commerce;Username=dariche;Password=dariche";
builder.Services.AddDbContext<CommerceDbContext>(opt => opt.UseNpgsql(conn));

builder.Services.AddHttpClient<TelegramClient>();
builder.Services.AddScoped<ProvisioningJobFactory>();
builder.Services.AddHostedService<TelegramBotWorker>();
builder.Services.AddHostedService<ProvisioningResultDispatcher>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
    await SeedData.InitializeAsync(db, app.Configuration);
}

app.MapGet("/health", () => Results.Ok(new { ok = true, service = "dariche-commerce", utc = DateTimeOffset.UtcNow }));
app.MapAgentEndpoints();

app.Run();
