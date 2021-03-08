using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Security;
using System.Security.Authentication;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitDemo
{
    public class RabbitService : IRabbitService
    {
        private readonly ILogger<RabbitService> _logger;
        private readonly IConfiguration _config;
        private readonly ConnectionFactory _connectionFactory;

        private string _exchangeName;
        private string _queueName;
        private string _routingKey;
        public RabbitService(ILogger<RabbitService> log, IConfiguration config, [FromServices] ConnectionFactory connectionFactory)
        {
            _logger = log;
            _config = config;
            _connectionFactory = connectionFactory;
            SslOption opt = _connectionFactory.Ssl;
            if (opt != null && opt.Enabled)
            {
                opt.Version = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12;

                // Only needed if we want to disable certificate validations
                opt.AcceptablePolicyErrors = SslPolicyErrors.RemoteCertificateChainErrors |
                    SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateNotAvailable;
            }
        }

        /// <summary>
        /// Configure and start the service
        /// </summary>
        private void startService()
        {
            // Load the client settings
            loadSettings();

            try
            {
                _logger?.LogInformation("Starting listener for exchange: { _e } queue: { _q }", _exchangeName, _queueName);

                using (IConnection connection = _connectionFactory.CreateConnection())
                {
                    using (IModel channel = connection.CreateModel())
                    {
                        channel.ExchangeDeclare(_exchangeName, ExchangeType.Topic, false, true, null);

                        var queueDeclareOk = channel.QueueDeclare(_queueName, true, false, true, null);

                        // Setup the message handler
                        EventingBasicConsumer consumer = new EventingBasicConsumer(channel);
                        setEventHandlers(channel, consumer);

                        string consumerTag = channel.BasicConsume(consumer, _queueName, false, "Queue", true, false);

                        _logger?.LogInformation("Starting message processing for exchange: {_e} queue: {_q} key: {_k}", _exchangeName, _queueName, _routingKey);

                        channel.QueueBind(_queueName, _exchangeName, _routingKey);

                        Console.WriteLine("Service Started");
                        Console.ReadLine();

                        _logger?.LogInformation("Console exited");
                    }
                }
            }
            catch (Exception exception)
            {
                _logger?.LogError($"{DateTime.Now} > Exception: {exception.Message}");
            }
        }

        /// <summary>
        /// Load rabbit client settings
        /// </summary>
        private void loadSettings()
        {
            _exchangeName = _config.GetValue<string>("ExchangeName");
            if (string.IsNullOrEmpty(_exchangeName))
            {
                _exchangeName = "EXCHANGE3";
            }

            _queueName = _config.GetValue<string>("QueueName");
            if (string.IsNullOrEmpty(_queueName))
            {
                _queueName = "rabbit-queue";
            }

            _routingKey = _config.GetValue<string>("RoutingKey");
            if (string.IsNullOrEmpty(_routingKey))
            {
                _routingKey = "rabbit-test";
            }
        }

        /// <summary>
        /// Assign the event handlers
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="consumer"></param>
        private void setEventHandlers(IModel channel, EventingBasicConsumer consumer)
        {
            consumer.Received += (o, e) =>
            {
                string data = Encoding.ASCII.GetString(e.Body.ToArray());
                _logger?.LogInformation($"Received message: {data}");
                channel.BasicAck(e.DeliveryTag, true);
            };

            consumer.Shutdown += (o, e) =>
            {
                // Handle shutdown cleanup here
                _logger?.LogInformation("Shutting down worker");
                channel.QueueUnbind(_queueName, _exchangeName, _routingKey, null);

                _logger?.LogInformation("Listener stopped via shutdown event");
            };
        }

        /// <summary>
        /// Start the service
        /// </summary>
        public void Run()
        {
            try
            {
                _logger?.LogInformation($"Starting RabbitService");

                _logger?.LogInformation($"Process started");

                startService();

                _logger?.LogInformation($"Process Stopped");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex.ToString());
            }
        }
    }
}
