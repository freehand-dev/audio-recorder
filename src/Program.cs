using audio_recorder.Models;
using audio_recorder.Services;
using Microsoft.Extensions.Hosting.WindowsServices;

if (WindowsServiceHelpers.IsWindowsService())
    Directory.SetCurrentDirectory(AppContext.BaseDirectory);

HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings()
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "AudioRecorder";
});

builder.Services.AddSystemd();

builder.Configuration.AddJsonFile($"{builder.Environment.ApplicationName}.json", optional: true);
builder.Configuration.AddYamlFile($"{builder.Environment.ApplicationName}.yaml", optional: true);
builder.Configuration.AddCommandLine(args);
builder.Configuration.AddEnvironmentVariables();


//
string? localConfig = builder.Configuration["config"];
if (!string.IsNullOrEmpty(localConfig))
{
    if (Path.GetExtension(localConfig).Equals(".json", StringComparison.OrdinalIgnoreCase))
    {
        builder.Configuration.AddJsonFile(localConfig, optional: true);
    }
    else if (Path.GetExtension(localConfig).Equals(".yaml", StringComparison.OrdinalIgnoreCase) ||
             Path.GetExtension(localConfig).Equals(".yml", StringComparison.OrdinalIgnoreCase))
    {
        builder.Configuration.AddYamlFile(localConfig, optional: true);
    }
    else
    {
        throw new ArgumentException($"Unsupported configuration file format: {localConfig}");
    }
}

//
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.Configure<CleanerOptions>(builder.Configuration.GetSection("Cleaner"));
builder.Services.Configure<RecorderOptions>(builder.Configuration.GetSection("Recorder"));

builder.Services.AddHostedService<AudioCleaner>();
builder.Services.AddHostedService<AudioRecorder>();

var host = builder.Build();

await host.RunAsync();
