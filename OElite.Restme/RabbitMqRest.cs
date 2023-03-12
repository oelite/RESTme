using System;
using RabbitMQ.Client;

namespace OElite;

public partial class Rest
{
    internal ConnectionFactory RabbitMqConnectionFactory { get; set; }
    internal IConnection RabbitMqConnection { get; set; }
    internal IModel RabbitMqChannel { get; set; }

    private void PrepareRabbitRestme()
    {
        if (CurrentMode != RestMode.RabbitMq) throw new NotSupportedException("Only RabbitMq mode is supported.");
        if ((Configuration?.RestKey).IsNullOrEmpty() || (Configuration?.RestSecret).IsNullOrEmpty())
            throw new OEliteException("invalid username and password for rabbitmq");

        if (Configuration != null && (RabbitMqConnectionFactory?.UserName != Configuration.RestKey ||
                                      RabbitMqConnectionFactory?.Password != Configuration.RestSecret ||
                                      RabbitMqConnectionFactory?.HostName != ConnectionString))

        {
            try
            {
                RabbitMqConnectionFactory = new ConnectionFactory
                {
                    UserName = Configuration.RestKey,
                    Password = Configuration.RestSecret,
                    Endpoint = new AmqpTcpEndpoint(BaseUri),
                    ClientProvidedName = "Restme MQ"
                };

                RabbitMqConnection = RabbitMqConnectionFactory.CreateConnection();
                RabbitMqChannel = RabbitMqConnection.CreateModel();
            }
            catch (Exception ex)
            {
                LogError(ex?.Message, ex);
            }

            if (RabbitMqConnection?.IsOpen == true)
            {
                Initialized = true;
            }
        }
    }
}