using Dariche.IranAgent.Options;
using Dariche.IranAgent.Services;
using Dariche.IranAgent.Xui;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection("Agent"));
builder.Services.Configure<XuiOptions>(builder.Configuration.GetSection("Xui"));
builder.Services.AddHttpClient<SignedCommerceClient>();
builder.Services.AddSingleton<XuiSqliteProvisioner>();
builder.Services.AddHostedService<AgentWorker>();

var host = builder.Build();
host.Run();
