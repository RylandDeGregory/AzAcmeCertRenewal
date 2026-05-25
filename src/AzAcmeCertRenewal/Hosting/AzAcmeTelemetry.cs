using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Azure.Core;
using Azure.Monitor.OpenTelemetry.Exporter;

internal static class AzAcmeTelemetry
{
    public const string ServiceName = "AzAcmeCertRenewal";
    public const string ActivitySourceName = ServiceName;

    public static void ConfigureAzAcmeTelemetry(this IHostApplicationBuilder builder, TokenCredential credential)
    {
        ArgumentNullException.ThrowIfNull(credential);

        var applicationInsightsConnectionString = builder.Configuration.GetValue<string>("APPLICATIONINSIGHTS_CONNECTION_STRING");

        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(options =>
        {
            options.SingleLine = true;

            if (builder.Environment.IsDevelopment())
            {
                options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffK ";
            }
        });

        builder.Logging.AddOpenTelemetry(options =>
        {
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
            options.ParseStateValues = true;
            options.SetResourceBuilder(CreateResourceBuilder());

            if (!string.IsNullOrWhiteSpace(applicationInsightsConnectionString))
            {
                options.AddAzureMonitorLogExporter(
                    exporterOptions => exporterOptions.ConnectionString = applicationInsightsConnectionString,
                    credential);
            }
        });

        if (string.IsNullOrWhiteSpace(applicationInsightsConnectionString))
        {
            return;
        }

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .WithTracing(tracing => tracing
                .AddSource(ActivitySourceName)
                .AddHttpClientInstrumentation()
                .AddAzureMonitorTraceExporter(
                    options => options.ConnectionString = applicationInsightsConnectionString,
                    credential))
            .WithMetrics(metrics => metrics
                .AddRuntimeInstrumentation()
                .AddHttpClientInstrumentation()
                .AddAzureMonitorMetricExporter(
                    options => options.ConnectionString = applicationInsightsConnectionString,
                    credential));
    }

    private static ResourceBuilder CreateResourceBuilder()
    {
        return ResourceBuilder.CreateDefault().AddService(ServiceName);
    }
}