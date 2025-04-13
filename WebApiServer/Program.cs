using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WebApiServer.Config;

namespace WebApiServer;

public class Program
{
    public static async Task Main(string[] args)
    {
        Assembly a = Assembly.GetExecutingAssembly();
        string location = Path.GetDirectoryName(a.Location);
        Log.Init(Path.Combine(location, "logs", "loader.log"));
        Log.Info($"Plugin Loader Server - v{a.GetName().Version.ToString(3)}");

        ConfigFile config = await ConfigFile.TryLoadAsync(Path.Combine(location, "config.toml"));
        if (config == null)
            return;

        await Run(config.WebServer ?? new WebServerConfig());
    }

    private static async Task Run(WebServerConfig config, CancellationToken cancelToken = default)
    {
#if DEBUG
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")))
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
#else
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")))
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
#endif
        Directory.CreateDirectory("wwwroot");

        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions());
        builder.Configuration.Sources.Clear();

        Log.Link(builder.Host, "Web");

        config.ApplySettings(builder);

        if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            builder.Host.UseSystemd();

        builder.Services.AddControllers();

        var app = builder.Build();

        app.UseStaticFiles();

        app.MapControllers();

        await app.RunAsync(cancelToken);
    }
}
