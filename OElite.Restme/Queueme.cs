using System;
using System.IO;
using System.Threading.Tasks;

namespace OElite;

public static class RestmeMessageQueueExtensions
{
    public static bool Queueme(this Rest rest,
        object message,
        string? queueName = null, string? key = null,
        string? exchangeName = default,
        bool isDurable = true,
        bool isExclusive = false,
        bool autoDelete = true, string exchangeType = "direct", bool isMessagePersistent = true)
    {
        try
        {
            if (rest.QueueProvider == null)
            {
                rest.LogError("Queue provider not initialized. Please set CurrentMode to RabbitMq or reference OElite.Restme.RabbitMQ package.");
                return false;
            }

            // Use the new provider system
            var result = rest.QueueProvider.PublishAsync(message, queueName, key, exchangeName, 
                isDurable, isExclusive, autoDelete, exchangeType, isMessagePersistent).Result;
            
            return result;
        }
        catch (Exception? ex)
        {
            rest.LogError(ex.Message, ex);
            return false;
        }
    }

    public static void Dome<T>(this Rest rest,
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
            if (rest.QueueProvider == null)
            {
                rest.LogError("Queue provider not initialized. Please set CurrentMode to RabbitMq or reference OElite.Restme.RabbitMQ package.");
                return;
            }

            // Use the new provider system for consuming
            rest.QueueProvider.StartConsumingAsync<T>(queueTask, deliverCompleteCondition, 
                exchangeName, queueName, key, prefetchCount, isDurable, isExclusive, autoDelete, exchangeType).Wait();
        }
        catch (Exception? ex)
        {
            rest.LogError(ex.Message, ex);
        }
    }

    public static void Dome<T>(this Rest rest,
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
            if (rest.QueueProvider == null)
            {
                rest.LogError("Queue provider not initialized. Please set CurrentMode to RabbitMq or reference OElite.Restme.RabbitMQ package.");
                return;
            }

            // Convert synchronous delegates to async for the provider
            Func<T, Task<bool>>? asyncQueueTask = queueTask != null ? (t) => Task.FromResult(queueTask(t)) : null;
            Func<Task<bool>>? asyncDeliverCompleteCondition = deliverCompleteCondition != null ? () => Task.FromResult(deliverCompleteCondition()) : null;

            // Use the new provider system for consuming
            rest.QueueProvider.StartConsumingAsync<T>(asyncQueueTask, asyncDeliverCompleteCondition, 
                exchangeName, queueName, key, prefetchCount, isDurable, isExclusive, autoDelete, exchangeType).Wait();
        }
        catch (Exception? ex)
        {
            rest.LogError(ex.Message, ex);
        }
    }
}