using Microsoft.VisualBasic.Devices;
using SharpRaven;
using SharpRaven.Data;
using SharpRaven.Logging;
using Splat;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;

namespace BanjoPancake.Analytics
{
    public static class ErrorReporting
    {
        public static bool IsDebugMode => sentryKey == null;
        static string sentryKey;

        public static void Register(IMutableDependencyResolver resolver, string sentryKeyOrNullForDebugMode, Action<string, string> fillInOSDependentInfo)
        {
            sentryKey = sentryKeyOrNullForDebugMode;

            if (IsDebugMode) {
                IRavenClient reporter = new DummyClient();
                var logger = new DebugLogger();
                logger.Level = LogLevel.Info;
            } else {
                var userFactory = new UniqueSentryUserFactory();
                IRavenClient reporter = new RavenClient("https://08e38cda1d2e4aba8b35cb16a968360c:075db9f542464f06bcd9a80576a915b2@sentry.io/186611", sentryUserFactory: userFactory);
                var logger = new SentryLogger(reporter);
                logger.Level = LogLevel.Info;

            }
        }

        static Dictionary<string, string> collectStaticSystemInformation(Action<Dictionary<string, string>> fillInOSDependentInfo)
        {
            var ret = new Dictionary<string, string>();

            ret["Culture"] = CultureInfo.CurrentUICulture.ThreeLetterISOLanguageName;
            ret["AppArchitecture"] = Diagnostics.AppArchitecture;
            ret["ClrVersion"] = Diagnostics.ClrVersion;
            ret["DetectedOSVersion"] = Diagnostics.DetectedOSVersion.Value;
            ret["OSArchitecture"] = Diagnostics.OSArchitecture;
            ret["ProcessorCount"] = Diagnostics.ProcessorCount;
            ret["ServicePack"] = Diagnostics.ServicePack;
            ret["AppArchitecture"] = Diagnostics.AppArchitecture;
            ret["FullOSVersion"] = Diagnostics.FullOSVersion.Value;
            ret["AssemblyVersion"] = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            return ret;
        }

        static Dictionary<string, string> collectDynamicSystemInformation(Dictionary<string, string> staticSystemInfo)
        {
            var ret = staticSystemInfo;
            var ci = new ComputerInfo();

            ret["AvailablePhysicalMemory"] = ci.AvailablePhysicalMemory.ToString();
            ret["AvailableVirtualMemory"] = ci.AvailableVirtualMemory.ToString();
            ret["TotalPhysicalMemory"] = ci.TotalPhysicalMemory.ToString();

            return ret;
        }
    }

    public class DummyClient : IRavenClient
    {
        public Func<Requester, Requester> BeforeSend { get; set; }
        public bool Compression { get; set; }

        public Dsn CurrentDsn { get; }

        public string Environment { get; set; }
        public bool IgnoreBreadcrumbs { get; set; }
        public string Logger { get; set; }
        public IScrubber LogScrubber { get; set; }
        public string Release { get; set; }

        public IDictionary<string, string> Tags => new Dictionary<string, string>();

        public TimeSpan Timeout { get; set; }

        public void AddTrail(Breadcrumb breadcrumb) { }
        public string Capture(SentryEvent @event) { return ""; }
        public Task<string> CaptureAsync(SentryEvent @event) { return Task.FromResult(""); }
        public string CaptureEvent(Exception e) { return ""; }
        public string CaptureEvent(Exception e, Dictionary<string, string> tags) { return ""; }
        public string CaptureException(Exception exception, SentryMessage message = null, ErrorLevel level = ErrorLevel.Error, IDictionary<string, string> tags = null, string[] fingerprint = null, object extra = null) { return ""; }
        public Task<string> CaptureExceptionAsync(Exception exception, SentryMessage message = null, ErrorLevel level = ErrorLevel.Error, IDictionary<string, string> tags = null, string[] fingerprint = null, object extra = null) { return Task.FromResult(""); }
        public string CaptureMessage(SentryMessage message, ErrorLevel level = ErrorLevel.Info, IDictionary<string, string> tags = null, string[] fingerprint = null, object extra = null) { return ""; }
        public Task<string> CaptureMessageAsync(SentryMessage message, ErrorLevel level = ErrorLevel.Info, IDictionary<string, string> tags = null, string[] fingerprint = null, object extra = null) { return Task.FromResult(""); }
        public void RestartTrails() { }
    }

    public class SentryLogger : ILogger
    {
        public LogLevel Level { get; set; }
        public IRavenClient Sentry { get; private set; }

        public SentryLogger(IRavenClient client = null)
        {
            Sentry = client ?? Locator.Current.GetService<IRavenClient>();
        }

        static BreadcrumbLevel toBreadcrumbLevel(LogLevel level)
        {
            switch (level) {
            case LogLevel.Debug:
                return BreadcrumbLevel.Debug;
            case LogLevel.Error:
                return BreadcrumbLevel.Error;
            case LogLevel.Fatal:
                return BreadcrumbLevel.Critical;
            case LogLevel.Info:
                return BreadcrumbLevel.Info;
            case LogLevel.Warn:
                return BreadcrumbLevel.Warning;
            }

            throw new Exception("Unknown Log Level");
        }

        public void Write([Localizable(false)] string message, LogLevel logLevel)
        {
            if (logLevel < Level) return;
            Sentry.AddTrail(new Breadcrumb("log") { Message = message, Level = toBreadcrumbLevel(logLevel) });
        }

        public void Write(Exception exception, [Localizable(false)] string message, LogLevel logLevel)
        {
            if (logLevel < Level) return;
            Sentry.AddTrail(new Breadcrumb("log") { Message = message, Level = toBreadcrumbLevel(logLevel) });
        }

        public void Write([Localizable(false)] string message, [Localizable(false)] Type type, LogLevel logLevel)
        {
            if (logLevel < Level) return;
            Sentry.AddTrail(new Breadcrumb("log") { Message = message, Level = toBreadcrumbLevel(logLevel) });
        }

        public void Write(Exception exception, [Localizable(false)] string message, [Localizable(false)] Type type, LogLevel logLevel)
        {
            if (logLevel < Level) return;
            Sentry.AddTrail(new Breadcrumb("log") { Message = message, Level = toBreadcrumbLevel(logLevel) });
        }
    }
}
