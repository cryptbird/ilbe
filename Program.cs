using DigitalPaymentSystem_ILTC.BAL;
using DigitalPaymentSystem_ILTC.Helpers;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);


builder.WebHost.UseKestrel(option => option.AddServerHeader = false);

builder.Configuration.SetBasePath(builder.Environment.ContentRootPath)
                     .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                     .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: false, reloadOnChange: true)
                     .AddEnvironmentVariables();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

Stopwatch stopwatch_1 = Stopwatch.StartNew();

builder.Services.AddHangfire((provider, config) =>
{
    config.UsePostgreSqlStorage(
        bootstrapper =>
        {
            bootstrapper.UseNpgsqlConnection(builder.Configuration.GetConnectionString("ILGHIConnectionString"));
        },
        new PostgreSqlStorageOptions
        {
            SchemaName = "DPS_Hangfire",
            PrepareSchemaIfNecessary = true
        });
});

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = Environment.ProcessorCount * 5;
});

stopwatch_1.Stop();
var responseTime = stopwatch_1.Elapsed.TotalSeconds;

builder.Services.AddTransient<RefundBAL>();
builder.Services.AddTransient<RetryResyncBAL>();
builder.Services.AddTransient<CallbackBAL>();

builder.Services.AddHttpContextAccessor();

builder.Services.AddHttpClient();


var app = builder.Build();

JobStorage.Current = app.Services.GetRequiredService<JobStorage>();

// Make configuration available to static helpers (must run before DatabaseConfig is accessed)
AppConfiguration.Configuration = app.Configuration;

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/dps-agorax/hangfire", out var remainder))
    {
        context.Request.PathBase = app.Environment.IsDevelopment()
            ? PathString.Empty
            : "/digital/v2.0/digital-payment-be";
        context.Request.Path = "/hangfire" + remainder;
    }
    await next();
});

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/hangfire"))
    {
        context.Response.Headers.Remove("X-Frame-Options");
        context.Response.Headers.Append("Content-Security-Policy", "frame-ancestors 'self' https://www.icicilombard.com https://echannel.insurancepp.com https://echannel.insurancearticlez.com");
    }
    await next();
});

var hangfireOptions = new DashboardOptions
{
    Authorization = new[] { new AllowAllAuthorizationFilter() },
    AppPath = null,
    IgnoreAntiforgeryToken = true,
    DisplayStorageConnectionString = false
};

//app.UseHangfireDashboard("/hangfire", hangfireOptions);

app.Lifetime.ApplicationStarted.Register(async () =>
{
    try
    {
        var clientFactory = app.Services.GetRequiredService<IHttpClientFactory>();
        var client = clientFactory.CreateClient();

        var url = "https://defaultd36615d9b483417583da3e968b0fde.20.environment.api.powerplatform.com/powerautomate/automations/direct/workflows/5c76cd42aae54d27b9d8a8b422988f3d/triggers/manual/paths/invoke?api-version=1&sp=%2Ftriggers%2Fmanual%2Frun&sv=1.0&sig=zgShWPYm4GTvO11XMaw_eQGtqRaR6n0BytqXUI-HnfQ";

        var payload = new
        {
            type = "message",
            attachments = new[]
                    {
                        new
                        {
                            contentType = "application/vnd.microsoft.card.adaptive",
                            content = new Dictionary<string, object>
                            {
                                { "$schema", "http://adaptivecards.io/schemas/adaptive-card.json" },
                                { "type", "AdaptiveCard" },
                                { "version", "1.4" },
                                {
                                    "body", new object[]
                                    {
                                        new
                                        {
                                            type = "TextBlock",
                                            text = "Alert",
                                            weight = "Bolder",
                                            size = "Large",
                                            color = "Good"
                                        },
                                        new
                                        {
                                            type = "FactSet",
                                            facts = new object[]
                                            {
                                                new { title = "Response Time", value = $"{responseTime}" },
                                                new { title = "Current Date Time", value = $"{DateTime.Now}" },
                                                new { title = "Location", value = "Program.cs" }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
        };

        var json = JsonSerializer.Serialize(payload);

        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync(url, content);

        var result = await response.Content.ReadAsStringAsync();
    }
    catch (Exception)
    {
    }
});


RecurringJob.AddOrUpdate<RetryResyncBAL>(
    "stp-retry-resync-job",
    x => x.ScheduleSTPRetryResync(),
    "0 */1 * * *"
);
RecurringJob.AddOrUpdate<CallbackBAL>
(
    "callback-sync-job",
    x => x.ScheduleExtractOrderDataWebhookFallback(),
    "0 17 * * 6"
);
RecurringJob.AddOrUpdate<ILTCBAL>
(
    "ILTC-job",
    x => x.ScheduleAutoClaimRaised(),
    "0 */1 * * *"
);

DataLoader.KeyValueStore = DataLoader.LoadKeyValueData();
DataLoader.DBQueriesKeyValueStore = DataLoader.LoadDBQueriesKeyValueData();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();
app.MapControllers();

OnProjectStart.OnStart();

app.Run();
public class AllowAllAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
{
        return true;
    }
}