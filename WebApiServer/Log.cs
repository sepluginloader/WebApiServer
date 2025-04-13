using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Templates;
using Serilog.Templates.Themes;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace WebApiServer;

public static class Log
{
    private const string LogFormat = "{@t:HH:mm:ss} [{@l:u3}] [{ThreadID}]{CustomPrefix} {@m} {@x}\n";

    private static Logger log;
    private static readonly List<Logger> customLoggers = new List<Logger>();

    public static void Init(string file)
    {
        AppDomain.CurrentDomain.UnhandledException += UnhandledException;


        ExpressionTemplate format = new ExpressionTemplate(LogFormat);
        log = new LoggerConfiguration()
            .Enrich.With(new ThreadIDEnricher())
#if DEBUG
            .WriteTo.Debug(format)
#endif
            .WriteTo.Console(new ExpressionTemplate(LogFormat, theme: TemplateTheme.Literate))
            .WriteTo.File(format, file, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 31)
            .CreateLogger();

    }

    public static void Link(Microsoft.Extensions.Hosting.IHostBuilder hostBuilder, string name)
    {
        hostBuilder.UseSerilog(CreateCustomLogger(name), false);
    }

    public static void Link(ILoggingBuilder logBuilder, string name)
    {
        logBuilder.AddSerilog(CreateCustomLogger(name), false);
    }

    public static Logger CreateCustomLogger(string name)
    {
        LoggerConfiguration config = new LoggerConfiguration()
            .Enrich.With(new PrefixEnricher(" [" + name + "]"))
            .WriteTo.Logger(log);
        Logger customLog = config.CreateLogger();
        customLoggers.Add(customLog);
        return customLog;
    }

    private static void UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            log.Fatal(ex, "An exception was thrown:");
        else
            log.Fatal("An unhandled exception occurred.");
    }

    public static void Info(string msg)
    {
        Write(LogEventLevel.Information, msg);
    }

    public static void Error(string msg)
    {
        Write(LogEventLevel.Error, msg);
    }

    public static void Warn(string msg)
    {
        Write(LogEventLevel.Warning, msg);
    }

    public static void Error(Exception ex)
    {
        log.Error(ex, "An exception was thrown:");
    }

    public static void Error(Exception ex, string msg)
    {
        log.Error(ex, msg);
    }

    internal static void Error(string msg, Exception ex)
    {
        log.Error(ex, msg);
    }

    public static void Fatal(string msg)
    {
        log.Fatal(msg);
    }

    private static void Write(LogEventLevel level, string msg)
    {
        log.Write(level, msg);
    }

    public static void Close()
    {
        log.Dispose();
        foreach (Logger log in customLoggers)
            log.Dispose();
    }


    private class PrefixEnricher : ILogEventEnricher
    {
        private readonly string prefix;

        public PrefixEnricher(string prefix)
        {
            this.prefix = " " + prefix;
        }

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CustomPrefix", prefix));
        }
    }

    private class ThreadIDEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
              "ThreadID", Environment.CurrentManagedThreadId.ToString()));
        }
    }
}