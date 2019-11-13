﻿namespace Sitecore.Support.EDS.Providers.SparkPost.Dispatch
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Sitecore.Diagnostics;
    using Sitecore.EDS.Core.Dispatch;
    using Sitecore.EDS.Core.Exceptions;
    using Sitecore.EDS.Core.Net.Smtp;
    using Sitecore.EDS.Core.Reporting;
    using Sitecore.EDS.Providers.SparkPost.Configuration;
    using Sitecore.EDS.Providers.SparkPost.Dispatch;
    using System.Diagnostics;

    public class DispatchProvider : Sitecore.EDS.Providers.SparkPost.Dispatch.DispatchProvider
    {
        private readonly ConnectionPoolManager _connectionPoolManager;
        private readonly IConfigurationStore _configurationStore;
        private readonly string _returnPath;
        private readonly int _maxTries;
        private readonly int _delay;

        // fix 304755
        public DispatchProvider([NotNull] ConnectionPoolManager connectionPoolManager, [NotNull] IEnvironmentId environmentIdentifier, [NotNull] IConfigurationStore configurationStore, [NotNull] string returnPath, [NotNull]  string maxTries = "3", [NotNull]  string delay = "1000")
            : base(connectionPoolManager, environmentIdentifier, configurationStore, returnPath)
        {
            Assert.ArgumentNotNull(connectionPoolManager, "connectionPoolManager");

            _connectionPoolManager = connectionPoolManager;
            _configurationStore = configurationStore;
            _returnPath = returnPath;
            _maxTries = Int32.Parse(maxTries);
            _delay = Int32.Parse(delay);
        }

        public override async Task<bool> ValidateDispatchAsync()
        {
            // fix 304754
            for (var i = 0; i < _maxTries; i++)
            {

                var client = await _connectionPoolManager.GetSmtpConnectionAsync();

                // fix 158128
                if (!client.ValidateSmtpConnection().Result)
                {

                    if (i == _maxTries - 1)
                    {
                        return false;
                    }
                    Thread.Sleep(_delay);
                }
                else
                {
                    return true;
                }
            }
            return false;
        }

        protected override async Task<DispatchResult> SendEmailAsync(EmailMessage message)
        {
            for (var i = 0; i < _maxTries; i++)
            {
                message.ReturnPath = _returnPath;
                try
                {
                    var stopwatch = Stopwatch.StartNew();

                    var chilkatMessageTransport = new ChilkatMessageTransport(message);

                    stopwatch.Stop();
                    string parseMessageElapsed = stopwatch.ElapsedMilliseconds.ToString();
                    stopwatch.Restart();

                    var client = await _connectionPoolManager.GetSmtpConnectionAsync();

                    stopwatch.Stop();




                    var dispatchResult = await chilkatMessageTransport.SendAsync(client);
                    dispatchResult.Statistics.Add("ParseMessage", parseMessageElapsed);
                    dispatchResult.Statistics.Add("GetConnection", stopwatch.ElapsedMilliseconds.ToString());

                    return dispatchResult;
                }
                // fix 158128
                catch (TransportException)
                {

                    if (i == _maxTries - 1)
                    {
                        throw;
                    }
                    Thread.Sleep(_delay);
                }
            }
            return null;
        }
    }
}