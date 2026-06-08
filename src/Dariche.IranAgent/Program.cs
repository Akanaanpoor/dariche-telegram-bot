using Dariche.IranAgent.Options;
using Dariche.IranAgent.Services;
using Dariche.IranAgent.Xui;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: false);
builder.Configuration.AddEnvironmentVariables();

// Options
builder.Services.Configure<AgentOptions>(
    builder.Configuration.GetSection("Agent"));

// HttpClient
builder.Services.AddHttpClient<SignedCommerceClient>(client =>
{
    var options = builder.Configuration.GetSection("Agent").Get<AgentOptions>();
    client.BaseAddress = new Uri(options?.CommerceBaseUrl ?? "https://localhost:7001");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Services
builder.Services.AddSingleton<XuiSqliteProvisioner>();
builder.Services.AddHostedService<AgentWorker>();

var host = builder.Build();
await host.RunAsync();