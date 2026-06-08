using Dariche.Commerce.AgentApi;
using Dariche.Commerce.Data;
using Dariche.Commerce.Options;
using Dariche.Commerce.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false);
builder.Configuration.AddEnvironmentVariables();

// Database
builder.Services.AddDbContext<CommerceDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// Options
builder.Services.Configure<BotOptions>(builder.Configuration.GetSection("Bot"));
builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection("Agent"));

// Services
builder.Services.AddHttpClient<TelegramClient>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<ProvisioningJobFactory>();

// Hosted Services - این دو خط مهم هستند
builder.Services.AddHostedService<TelegramBotWorker>();
builder.Services.AddHostedService<ProvisioningResultDispatcher>();

// API
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
    var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    await db.Database.EnsureCreatedAsync();
    await SeedData.InitializeAsync(db, cfg);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapAgentEndpoints();

app.Run();