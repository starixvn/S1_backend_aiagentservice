using AIAgenHangfireService.Extensions;
using AIAgentHangfireService.Extensions;
using AIAgentHangfireService.Extensions.ActionFilters;
using AIAgentService.Core.Jobs;
using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Exceptions;
using Serilog.Formatting.Json;
 

var builder = WebApplication.CreateBuilder(args);

var remoteUrls = builder.Configuration.GetSection("ConfigService").Get<string[]>();
remoteUrls = builder.Configuration.CheckLocalConfig(builder.Environment, args, remoteUrls);
builder.Configuration.AddRemoteJsonConfig(remoteUrls);

var excludesMessage = builder.Configuration.GetSection("ExcludesMessage").Get<string[]>();
var excludesApiEndpoints = builder.Configuration.GetSection("ExcludesApiEndpoint").Get<string[]>();
builder.Host.UseSerilog((context, Configuration) =>
{
    Configuration
        .ReadFrom.Configuration(builder.Configuration)
        .Enrich.FromLogContext()
        .Filter.ByExcluding(c =>
            c.Properties.Any(p =>
                excludesApiEndpoints.Any(item => p.Value.ToString().Contains(item))
                || excludesMessage.Any(item => c.MessageTemplate.ToString().Contains(item))))
        .Enrich.WithExceptionDetails();

    OverrideLoggerWriteToLFile(Configuration);
});

void OverrideLoggerWriteToLFile(LoggerConfiguration configuration)
{
    const string cashSiteBrand = "JackFish";

    var brand = Environment.GetEnvironmentVariable("Brand") ?? string.Empty;
    if (brand != cashSiteBrand) return;

    var logFilePath = $"/aiagentworker/hangfire-{DateTime.Now:yyyyMMddHHmmssfff}.log";
    configuration
        .WriteTo.File(new JsonFormatter(),
            logFilePath,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 60,
            fileSizeLimitBytes: 10485760,
            rollOnFileSizeLimit: true);
}


// Add services to the container.

builder.Services
    .AddDatabaseConnection(builder.Configuration)
    .AddRedisConnection(builder.Configuration)
    .RegisterServices()
    .RegisterScheduledJobs()
    .AddHangfireService()
    .AddOption()
    .AddConfig()
    .AddServerInfo()
    .AddSiteConfig();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.EnvironmentName.Equals("Local"))
{
    app.UseDeveloperExceptionPage();
}

app.UseRouting();
app.UseEndpoints(endpoints =>
{
    endpoints.MapGet("/health", () => "OK.");
});

app.UseHangfireDashboard(pathMatch: "", options: new DashboardOptions()
{
    DashboardTitle = "AILab",
    AppPath = null,
    Authorization = new[] { new LocalAuthorizationFilter() }
});

MyJobManager.AddRecurringJob(app.Services);

var minWorkers = Environment.ProcessorCount * 8;
var minIOCPs = Environment.ProcessorCount * 8;
ThreadPool.SetMinThreads(minWorkers, minIOCPs);

app.Run();