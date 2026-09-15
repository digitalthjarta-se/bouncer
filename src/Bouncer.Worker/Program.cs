using Bouncer.Core.Configuration;
using Bouncer.Core.Data;
using Bouncer.Core.Webhooks;
using Bouncer.Worker;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services
    .AddOptions<BouncerOptions>()
    .Bind(builder.Configuration.GetSection(BouncerOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<BouncerOptions>, BouncerOptionsValidator>();

builder.Services.AddSingleton<BouncerDatabase>();
builder.Services.AddSingleton<ProcessedMessageRepository>();
builder.Services.AddSingleton<BounceRecordRepository>();
builder.Services.AddSingleton<SenderBackoffRepository>();
builder.Services.AddSingleton<AuditLogRepository>();

builder.Services.AddHttpClient<WebhookSender>();

builder.Services.AddHostedService<BouncerWorker>();

var host = builder.Build();

host.Services.GetRequiredService<BouncerDatabase>().EnsureSchema();

host.Run();
