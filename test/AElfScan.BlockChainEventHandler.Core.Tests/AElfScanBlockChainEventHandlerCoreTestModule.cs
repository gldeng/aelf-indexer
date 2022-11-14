using AElfScan.AElf.DTOs;
using AElfScan.AElf.Processors;
using AElfScan.Grains.Grain;
using AElfScan.Orleans.TestBase;
using AElfScan.Providers;
using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.EventBus;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Modularity;

namespace AElfScan;

[DependsOn(
    typeof(AElfScanBlockChainEventHandlerCoreModule),
    typeof(AbpEventBusModule),
    typeof(AElfScanOrleansTestBaseModule),
    typeof(AElfScanDomainModule))]
public class AElfScanBlockChainEventHandlerCoreTestModule:AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.Configure<PrimaryKeyOptions>(o =>
        {
            o.BlockGrainSwitchInterval = 100;
        });
        
        context.Services.AddTransient<IDistributedEventHandler<BlockChainDataEto>>(sp =>
            sp.GetService<BlockChainDataEventHandler>());
        context.Services.AddSingleton<IBlockGrainProvider>(sp => sp.GetService<TestBlockGrainProvider>());
    }
}