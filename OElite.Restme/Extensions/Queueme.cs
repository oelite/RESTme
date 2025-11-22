using System;
using System.Threading;
using System.Threading.Tasks;
using OElite.Restme.Abstractions;

// ReSharper disable once CheckNamespace
namespace OElite;

public static class QueueProviderExtensions
{
    public static bool Queueme(this IQueueProvider queueProvider,
        object message,
        string? queueName = null, string? key = null,
        string? exchangeName = null,
        bool isDurable = true,
        bool isExclusive = false,
        bool autoDelete = true, string exchangeType = "direct", bool isMessagePersistent = true)
    {
        try
        {
            // Use the provider directly for publishing
            var result = queueProvider.PublishAsync(message, queueName, key, exchangeName,
                isDurable, isExclusive, autoDelete, exchangeType, isMessagePersistent).Result;

            return result;
        }
        catch (Exception? ex)
        {
            return false;
        }
    }

    public static async Task<bool> QueuemeAsync(this IQueueProvider queueProvider,
        object message,
        string? queueName = null, string? key = null,
        string? exchangeName = null,
        bool isDurable = true,
        bool isExclusive = false,
        bool autoDelete = true, string exchangeType = "direct", bool isMessagePersistent = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Use the provider directly with cancellation token
            var result = await queueProvider.PublishAsync(message, queueName, key, exchangeName,
                isDurable, isExclusive, autoDelete, exchangeType, isMessagePersistent, cancellationToken);

            return result;
        }
        catch (Exception? ex) when (!(ex is OperationCanceledException))
        {
            return false;
        }
    }

    public static void Dome<T>(this IQueueProvider queueProvider,
        Func<T, Task<bool>>? queueTask,
        Func<Task<bool>>? deliverCompleteCondition,
        string? exchangeName = null,
        string? queueName = null, string? key = null,
        ushort prefetchCount = 1,
        bool isDurable = true,
        bool isExclusive = false,
        bool autoDelete = true,
        string exchangeType = "direct") where T : class
    {
        try
        {
            // Use the provider directly for consuming
            queueProvider.StartConsumingAsync<T>(queueTask, deliverCompleteCondition,
                exchangeName, queueName, key, prefetchCount, isDurable, isExclusive, autoDelete, exchangeType).Wait();
        }
        catch (Exception? ex)
        {
            // Silent fail for provider-specific extensions
        }
    }

    public static void Dome<T>(this IQueueProvider queueProvider,
        Func<T, bool>? queueTask,
        Func<bool>? deliverCompleteCondition,
        string? exchangeName = null,
        string? queueName = null, string? key = null,
        ushort prefetchCount = 1,
        bool isDurable = true,
        bool isExclusive = false,
        bool autoDelete = true,
        string exchangeType = "direct") where T : class
    {
        try
        {
            // Convert synchronous delegates to async for the provider
            Func<T, Task<bool>>? asyncQueueTask = queueTask != null ? (t) => Task.FromResult(queueTask(t)) : null;
            Func<Task<bool>>? asyncDeliverCompleteCondition = deliverCompleteCondition != null
                ? () => Task.FromResult(deliverCompleteCondition())
                : null;

            // Use the provider directly for consuming
            queueProvider.StartConsumingAsync<T>(asyncQueueTask, asyncDeliverCompleteCondition,
                exchangeName, queueName, key, prefetchCount, isDurable, isExclusive, autoDelete, exchangeType).Wait();
        }
        catch (Exception? ex)
        {
            // Silent fail for provider-specific extensions
        }
    }

    public static async Task DomeAsync<T>(this IQueueProvider queueProvider,
        Func<T, Task<bool>>? queueTask,
        Func<Task<bool>>? deliverCompleteCondition,
        string? exchangeName = null,
        string? queueName = null, string? key = null,
        ushort prefetchCount = 1,
        bool isDurable = true,
        bool isExclusive = false,
        bool autoDelete = true,
        string exchangeType = "direct", CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            // Use the provider directly for consuming with cancellation token
            await queueProvider.StartConsumingAsync<T>(queueTask, deliverCompleteCondition,
                exchangeName, queueName, key, prefetchCount, isDurable, isExclusive, autoDelete, exchangeType,
                cancellationToken);
        }
        catch (Exception? ex) when (!(ex is OperationCanceledException))
        {
            // Silent fail for provider-specific extensions
        }
    }

    public static async Task DomeAsync<T>(this IQueueProvider queueProvider,
        Func<T, bool>? queueTask,
        Func<bool>? deliverCompleteCondition,
        string? exchangeName = null,
        string? queueName = null, string? key = null,
        ushort prefetchCount = 1,
        bool isDurable = true,
        bool isExclusive = false,
        bool autoDelete = true,
        string exchangeType = "direct", CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            // Convert synchronous delegates to async for the provider
            Func<T, Task<bool>>? asyncQueueTask = queueTask != null ? (t) => Task.FromResult(queueTask(t)) : null;
            Func<Task<bool>>? asyncDeliverCompleteCondition = deliverCompleteCondition != null
                ? () => Task.FromResult(deliverCompleteCondition())
                : null;

            // Use the provider directly for consuming with cancellation token
            await queueProvider.StartConsumingAsync<T>(asyncQueueTask, asyncDeliverCompleteCondition,
                exchangeName, queueName, key, prefetchCount, isDurable, isExclusive, autoDelete, exchangeType,
                cancellationToken);
        }
        catch (Exception? ex) when (!(ex is OperationCanceledException))
        {
            // Silent fail for provider-specific extensions
        }
    }
}