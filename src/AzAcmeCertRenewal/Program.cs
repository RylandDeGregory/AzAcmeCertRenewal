using Azure.Identity;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
var credential = new DefaultAzureCredential();

builder.ConfigureAzAcmeTelemetry(credential);
builder.Services.AddAzAcmeRenewal(credential);

using var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(AzAcmeTelemetry.ServiceName);

try
{
    await host.Services.GetRequiredService<AzAcmeRenewalWorker>().RunAsync(CancellationToken.None);
}
catch (Exception ex)
{
    logger.LogCritical(ex, "Certificate renewal run failed");
    Environment.ExitCode = 1;
}