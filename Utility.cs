using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;

namespace BanjoPancake.Analytics
{
    public static class ActuallyAJunkDrawer
    {
        public static void DisposeAndClear<TDisp>(this IList<TDisp> This)
            where TDisp : IDisposable
        {
            foreach (var v in This) v.Dispose();
            This.Clear();
        }

        public static IDisposable RecordTiming(this IAnalyticsSink This, string name)
        {
            var st = new Stopwatch();
            st.Start();

            return Disposable.Create(() => {
                st.Stop();
                This.RecordMetric(name, st.ElapsedMilliseconds);
            });
        }

        public static IObservable<T> GuaranteedThrottle<T>(this IObservable<T> This, TimeSpan delay, IScheduler sched)
        {
            return This
                .Select(x => Observable.Timer(delay, sched).Select(_ => x))
                .Switch();
        }
    }
}
