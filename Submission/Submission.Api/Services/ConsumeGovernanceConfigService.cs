using System.Text;
using EasyNetQ;
using EasyNetQ.Topology;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Services;
using Newtonsoft.Json;
using Serilog;
using Submission.Api.Repositories.DbContexts;
using Submission.Api.Services.Contract;

namespace Submission.Api.Services;

public class ConsumeGovernanceConfigService : BackgroundService
{
    private readonly IBus _bus;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    private const string ExchangeName = "dg-export-exchange";
    private const string QueueName = "dg-export-queue";
    private const string RoutingKey = "dg-export-routing";

    public ConsumeGovernanceConfigService(IBus bus,  IServiceScopeFactory serviceScopeFactory) 
    {
        _bus = bus;
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try 
        {
            Exchange exchange = await _bus.Advanced.ExchangeDeclareAsync(ExchangeName, x => { }, stoppingToken);
            Queue queue = await _bus.Advanced.QueueDeclareAsync(QueueName, x => x.WithArgument("x-queue-type", "classic"), stoppingToken);

            await _bus.Advanced.BindAsync(exchange, queue, RoutingKey, stoppingToken);
            _bus.Advanced.Consume<byte[]>(queue, Process);
        }
        catch (Exception ex) 
        {
            Log.Error(ex, "{Function} Failed to subscribe to {Queue}", "ConsumeGovernanceConfigService", QueueName);
        }
    }

    private void Process(IMessage<byte[]> message, MessageReceivedInfo info)
    {
        string body = Encoding.UTF8.GetString(message.Body);

        try
        {
            DirectorGovernanceConfig? config = JsonConvert.DeserializeObject<DirectorGovernanceConfig>(body);
            if (config == null)
            {
                Log.Error("{Function} Could not parse Director Governance message. Body: {Body}", "ConsumeGovernanceConfigService", body);
                return;
            }

            using IServiceScope scope = _serviceScopeFactory.CreateScope();
            IGovernanceImportService governanceImportService = scope.ServiceProvider.GetRequiredService<IGovernanceImportService>();

            governanceImportService.ApplyConfigAsync(config).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "{Function} Failed to process Director Governance message. Body: {Body}", "ConsumeGovernanceConfigService", body);
        }
    }
}
