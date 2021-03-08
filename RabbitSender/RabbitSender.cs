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
    public class RabbitSender : IRabbitSender
    {
        private readonly ILogger<RabbitSender> _logger;
        private readonly IConfiguration _config;
        private readonly ConnectionFactory _connectionFactory;

        private string _exchangeName;
        private string _queueName;
        private string _routingKey;
        private int _interval;

        public RabbitSender(ILogger<RabbitSender> log, IConfiguration config, [FromServices] ConnectionFactory connectionFactory)
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
        /// Creates the queue.
        /// </summary>
        /// <param name="channel">The channel.</param>
        protected void createQueue(IModel channel)
        {

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

                        var queueDeclareOk = channel.QueueDeclare(queue: _queueName,
                                                durable: true,
                                                exclusive: false,
                                                autoDelete: true,
                                                arguments: null);
                       
                        while (true)
                        {
                            var message = $"This message sent on {DateTime.Now.ToString()}";
                            var body = Encoding.UTF8.GetBytes(message);
                            channel.BasicPublish(exchange: _exchangeName,
                                                 routingKey: _routingKey,
                                                 basicProperties: null,
                                                 body: body);

                            _logger?.LogInformation("Sending message: {_m}", message);

                            Thread.Sleep(_interval);
                        }

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

            _interval = _config.GetValue<int>("MessageInterval");
            if (_interval <= 0)
            {
                _interval = 5000;
            }

        }

        /// <summary>
        /// Assign the event handlers
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="consumer"></param>
        private void setEventHandlers(IModel channel, EventingBasicConsumer consumer)
        {
            consumer.Shutdown += (o, e) =>
            {
                // Handle shutdown cleanup here
                _logger?.LogInformation("Shutting down worker");
                channel.QueueUnbind(_queueName, _exchangeName, _routingKey, null);

                _logger?.LogInformation("Sender stopped via shutdown event");
            };
        }

        public bool Send(string message)
        {
            try
            {
                // TODO: Add insert logic here
                if (message != null && message != "")
                {
                    using (var connection = _connectionFactory.CreateConnection())
                    using (var channel = connection.CreateModel())
                    {
                        channel.ExchangeDeclare(_exchangeName, ExchangeType.Topic, false, true, null);

                        var body = Encoding.UTF8.GetBytes(message);
                        channel.BasicPublish(exchange: _exchangeName,
                                             routingKey: _routingKey,
                                             basicProperties: null,
                                             body: body);
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
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
