using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using OElite.Abstractions;

namespace OElite.Base
{
    /// <summary>
    /// In-memory queue provider using Channels. Suitable for single-process scenarios.
    /// </summary>
    public sealed class MemoryQueueProvider : IQueueProvider
    {
        private readonly ConcurrentDictionary<string, Channel<object>> _queues = new();
        private CancellationTokenSource? _cts;
        private bool _disposed;

        public Task<bool> PublishAsync<T>(T message, string? queueName = null, string? routingKey = null, string? exchangeName = null, bool isDurable = true, bool isExclusive = false, bool autoDelete = true, string exchangeType = "direct", bool isMessagePersistent = true, CancellationToken cancellationToken = default) where T : class
        {
            var q = GetOrCreateQueue(queueName ?? "default");
            return Task.FromResult(q.Writer.TryWrite(message));
        }

        public Task StartConsumingAsync<T>(Func<T, Task<bool>> messageHandler, Func<Task<bool>>? completionCondition = null, string? exchangeName = null, string? queueName = null, string? routingKey = null, ushort prefetchCount = 1, bool isDurable = true, bool isExclusive = false, bool autoDelete = true, string exchangeType = "direct", CancellationToken cancellationToken = default) where T : class
        {
            var q = GetOrCreateQueue(queueName ?? "default");
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = _cts.Token;

            _ = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    var msg = await q.Reader.ReadAsync(token).ConfigureAwait(false);
                    if (msg is T typed)
                    {
                        try { await messageHandler(typed).ConfigureAwait(false); }
                        catch { /* ignore handler errors for base provider */ }
                    }
                    if (completionCondition != null)
                    {
                        try
                        {
                            if (await completionCondition().ConfigureAwait(false)) break;
                        }
                        catch { /* ignore */ }
                    }
                }
            }, token);

            return Task.CompletedTask;
        }

        public Task StopConsumingAsync(CancellationToken cancellationToken = default)
        {
            _cts?.Cancel();
            return Task.CompletedTask;
        }

        public Task<string> DeclareQueueAsync(string? queueName = null, bool isDurable = true, bool isExclusive = false, bool autoDelete = true, CancellationToken cancellationToken = default)
        {
            queueName ??= "default";
            GetOrCreateQueue(queueName);
            return Task.FromResult(queueName);
        }

        public Task DeclareExchangeAsync(string exchangeName, string exchangeType = "direct", bool isDurable = true, bool autoDelete = true, CancellationToken cancellationToken = default)
        {
            // No-op for in-memory base provider
            return Task.CompletedTask;
        }

        public Task BindQueueAsync(string queueName, string exchangeName, string routingKey, CancellationToken cancellationToken = default)
        {
            // No-op for in-memory base provider
            return Task.CompletedTask;
        }

        private Channel<object> GetOrCreateQueue(string name)
        {
            return _queues.GetOrAdd(name, _ => Channel.CreateUnbounded<object>());
        }

        public void Dispose()
        {
            if (_disposed) return;
            _cts?.Cancel();
            _cts?.Dispose();
            _disposed = true;
        }
    }
}


