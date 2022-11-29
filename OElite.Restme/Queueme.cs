using System;
using System.Dynamic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OElite;

public static class RestmeMessageQueueExtensions
{
    public static bool Queueme(this Rest rest,
        object message,
        string queueName = default, string key = default,
        string exchangeName = default,
        bool isDurable = true,
        bool isExclusive = false,
        bool autoDelete = true, string exchangeType = "direct")
    {
        try
        {
            if (exchangeName.IsNotNullOrEmpty())
            {
                rest.RabbitMqChannel.ExchangeDeclare(exchangeName, exchangeType, isDurable, autoDelete);
            }
            else exchangeName = string.Empty;

            if (queueName.IsNotNullOrEmpty())
            {
                rest.RabbitMqChannel.QueueDeclare(queueName, isDurable, isExclusive, autoDelete);
                if (key.IsNullOrEmpty()) key = queueName;
            }

            if (key.IsNullOrEmpty()) key = string.Empty;

            if (queueName.IsNotNullOrEmpty() && exchangeName.IsNotNullOrEmpty() && key.IsNotNullOrEmpty())
                rest.RabbitMqChannel.QueueBind(queueName, exchangeName, key);


            var objBytes = message.JsonSerialize().ToStream().ToBytes();
            rest.RabbitMqChannel.BasicPublish(exchangeName, key,
                body: objBytes);


            return true;
        }
        catch (Exception ex)
        {
            rest.LogError(ex?.Message, ex);
            return false;
        }

        return false;
    }

    public static void Dome<T>(this Rest rest,
        Func<T, Task<bool>> queueTask,
        Func<Task<bool>> deliverCompleteCondition,
        string exchangeName = default,
        string queueName = default, string key = default,
        ushort prefetchCount = 1,
        bool isDurable = true,
        bool isExclusive = false,
        bool autoDelete = true,
        string exchangeType = "direct")
    {
        try
        {
            if (exchangeName.IsNotNullOrEmpty())
            {
                rest.RabbitMqChannel.ExchangeDeclare(exchangeName, exchangeType, isDurable, autoDelete);
            }
            else exchangeName = string.Empty;

            if (queueName.IsNotNullOrEmpty())
            {
                rest.RabbitMqChannel.QueueDeclare(queueName, isDurable, isExclusive, autoDelete);
            }
            else
            {
                queueName = rest.RabbitMqChannel.QueueDeclare().QueueName;
            }

            if (key.IsNullOrEmpty()) key = queueName;

            if (queueName.IsNotNullOrEmpty() && exchangeName.IsNotNullOrEmpty() && key.IsNotNullOrEmpty())
                rest.RabbitMqChannel.QueueBind(queueName, exchangeName, key);

            var consumer = new EventingBasicConsumer(rest.RabbitMqChannel);
            consumer.Received += async (chn, args) =>
            {
                var result = StringUtils.GetStringFromStream(new MemoryStream(args.Body.ToArray()))
                    .JsonDeserialize<T>();
                if (queueTask == null || (await queueTask(result)))
                {
                    rest.RabbitMqChannel.BasicAck(args.DeliveryTag, false);
                }
                else
                {
                    rest.RabbitMqChannel.BasicNack(args.DeliveryTag, false, true);
                }
            };
            // prefetchCount = 1  ---> accept only one unack-ed message at a time
            rest.RabbitMqChannel.BasicQos(0, prefetchCount, false);
            rest.RabbitMqChannel.BasicConsume(queueName, false, consumer);

            if (deliverCompleteCondition == null) return;
            var isComplete = false;
            while (!isComplete)
            {
                isComplete = deliverCompleteCondition.Invoke().WaitAndGetResult();
                //no nothing, keep the loop await
            }
        }
        catch (Exception ex)
        {
            rest.LogError(ex?.Message, ex);
        }
    }

    public static void Dome<T>(this Rest rest,
        Func<T, bool> queueTask,
        Func<bool> deliverCompleteCondition,
        string exchangeName = default,
        string queueName = default, string key = default,
        ushort prefetchCount = 1,
        bool isDurable = true,
        bool isExclusive = false,
        bool autoDelete = true,
        string exchangeType = "direct")
    {
        try
        {
            if (exchangeName.IsNotNullOrEmpty())
            {
                rest.RabbitMqChannel.ExchangeDeclare(exchangeName, exchangeType, isDurable, autoDelete);
            }
            else exchangeName = string.Empty;

            if (queueName.IsNotNullOrEmpty())
            {
                rest.RabbitMqChannel.QueueDeclare(queueName, isDurable, isExclusive, autoDelete);
            }
            else
            {
                queueName = rest.RabbitMqChannel.QueueDeclare().QueueName;
            }

            if (key.IsNullOrEmpty()) key = queueName;

            if (queueName.IsNotNullOrEmpty() && exchangeName.IsNotNullOrEmpty() && key.IsNotNullOrEmpty())
                rest.RabbitMqChannel.QueueBind(queueName, exchangeName, key);

            var consumer = new EventingBasicConsumer(rest.RabbitMqChannel);
            consumer.Received += (chn, args) =>
            {
                var result = StringUtils.GetStringFromStream(new MemoryStream(args.Body.ToArray()))
                    .JsonDeserialize<T>();
                if (queueTask == null || queueTask(result))
                {
                    rest.RabbitMqChannel.BasicAck(args.DeliveryTag, false);
                }
                else
                {
                    rest.RabbitMqChannel.BasicNack(args.DeliveryTag, false, true);
                }
            };
            // prefetchCount = 1  ---> accept only one unack-ed message at a time
            rest.RabbitMqChannel.BasicQos(0, prefetchCount, false);
            rest.RabbitMqChannel.BasicConsume(queueName, false, consumer);

            if (deliverCompleteCondition == null) return;
            var isComplete = false;
            while (!isComplete)
            {
                isComplete = deliverCompleteCondition.Invoke();
                //no nothing, keep the loop await
            }
        }
        catch (Exception ex)
        {
            rest.LogError(ex?.Message, ex);
        }
    }
}