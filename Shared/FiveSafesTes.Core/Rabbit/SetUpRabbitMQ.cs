using Serilog;
using EasyNetQ.Management.Client;
using EasyNetQ.Management.Client.Model;

namespace FiveSafesTes.Core.Rabbit
{
    public static class SetUpRabbitMQ
    {
        public static async Task DoItSubmissionAsync(string hostname, string portNumber, string virtualHost, string username,
            string password)
        {
            Log.Information("{Function} Rabbit Conf: host={Host}", "DoItSubmissionAsync", hostname);
            Log.Information("{Function} Rabbit Conf: port={Port}", "DoItSubmissionAsync", portNumber);
            Log.Information("{Function} Rabbit Conf: vhost={VHost}", "DoItSubmissionAsync", virtualHost);

            try
            {

                var initial = new ManagementClient(hostname, username, password);

                // Create dev vhost as used by so many other things
                await initial.CreateVhostAsync(virtualHost);

                var vhost = await initial.GetVhostAsync(virtualHost);
                // Create main exchange
                await initial.CreateExchangeAsync(new ExchangeInfo(ExchangeConstants.Submission, "topic"), vhost);

                var exchange = await initial.GetExchangeAsync(vhost, ExchangeConstants.Submission);

                // create a queue users
                await initial.CreateQueueAsync(new QueueInfo(QueueConstants.ProcessSub), vhost);
                var subs = await initial.GetQueueAsync(vhost, QueueConstants.ProcessSub);
                await initial.CreateQueueBindingAsync(exchange, subs, new BindingInfo(RoutingConstants.ProcessSub));

                //await initial.CreateQueueAsync(new QueueInfo(QueueConstants.FetchExternalFile), vhost);
                //var fetchFile = await initial.GetQueueAsync(vhost, QueueConstants.FetchExternalFile);
                //await initial.CreateQueueBindingAsync(exchange, fetchFile, new BindingInfo(RoutingConstants.FetchExternalFile));



            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crash", "DoItSubmissionAsync");
            }
        }

        public static async Task DoItTreAsync(string hostname, string portNumber, string virtualHost, string username,
            string password)
        {
            Log.Information("{Function} Rabbit Conf: host={Host}", "DoItTreAsync", hostname);
            Log.Information("{Function} Rabbit Conf: port={Port}", "DoItTreAsync", portNumber);
            Log.Information("{Function} Rabbit Conf: vhost={VHost}", "DoItTreAsync", virtualHost);

            try
            {

                var initial = new ManagementClient(hostname, username, password);

                // Create dev vhost as used by so many other things
                await initial.CreateVhostAsync(virtualHost);

                var vhost = await initial.GetVhostAsync(virtualHost);
                // Create main exchange
                await initial.CreateExchangeAsync(new ExchangeInfo(ExchangeConstants.Tre, "topic"), vhost);

                var exchange = await initial.GetExchangeAsync(vhost, ExchangeConstants.Tre);

                // create a queue users
                await initial.CreateQueueAsync(new QueueInfo(QueueConstants.ProcessFinalOutput), vhost);
                var outputs = await initial.GetQueueAsync(vhost, QueueConstants.ProcessFinalOutput);
                await initial.CreateQueueBindingAsync(exchange, outputs, new BindingInfo(RoutingConstants.ProcessFinalOutput));

                



            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crash", "DoItTreAsync");
            }
        }
    }
}



