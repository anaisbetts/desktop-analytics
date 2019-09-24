using Akavache;
using Microsoft.VisualBasic.Devices;
using SharpRaven;
using SharpRaven.Data;
using SharpRaven.Logging;
using Splat;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reactive.Threading.Tasks;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BanjoPancake.Analytics
{
    public static class ErrorReporting
    {
        public static bool IsDebugMode => sentryKey == null;
        static string sentryKey;

        public static void Register(IMutableDependencyResolver resolver, string sentryKeyOrNullForDebugMode, ISecureBlobCache secureBlobCache = null)
        {
            sentryKey = sentryKeyOrNullForDebugMode;

            var logger = default(ILogger);
            var reporter = default(IRavenClient);

            if (IsDebugMode) {
                reporter = new DummyClient();
                logger = new DebugLogger();
                ((DebugLogger)logger).Level = LogLevel.Info;
            } else {
                var userFactory = new UniqueSentryUserFactory(secureBlobCache);
                reporter = new RavenClient(sentryKeyOrNullForDebugMode, sentryUserFactory: userFactory);
                logger = new SentryLogger(reporter);
                ((SentryLogger)logger).Level = LogLevel.Info;

                reporter.Compression = true;
                reporter.Release = Assembly.GetEntryAssembly().GetName().Version.ToString();
            }

            resolver.RegisterConstant(logger, typeof(ILogger));
            resolver.RegisterConstant(reporter, typeof(IRavenClient));
        }

        public static Dictionary<string, string> CollectStaticSystemInformation(Action<Dictionary<string, string>> fillInOSDependentInfo)
        {
            var ret = new Dictionary<string, string>();

            ret["Culture"] = CultureInfo.CurrentUICulture.ThreeLetterISOLanguageName;
            ret["AssemblyVersion"] = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            fillInOSDependentInfo(ret);

            return ret;
        }

        public static Dictionary<string, string> CollectDynamicSystemInformation(Dictionary<string, string> staticSystemInfo)
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

        public SentryLogger(IRavenClient client = null) => (Sentry) = client ?? Locator.Current.GetService<IRavenClient>();

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

    public class UniqueSentryUserFactory : ISentryUserFactory
    {
        readonly ISentryUserFactory innerFactory = new SentryUserFactory();
        ISecureBlobCache secureCache;

        public UniqueSentryUserFactory(ISecureBlobCache secureCache = null) => (this.secureCache) = secureCache;

        public SentryUser Create()
        {
            // NB: We have to call this way later because at the time we create the reporter,
            // we haven't initialized Akavache yet.
            this.secureCache = this.secureCache ?? Locator.Current.GetService<ISecureBlobCache>();
            var user = innerFactory.Create();

            var slugInfo = secureCache.GetOrCreateObject("slugInfo", () => {
                var prng = new Random();

                var data = new byte[8];
                prng.NextBytes(data);
                var slug = data.Aggregate(new StringBuilder(), (acc, x) => {
                    acc.Append(x.ToString("x2"));
                    return acc;
                });

                return new Dictionary<string, string> {
                    { "Slug", slug.ToString() }
                };
            }).ToTask().Result;

            user.Username = $"{user.Username}_{slugInfo["Slug"]}";
            return user;
        }
    }
}
