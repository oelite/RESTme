using System;
using RabbitMQ.Client;

namespace OElite;

public static class RestmeMessageQueueExtensions
{
    public static ConnectionFactory RabbitMqConnectionFactory { get; set; }

    public static void Queueme<T>(this Rest rest, string queueName, string key, bool isDurable = true, bool isExclusive = true,
        bool autoDelete = true)
    {
        if (rest?.CurrentMode != RestMode.RabbitMq) throw new NotSupportedException("Only RabbitMq mode is supported.");
        if ((rest.Configuration?.RestKey).IsNullOrEmpty() || (rest.Configuration?.RestSecret).IsNullOrEmpty())
            throw new OEliteException("invalid username and password for rabbitmq");

        if (RabbitMqConnectionFactory?.UserName != rest.Configuration.RestKey ||
            RabbitMqConnectionFactory?.Password != rest.Configuration.RestSecret ||
            RabbitMqConnectionFactory?.HostName != rest.ConnectionString)

            RabbitMqConnectionFactory = new ConnectionFactory
            {
                UserName = rest.Configuration.RestKey,
                Password = rest.Configuration.RestSecret,
                HostName = rest.ConnectionString,
                ClientProvidedName = "Restme MQ"
            };

        using var conn = RabbitMqConnectionFactory.CreateConnection();
        using var channel = conn.CreateModel();
        var channelDeclare = channel.QueueDeclare(queueName, isDurable, isExclusive, autoDelete);
        var ex = string.Empty;
        // channel.BasicPublish(ex,);
    }
}