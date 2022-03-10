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
    public static ConnectionFactory RabbitMqConnectionFactory { get; set; }

    public static bool Queueme(this Rest rest,
        object message,
        string queueName = default, string key = default,
        string exchangeName = default,
        bool isDurable = true,
        bool isExclusive = false,
        bool autoDelete = true, string exchangeType = "direct")
    {
        if (rest?.CurrentMode != RestMode.RabbitMq) throw new NotSupportedException("Only RabbitMq mode is supported.");
        if ((rest.Configuration?.RestKey).IsNullOrEmpty() || (rest.Configuration?.RestSecret).IsNullOrEmpty())
            throw new OEliteException("invalid username and password for rabbitmq");

        if (rest.Configuration != null && (RabbitMqConnectionFactory?.UserName != rest.Configuration.RestKey ||
                                           RabbitMqConnectionFactory?.Password != rest.Configuration.RestSecret ||
                                           RabbitMqConnectionFactory?.HostName != rest.ConnectionString))

        {
            try
            {
                RabbitMqConnectionFactory = new ConnectionFactory
                {
                    UserName = rest.Configuration.RestKey,
                    Password = rest.Configuration.RestSecret,
                    Endpoint = new AmqpTcpEndpoint(rest.BaseUri),
                    ClientProvidedName = "Restme MQ"
                };

                using var conn = RabbitMqConnectionFactory.CreateConnection();
                using var channel = conn.CreateModel();
                if (exchangeName.IsNotNullOrEmpty())
                {
                    channel.ExchangeDeclare(exchangeName, exchangeType, isDurable, autoDelete);
                }
                else exchangeName = string.Empty;

                if (queueName.IsNotNullOrEmpty())
                {
                    channel.QueueDeclare(queueName, isDurable, isExclusive, autoDelete);
                    if (key.IsNullOrEmpty()) key = queueName;
                }

                if (key.IsNullOrEmpty()) key = string.Empty;

                if (queueName.IsNotNullOrEmpty() && exchangeName.IsNotNullOrEmpty() && key.IsNotNullOrEmpty())
                    channel.QueueBind(queueName, exchangeName, key);


                var objBytes = message.JsonSerialize().ToStream().ToBytes();
                channel.BasicPublish(exchangeName, key,
                    body: objBytes);


                return true;
            }
            catch (Exception ex)
            {
                rest.LogError(ex?.Message, ex);
                return false;
            }
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
            RabbitMqConnectionFactory = new ConnectionFactory
            {
                UserName = rest.Configuration.RestKey,
                Password = rest.Configuration.RestSecret,
                Endpoint = new AmqpTcpEndpoint(rest.BaseUri),
                ClientProvidedName = "Restme MQ"
            };

            using var conn = RabbitMqConnectionFactory.CreateConnection();
            using var channel = conn.CreateModel();
            if (exchangeName.IsNotNullOrEmpty())
            {
                channel.ExchangeDeclare(exchangeName, exchangeType, isDurable, autoDelete);
            }
            else exchangeName = string.Empty;

            if (queueName.IsNotNullOrEmpty())
            {
                channel.QueueDeclare(queueName, isDurable, isExclusive, autoDelete);
            }
            else
            {
                queueName = channel.QueueDeclare().QueueName;
            }

            if (key.IsNullOrEmpty()) key = string.Empty;

            if (queueName.IsNotNullOrEmpty() && exchangeName.IsNotNullOrEmpty())
                channel.QueueBind(queueName, exchangeName, key);

            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (chn, args) =>
            {
                var result = StringUtils.GetStringFromStream(new MemoryStream(args.Body.ToArray()))
                    .JsonDeserialize<T>();
                if (queueTask == null || queueTask?.Invoke(result) == true)
                {
                    channel.BasicAck(args.DeliveryTag, true);
                }
                else
                {
                    channel.BasicNack(args.DeliveryTag, true, true);
                }
            };
            // prefetchCount = 1  ---> accept only one unack-ed message at a time
            channel.BasicQos(0, prefetchCount, false);
            channel.BasicConsume(queueName, false, consumer);

            if (deliverCompleteCondition == null) return;
            while (!deliverCompleteCondition.Invoke())
            {
                //no nothing, keep the loop await
            }
        }
        catch (Exception ex)
        {
            rest.LogError(ex?.Message, ex);
        }
    }
}