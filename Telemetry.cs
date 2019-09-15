using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using SharpRaven;
using SharpRaven.Data;
using Splat;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace BanjoPancake.Analytics
{
    public static class Telemetry
    {
        public static void Register(IMutableDependencyResolver resolver, string instrumentationKeyOrNullIfDebug, Dictionary<string, string> staticSystemInfo)
        {
            var analytics = default(IAnalyticsSink);
            var tc = default(TelemetryClient);

            if (instrumentationKeyOrNullIfDebug != null) {
                tc = new TelemetryClient();
                TelemetryConfiguration.Active.InstrumentationKey = instrumentationKeyOrNullIfDebug;

                resolver.RegisterConstant(tc, typeof(TelemetryClient));

                tc.Context.Session.Id = Guid.NewGuid().ToString();
                tc.Context.Device.OperatingSystem = Environment.OSVersion.ToString();

                foreach (var kvp in staticSystemInfo) { tc.Context.Properties[kvp.Key] = kvp.Value; }

                analytics = new LiveAnalyticsSink();

                var userFactory = new UniqueSentryUserFactory();
                tc.Context.User.Id = userFactory.Create().Username;
            } else {
                analytics = new LoggerAnalyticsSink();
            }

            resolver.RegisterConstant(analytics, typeof(IAnalyticsSink));
        }

        public static Task Shutdown()
        {
            var tc = Locator.Current.GetService<TelemetryClient>();
            if (tc == null) return Task.FromResult(true);

            tc.Flush();
            return Task.Delay(1000);
        }
    }

    public interface IAnalyticsSink
    {
        void RecordUserInteraction(string name, string message);
        void RecordMetric(string name, double val);
        void RecordPageview(string name);
    }

    public class LiveAnalyticsSink : IAnalyticsSink
    {
        IRavenClient sentry;
        public IRavenClient Sentry => sentry ?? (sentry = Locator.Current.GetService<IRavenClient>());

        TelemetryClient telemetry;
        public TelemetryClient Telemetry => telemetry ?? (telemetry = Locator.Current.GetService<TelemetryClient>());

        public LiveAnalyticsSink(IRavenClient sentry = null, TelemetryClient appInsights = null)
        {
            this.sentry = sentry;
            this.telemetry = appInsights;
        }

        public void RecordUserInteraction(string name, string message)
        {
            try {
                Sentry.AddTrail(new Breadcrumb("interaction") { Message = message, Level = BreadcrumbLevel.Info });
                Telemetry.TrackEvent(name);
            } catch (Exception) {
                // NB: Never crash because telemetry fails
            }
        }

        public void RecordMetric(string name, double val)
        {
            try {
                Telemetry.TrackMetric(name, val);
            } catch (Exception) {
                // NB: Never crash because telemetry fails
            }
        }

        public void RecordPageview(string name)
        {
            try {
                Sentry.AddTrail(new Breadcrumb("interaction") { Message = $"Opened page {name}", Level = BreadcrumbLevel.Info });
                Telemetry.TrackPageView(name);
            } catch (Exception) {
                // NB: Never crash because telemetry fails
            }
        }
    }

    public class LoggerAnalyticsSink : IAnalyticsSink, IEnableLogger
    {
        public void RecordMetric(string name, double val)
        {
            this.Log().Info($"Recorded metric {name} = {val}");
        }

        public void RecordPageview(string name)
        {
            RecordUserInteraction("Pageview", $"Visited page {name}");
        }

        public void RecordUserInteraction(string name, string message)
        {
            this.Log().Info($"User Interaction: {message}");
        }
    }
}
