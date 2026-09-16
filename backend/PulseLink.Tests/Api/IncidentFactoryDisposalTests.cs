using System.Collections.Concurrent;
using PulseLink.Tests.Infrastructure;

namespace PulseLink.Tests.Api;

public class IncidentFactoryDisposalTests
{
    [LocalDbFact]
    public async Task SynchronousDisposal_DoesNotRequireCallingContextToPump()
    {
        var factory = new IncidentApiFactory(sqlServer: true);
        var context = new HeldSynchronizationContext();
        var disposal = Task.Run(() =>
        {
            SynchronizationContext.SetSynchronizationContext(context);
            try { factory.Dispose(); }
            finally { SynchronizationContext.SetSynchronizationContext(null); }
        });
        try
        {
            // A synchronous caller cannot pump continuations queued to itself.
            // Cleanup must finish independently, even on a constrained runner.
            await disposal.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            // Release queued work even when the regression fails, so its unique
            // disposable database is dropped rather than left behind.
            context.Release();
            await disposal.WaitAsync(TimeSpan.FromSeconds(20));
        }
    }

    private sealed class HeldSynchronizationContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> pending = new();
        private readonly object gate = new();
        private bool released;

        public override void Post(SendOrPostCallback callback, object? state)
        {
            lock (gate)
            {
                if (!released) { pending.Enqueue((callback, state)); return; }
            }
            ThreadPool.QueueUserWorkItem(_ => callback(state));
        }

        public void Release()
        {
            lock (gate)
            {
                released = true;
                while (pending.TryDequeue(out var item))
                    ThreadPool.QueueUserWorkItem(_ => item.Callback(item.State));
            }
        }
    }
}
