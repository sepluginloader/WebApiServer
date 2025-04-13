using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;
using Tomlyn.Model;

namespace WebApiServer.Config;
public class WebServerConfig : ITomlMetadataProvider
{
    public const string CorsPolicyName = "_allowSpecificOrigins";

    public string BindAddress { get; set; } = "*";

#if DEBUG
    public ushort HttpPort { get; set; } = 8080;
    public ushort HttpsPort { get; set; } = 0;
    public string[] AllowedHosts { get; set; } = ["localhost"];
#else
    public ushort HttpPort { get; set; } = 80;
    public ushort HttpsPort { get; set; } = 0;
    public string[] AllowedHosts { get; set; } = ["api.sepluginloader.com"];
#endif
    public string[] CorsAllowedOrigins { get; set; } = [];

    public bool Hsts { get; set; } = false;

    // Example: certificate.pfx
    public string SslCertificateFile { get; set; } = "";
    public string SslCertificateKeyFile { get; set; }
    public string SslCertificatePassword { get; set; } = "";

    public int RateLimit { get; set; } = -1;
    public int RateLimitQueue { get; set; } = 2;
    public double RateLimitRate { get; set; } = 12;

    public WebServerConfig() { }

    internal void ApplySettings(WebApplicationBuilder webBuilder)
    {
        if (CorsAllowedOrigins != null && CorsAllowedOrigins.Length > 0)
            webBuilder.Services.AddCors(ApplyCorsSettings);
        else
            Log.Warn("No allowed origins specified, CORS requests will fail");

        IWebHostBuilder webHostBuilder = webBuilder.WebHost.UseKestrel(ApplyKestrelSettings);
        if (AllowedHosts != null && AllowedHosts.Length > 0)
            webHostBuilder.ConfigureServices(ApplyHostnameFilter);

        if (RateLimit > 0)
            webBuilder.Services.AddRateLimiter(ApplyRateLimiter);
    }

    private void ApplyRateLimiter(RateLimiterOptions options)
    {
        FixedWindowRateLimiterOptions fixedWindowRateLimiterOptions = new FixedWindowRateLimiterOptions
        {
            AutoReplenishment = false,
            PermitLimit = RateLimit,
            QueueLimit = RateLimitQueue,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            Window = TimeSpan.FromSeconds(RateLimitRate),
        };

        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.User.Identity?.Name ?? httpContext.Request.Headers.Host.ToString(),
                factory: partition => fixedWindowRateLimiterOptions)
        );
    }

    private void ApplyCorsSettings(CorsOptions options)
    {

        string[] hosts = CorsAllowedOrigins.Select(x =>
        {
            if (x.StartsWith("http:", StringComparison.InvariantCultureIgnoreCase) || x.StartsWith("https:", StringComparison.InvariantCultureIgnoreCase))
                return x;
            return "https://" + x;
        }).ToArray();

        options.AddPolicy(
            name: CorsPolicyName,
            policy =>
            {
                policy.WithOrigins(hosts);
                policy.AllowAnyHeader();
                policy.AllowAnyMethod();
            });
    }

    private void ApplyHostnameFilter(IServiceCollection services)
    {
        services.AddHostFiltering(options => options.AllowedHosts = AllowedHosts);
    }

    private void ApplyKestrelSettings(KestrelServerOptions options)
    {

        if (HttpPort == 0 && HttpsPort == 0)
            throw new Exception("No server ports defined");

        if (BindAddress != null && IPAddress.TryParse(BindAddress, out IPAddress bindAddress))
        {
            if (HttpPort > 0)
                options.Listen(bindAddress, HttpPort);

            if (HttpsPort > 0)
            {
                options.Listen(bindAddress, HttpsPort, options =>
                {
                    if (string.IsNullOrWhiteSpace(SslCertificateFile))
                    {
                        Log.Warn("Https port specified without certificate file");
                        options.UseHttps();
                    }
                    else
                    {
                        options.UseHttps(ReadCertificate());
                    }
                });
            }

            return;
        }

        if (HttpPort > 0)
            options.ListenAnyIP(HttpPort);

        if (HttpsPort > 0)
        {
            options.ListenAnyIP(HttpsPort, options =>
            {
                if (string.IsNullOrWhiteSpace(SslCertificateFile))
                {
                    Log.Warn("Https port specified without certificate file");
                    options.UseHttps();
                }
                else
                {
                    options.UseHttps(ReadCertificate());
                }
            });
        }
    }

    private X509Certificate2 ReadCertificate()
    {
        bool password = !string.IsNullOrWhiteSpace(SslCertificatePassword);
        string extension = Path.GetExtension(SslCertificateFile);
        if (string.IsNullOrEmpty(extension))
        {
            if (password)
                return new X509Certificate2(SslCertificateFile, SslCertificatePassword);
            return new X509Certificate2(SslCertificateFile);
        }

        switch (extension.ToLowerInvariant())
        {
            case ".pem":
                string keyFile = null;
                if (!string.IsNullOrWhiteSpace(SslCertificateKeyFile))
                    keyFile = SslCertificateKeyFile;
                if (password)
                    return X509Certificate2.CreateFromEncryptedPemFile(SslCertificateFile, SslCertificatePassword, keyFile);
                return X509Certificate2.CreateFromPemFile(SslCertificateFile, keyFile);
            case ".pfx":
            case ".p12":
                if (password)
                    return new X509Certificate2(SslCertificateFile, SslCertificatePassword);
                return new X509Certificate2(SslCertificateFile);
        }
        throw new IOException("SSL certificate format not supported.");
    }


    [IgnoreDataMember]
    public TomlPropertiesMetadata PropertiesMetadata { get; set; }
}
