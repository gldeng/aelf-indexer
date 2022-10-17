using AElfScan.AElf.DTOs;
using AElfScan.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Volo.Abp;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.EventBus.RabbitMq;
using Volo.Abp.Modularity;

namespace AElfScan;

[DependsOn(typeof(AbpAutofacModule),
    typeof(AElfScanOrleansEventSourcingModule),
    typeof(AElfScanBlockChainEventHandlerCoreModule),
    typeof(AbpAspNetCoreSerilogModule),
    typeof(AbpEventBusRabbitMqModule))]
public class AElfScanBlockChainEventHandlerModule:AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        // context.Services.AddHostedService<AElfScanClusterClientHostedService>();
        
        Configure<OrleansClientOption>(configuration.GetSection("OrleansClient"));
        context.Services.AddSingleton<AElfScanClusterClientHostedService>();
        context.Services.AddSingleton<IHostedService>(sp => sp.GetService<AElfScanClusterClientHostedService>());
        context.Services.AddSingleton<IClusterClient>(sp => sp.GetService<AElfScanClusterClientHostedService>().OrleansClient);
        context.Services.AddSingleton<IGrainFactory>(sp =>
            sp.GetService<AElfScanClusterClientHostedService>().OrleansClient);
    }
    
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var _logger = context.ServiceProvider.GetService<ILogger<AElfScanBlockChainEventHandlerModule>>();
        var _distributedEventBus = context.ServiceProvider.GetService<IDistributedEventBus>();
        // _logger.LogInformation("Start publish simple block to RabbitMQ");
        // _distributedEventBus.PublishAsync(
        //     new BlockChainDataEto
        //     {
        //         ChainId = "AELF",
        //         Blocks = new List<BlockEto>()
        //         {
        //             new BlockEto()
        //             {
        //                 BlockHash = "19456c0236cac35c097bd46c44ae3492a4f4842d6cc19ff594785ec7ccea6460",
        //                 BlockNumber = 319,
        //                 BlockTime = DateTime.Now,
        //                 PreviousBlockHash = "b339518e8b0dfa83818e739e7abbb313f2c391c644c007eda155405687924c6e",
        //                 Signature = "7b0bd1525c6f41a9f0bd5136bcf1adb659be908c16fbba2a407e3e139d1d07f8484f6fbdb2da7ed78d776222c87f269350aef21bff3cab86474f7a3e5788545901",
        //                 SignerPubkey = "04bcd1c887cd0edbd4ccf8d9d2b3f72e72511aa6183199600313687ba6c583f13c3d6d716fa40df8604aaed0fcab31135fe3c2d45c009800c075254a3782b4c4db",
        //                 ExtraProperties = new Dictionary<string, string>()
        //                 {
        //                     ["Version"]="0",
        //                     ["Bloom"]="AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAAAAAAAACAAAAAEAAAAAAAAgAAIAABAAAAAAAAAABAAAAAAAAAAAAAAAAAEAAAAAAAAAIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAIAAAAAABAAAAAAAAAAAAAAAAAAAAAABAACAAAAAABAABAAAAAAAAAAAAgAAAAAAAAAAAAAAAIAAAAAAAAAAAAAgAAAAAAAAAAAAAAAAAAAAAAAEAABAAAAAAAAAAAAAAAAAAAAgAAAAAAAAAABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAAAAAAAAggAAgAAAAAAAABAAAAA==AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAAAAAAAACAAAAAEAAAAAAAAgAAIAABAAAAAAAAAABAAAAAAAAAAAAAAAAAEAAAAAAAAAIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAIAAAAAABAAAAAAAAAAAAAAAAAAAAAABAACAAAAAABAABAAAAAAAAAAAAgAAAAAAAAAAAAAAAIAAAAAAAAAAAAAgAAAAAAAAAAAAAAAAAAAAAAAEAABAAAAAAAAAAAAAAAAAAAAgAAAAAAAAAABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAAAAAAAAggAAgAAAAAAAABAAAAA==",
        //                     ["ExtraData"]="{ \"chainId\": 9992731, \"previousBlockHash\": \"b339518e8b0dfa83818e739e7abbb313f2c391c644c007eda155405687924c6e\", \"merkleTreeRootOfTransactions\": \"bd76965b3bdaac0643941491ad3de5b0a36cc94044671a627c219f212f2081a3\", \"merkleTreeRootOfWorldState\": \"286ee62e6df6e90cc0bb5ae652fce9f59921b628984ebe7386b8aab31bd7df9c\", \"bloom\": \"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAAAAAAAACAAAAAEAAAAAAAAgAAIAABAAAAAAAAAABAAAAAAAAAAAAAAAAAEAAAAAAAAAIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAIAAAAAABAAAAAAAAAAAAAAAAAAAAAABAACAAAAAABAABAAAAAAAAAAAAgAAAAAAAAAAAAAAAIAAAAAAAAAAAAAgAAAAAAAAAAAAAAAAAAAAAAAEAABAAAAAAAAAAAAAAAAAAAAgAAAAAAAAAABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAAAAAAAAggAAgAAAAAAAABAAAAA==\", \"height\": \"3704\", \"extraData\": { \"CrossChain\": \"\", \"Consensus\": \"CkEEvNHIh80O29TM+NnSs/cuclEaphgxmWADE2h7psWD8Tw9bXFvpA34YEqu0PyrMRNf48LUXACYAMB1JUo3grTE2xKHBAiDAhL7AwqCATA0YmNkMWM4ODdjZDBlZGJkNGNjZjhkOWQyYjNmNzJlNzI1MTFhYTYxODMxOTk2MDAzMTM2ODdiYTZjNTgzZjEzYzNkNmQ3MTZmYTQwZGY4NjA0YWFlZDBmY2FiMzExMzVmZTNjMmQ0NWMwMDk4MDBjMDc1MjU0YTM3ODJiNGM0ZGIS8wIIARABIiIKINtQYJbFJW9txThi/mFYRL+dYW8Imo0YLWKQBD7+gCt2KiIKIByQ72GTQpFvM4ckvuqi38XzXdV9CdTp9G6vpHsGvmYROPccSoIBMDRiY2QxYzg4N2NkMGVkYmQ0Y2NmOGQ5ZDJiM2Y3MmU3MjUxMWFhNjE4MzE5OTYwMDMxMzY4N2JhNmM1ODNmMTNjM2Q2ZDcxNmZhNDBkZjg2MDRhYWVkMGZjYWIzMTEzNWZlM2MyZDQ1YzAwOTgwMGMwNzUyNTRhMzc4MmI0YzRkYlIiCiDlfPSfe6qmev4uOnB5ZynIBQvpA3dUB7mOH5WE94Pdf1gBYAFqBgjY9aSZBmoMCNj1pJkGENi/4a8BagwI2PWkmQYQuO/ewAJqDAjY9aSZBhDQ8fXQA2oLCNn1pJkGEOi60lxqDAjZ9aSZBhDQpOXWAWoMCNn1pJkGENilmOMCagsI2vWkmQYQgNybTmoGCNz1pJkGgAEJiAH4HFDc9aSZBg==\", \"SystemTransactionCount\": \"CAI=\" }, \"time\": \"2022-09-20T04:00:28Z\", \"merkleTreeRootOfTransactionStatus\": \"ca6230f7cbc4d38400c39ae4ca35f9182f1b07b19ece80ecb972c6afbad71f62\", \"signerPubkey\": \"BLzRyIfNDtvUzPjZ0rP3LnJRGqYYMZlgAxNoe6bFg/E8PW1xb6QN+GBKrtD8qzETX+PC1FwAmADAdSVKN4K0xNs=\", \"signature\": \"ewvRUlxvQanwvVE2vPGttlm+kIwW+7oqQH4+E50dB/hIT2+9stp+1413YiLIfyaTUK7yG/88q4ZHT3o+V4hUWQE=\" }",
        //                     ["MerkleTreeRootOfTransactions"]="bd76965b3bdaac0643941491ad3de5b0a36cc94044671a627c219f212f2081a3",
        //                     ["MerkleTreeRootOfWorldState"]="286ee62e6df6e90cc0bb5ae652fce9f59921b628984ebe7386b8aab31bd7df9c"
        //                 }
        //             }
        //         }
        //     }
        //     );
        
        // _logger.LogInformation("Start publish simple block2 to RabbitMQ");
        // _distributedEventBus.PublishAsync(
        //     new BlockChainDataEto
        //     {
        //         ChainId = "AELF",
        //         Blocks = new List<BlockEto>()
        //         {
        //             new BlockEto()
        //             {
        //                 BlockHash = "3de406161fb47785641612e953973de8a018003065633ce52973378f31240456",
        //                 BlockNumber = 320,
        //                 BlockTime = DateTime.Now,
        //                 PreviousBlockHash = "19456c0236cac35c097bd46c44ae3492a4f4842d6cc19ff594785ec7ccea6460",
        //                 Signature = "0b1eec144cdf8575f3004352811123a408c505d8f20084bad27bb2aa16cf797a68078fb06a4706207874b0328096d0e03cde427bdcc1605519b2ec277853cb2f01",
        //                 SignerPubkey = "04bcd1c887cd0edbd4ccf8d9d2b3f72e72511aa6183199600313687ba6c583f13c3d6d716fa40df8604aaed0fcab31135fe3c2d45c009800c075254a3782b4c4db",
        //                 ExtraProperties = new Dictionary<string, string>()
        //                 {
        //                     ["Version"]="0",
        //                     ["Bloom"]="AAAAAAAAAAAAAAAAAAAAAAEBAAAAAAAAAAAAAAEEAACAAAAAEAAAAAAAAgAAAAAAAAQAAAAAAABAAAAAAAAAAAAAAAAAAAgAQAAAAAAEAAAAAAAAAAAQAAAAAAAAAAAAAAAAAAAAIBAAAAABAAAAAAAAAAAAAAAAAAAAAAAAACAAAAAAAAABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAgAAAAAAAAAAAAAAAAAAAAAAAEAABAAAAAAAAAAAAAAAAAAAAgAAAAAEAAAABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAgAAgCAAAAAAAAAAAAA==",
        //                     ["ExtraData"]="{ \"chainId\": 9992731, \"previousBlockHash\": \"19456c0236cac35c097bd46c44ae3492a4f4842d6cc19ff594785ec7ccea6460\", \"merkleTreeRootOfTransactions\": \"92656d4668632b738f4bb71a22903d3089287a6c213b45fc4633366f2af929cf\", \"merkleTreeRootOfWorldState\": \"97dad6a116dc008692c351066f15dd0f9ad11f7de21fd2ca785b18bc64ad9467\", \"bloom\": \"AAAAAAAAAAAAAAAAAAAAAAEBAAAAAAAAAAAAAAEEAACAAAAAEAAAAAAAAgAAAAAAAAQAAAAAAABAAAAAAAAAAAAAAAAAAAgAQAAAAAAEAAAAAAAAAAAQAAAAAAAAAAAAAAAAAAAAIBAAAAABAAAAAAAAAAAAAAAAAAAAAAAAACAAAAAAAAABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAgAAAAAAAAAAAAAAAAAAAAAAAEAABAAAAAAAAAAAAAAAAAAAAgAAAAAEAAAABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAgAAgCAAAAAAAAAAAAA==\", \"height\": \"3695\", \"extraData\": { \"CrossChain\": \"\", \"Consensus\": \"CkEEvNHIh80O29TM+NnSs/cuclEaphgxmWADE2h7psWD8Tw9bXFvpA34YEqu0PyrMRNf48LUXACYAMB1JUo3grTE2xKIBAiCAhL8AwqCATA0YmNkMWM4ODdjZDBlZGJkNGNjZjhkOWQyYjNmNzJlNzI1MTFhYTYxODMxOTk2MDAzMTM2ODdiYTZjNTgzZjEzYzNkNmQ3MTZmYTQwZGY4NjA0YWFlZDBmY2FiMzExMzVmZTNjMmQ0NWMwMDk4MDBjMDc1MjU0YTM3ODJiNGM0ZGIS9AIIARABIiIKIIgtjhKXmcTFK25PS8n0NY7+sf4aT8uEnW2MuWIyrcx5KiIKIEzHge8PatqdOJiQ2uAQOc32HTIRrfN+t2Rm9OEqL7pkOO4cSoIBMDRiY2QxYzg4N2NkMGVkYmQ0Y2NmOGQ5ZDJiM2Y3MmU3MjUxMWFhNjE4MzE5OTYwMDMxMzY4N2JhNmM1ODNmMTNjM2Q2ZDcxNmZhNDBkZjg2MDRhYWVkMGZjYWIzMTEzNWZlM2MyZDQ1YzAwOTgwMGMwNzUyNTRhMzc4MmI0YzRkYlIiCiBYeXTie7SLk0wShRB9HOOz9yiRa23Wd3gPvzT4+IhHCVgBYAFqBgjA9aSZBmoMCMD1pJkGEJiS35IBagwIwPWkmQYQqMuu+AFqDAjA9aSZBhDYgoneAmoMCMD1pJkGEJCJj80DagsIwfWkmQYQ8IyYXWoMCMH1pJkGEIiV49sBagwIwfWkmQYQgI+awwJqBgjE9aSZBoABCYgB7xxQxPWkmQY=\", \"SystemTransactionCount\": \"CAI=\" }, \"time\": \"2022-09-20T04:00:04Z\", \"merkleTreeRootOfTransactionStatus\": \"2dd62bb8acffbec4f5a511d4027a3ca075af4ae505fd8349a56d3e36a7fa7d50\", \"signerPubkey\": \"BLzRyIfNDtvUzPjZ0rP3LnJRGqYYMZlgAxNoe6bFg/E8PW1xb6QN+GBKrtD8qzETX+PC1FwAmADAdSVKN4K0xNs=\", \"signature\": \"Cx7sFEzfhXXzAENSgREjpAjFBdjyAIS60nuyqhbPeXpoB4+wakcGIHh0sDKAltDgPN5Ce9zBYFUZsuwneFPLLwE=\" }",
        //                     ["MerkleTreeRootOfTransactions"]="92656d4668632b738f4bb71a22903d3089287a6c213b45fc4633366f2af929cf",
        //                     ["MerkleTreeRootOfWorldState"]="97dad6a116dc008692c351066f15dd0f9ad11f7de21fd2ca785b18bc64ad9467"
        //                 },
        //                 Transactions = new List<TransactionEto>(){}
        //             }
        //         }
        //     }
        // );
        //
        // _logger.LogInformation("Start publish MiningInfoUpdate Event block to RabbitMQ");
        // _distributedEventBus.PublishAsync(
        //     new BlockChainDataEto
        //     {
        //         ChainId = "AELF",
        //         Blocks = new List<BlockEto>()
        //         {
        //             new BlockEto()
        //             {
        //                 BlockHash = "156ff3721154b30e08bdbd3aab85071c2bc8744dc3249471100c21268bb4641a",
        //                 BlockNumber = 334,
        //                 BlockTime = DateTime.Now,
        //                 PreviousBlockHash = "3de406161fb47785641612e953973de8a018003065633ce52973378f31240456",
        //                 Signature = "2c4117170f79e4f4c01976265b9782ebe840735b7ec25fb82fbce6756b34218e5996a5c6650be7b4594397f999e222beb45c0ed11412b67871857968871ce03f01",
        //                 SignerPubkey = "04bcd1c887cd0edbd4ccf8d9d2b3f72e72511aa6183199600313687ba6c583f13c3d6d716fa40df8604aaed0fcab31135fe3c2d45c009800c075254a3782b4c4db",
        //                 ExtraProperties = new Dictionary<string, string>()
        //                 {
        //                     ["Version"]="0",
        //                     ["Bloom"]="AAAAAAABAAAAAAAAAAAAAABAAAAAAAAAAAAAAAAAAACAAAAAACAAAAAAAAAAAAAAAAAAAAAAACAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAoAAAAAAAAAAAAAAAAAAAAAACAAAAIAAAAAABAAAAAAAAAAAAAAAAAAAAAAAAACACAAAAAAABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAgAAAAAAAAAAAAAAAAAAAAAAAEAAAACAAAAAAAAAAAAAAAAAAgAAAAAAAAAAAgAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAAAAAAAAAAAAAAgAAAAAAAAAAAAAA==",
        //                     ["ExtraData"]="{ \"chainId\": 9992731, \"previousBlockHash\": \"3de406161fb47785641612e953973de8a018003065633ce52973378f31240456\", \"merkleTreeRootOfTransactions\": \"59d46fbb95f16efef1ee5f6be2706e57c358f7a46f54ffcf5c70e4b1eaf0662b\", \"merkleTreeRootOfWorldState\": \"af75c7a95ca33684c91b026c002c27622389c7d597e1d8fc744c34f07eff7ad1\", \"bloom\": \"AAAAAAABAAAAAAAAAAAAAABAAAAAAAAAAAAAAAAAAACAAAAAACAAAAAAAAAAAAAAAAAAAAAAACAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAoAAAAAAAAAAAAAAAAAAAAAACAAAAIAAAAAABAAAAAAAAAAAAAAAAAAAAAAAAACACAAAAAAABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAgAAAAAAAAAAAAAAAAAAAAAAAEAAAACAAAAAAAAAAAAAAAAAAgAAAAAAAAAAAgAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAAAAAAAAAAAAAAgAAAAAAAAAAAAAA==\", \"height\": \"4400\", \"extraData\": { \"CrossChain\": \"\", \"Consensus\": \"CkEEvNHIh80O29TM+NnSs/cuclEaphgxmWADE2h7psWD8Tw9bXFvpA34YEqu0PyrMRNf48LUXACYAMB1JUo3grTE2xKgAwiuAhKUAwqCATA0YmNkMWM4ODdjZDBlZGJkNGNjZjhkOWQyYjNmNzJlNzI1MTFhYTYxODMxOTk2MDAzMTM2ODdiYTZjNTgzZjEzYzNkNmQ3MTZmYTQwZGY4NjA0YWFlZDBmY2FiMzExMzVmZTNjMmQ0NWMwMDk4MDBjMDc1MjU0YTM3ODJiNGM0ZGISjAI4ryJKggEwNGJjZDFjODg3Y2QwZWRiZDRjY2Y4ZDlkMmIzZjcyZTcyNTExYWE2MTgzMTk5NjAwMzEzNjg3YmE2YzU4M2YxM2MzZDZkNzE2ZmE0MGRmODYwNGFhZWQwZmNhYjMxMTM1ZmUzYzJkNDVjMDA5ODAwYzA3NTI1NGEzNzgyYjRjNGRiagYIwNqlmQZqCwjA2qWZBhDojsQuagsIwNqlmQYQwMfvV2oMCMDapZkGELCv04QBagwIwNqlmQYQ4OSvsgFqDAjA2qWZBhCAqr3hAWoMCMDapZkGEJjI040CagwIwNqlmQYQuIyjtQJqBgjE2qWZBmoLCMTapZkGEODO+C2AAQqIAa8iUMTapZkGGAQ=\", \"SystemTransactionCount\": \"CAI=\" }, \"time\": \"2022-09-20T07:35:32.096348Z\", \"merkleTreeRootOfTransactionStatus\": \"65650c2ade99b6cd7d1f52c3c69f81f7b151fa1ed23dece36bf8f11d89c7000d\", \"signerPubkey\": \"BLzRyIfNDtvUzPjZ0rP3LnJRGqYYMZlgAxNoe6bFg/E8PW1xb6QN+GBKrtD8qzETX+PC1FwAmADAdSVKN4K0xNs=\", \"signature\": \"LEEXFw955PTAGXYmW5eC6+hAc1t+wl+4L7zmdWs0IY5ZlqXGZQvntFlDl/mZ4iK+tFwO0RQStnhxhXlohxzgPwE=\" }",
        //                     ["MerkleTreeRootOfTransactions"]="59d46fbb95f16efef1ee5f6be2706e57c358f7a46f54ffcf5c70e4b1eaf0662b",
        //                     ["MerkleTreeRootOfWorldState"]="af75c7a95ca33684c91b026c002c27622389c7d597e1d8fc744c34f07eff7ad1"
        //                 },
        //                 Transactions = new List<TransactionEto>()
        //                 {
        //                     new TransactionEto()
        //                     {
        //                         TransactionId = "a27227c6cc1de52de786ef5d8ddd60d905f72b9a914ac8c77cc4926b627474a3",
        //                         From = "2pL7foxBhMC1RVZMUEtkvYK4pWWaiLHBAQcXFdzfD5oZjYSr3e",
        //                         To="pGa4e5hNGsgkfjEGm72TEvbF7aRDqKBd4LuXtab4ucMbXLcgJ",
        //                         MethodName = "UpdateTinyBlockInformation",
        //                         Params = "CMTapZkGEgsIxNqlmQYQ4M74LRivIg==",
        //                         Signature = "CR4hFZmuquZY8YF8fnlTOFa8CmyzzleQBe+ALXgfBt4AYz0/Ez21E00WXx3knvHVzWolQr0IXfHb5CJZ0OQtEgE=",
        //                         Status = 3,
        //                         ExtraProperties = new Dictionary<string, string>()
        //                         {
        //                             ["Version"]="0",
        //                             ["RefBlockNumber"]="4399",
        //                             ["RefBlockPrefix"]="3de40616",
        //                             ["Bloom"]="AAAAAAABAAAAAAAAAAAAAABAAAAAAAAAAAAAAAAAAACAAAAAACAAAAAAAAAAAAAAAAAAAAAAACAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAoAAAAAAAAAAAAAAAAAAAAAACAAAAIAAAAAABAAAAAAAAAAAAAAAAAAAAAAAAACACAAAAAAABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAgAAAAAAAAAAAAAAAAAAAAAAAEAAAACAAAAAAAAAAAAAAAAAAgAAAAAAAAAAAgAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAAAAAAAAAAAAAAgAAAAAAAAAAAAAA==",
        //                             ["ReturnValue"]="",
        //                             ["Error"]=""
        //                         },
        //                         LogEvents = new List<LogEventEto>()
        //                         {
        //                             new LogEventEto()
        //                             {
        //                                 ContractAddress = "pGa4e5hNGsgkfjEGm72TEvbF7aRDqKBd4LuXtab4ucMbXLcgJ",
        //                                 EventName = "MiningInformationUpdated",
        //                                 Index = 0,
        //                                 ExtraProperties = new Dictionary<string, string>()
        //                                 {
        //                                     ["Indexed"]="[ \"CoIBMDRiY2QxYzg4N2NkMGVkYmQ0Y2NmOGQ5ZDJiM2Y3MmU3MjUxMWFhNjE4MzE5OTYwMDMxMzY4N2JhNmM1ODNmMTNjM2Q2ZDcxNmZhNDBkZjg2MDRhYWVkMGZjYWIzMTEzNWZlM2MyZDQ1YzAwOTgwMGMwNzUyNTRhMzc4MmI0YzRkYg==\", \"EgsIxNqlmQYQ4M74LQ==\", \"GhpVcGRhdGVUaW55QmxvY2tJbmZvcm1hdGlvbg==\", \"ILAi\", \"KiIKID3kBhYftHeFZBYS6VOXPeigGAAwZWM85SlzN48xJARW\" ]",
        //                                     ["NonIndexed"]=""
        //                                 }
        //                             }
        //                         }
        //                     },
        //                     new TransactionEto()
        //                     {
        //                         TransactionId = "77d295acadb1b1c92f98fcdf19b4cf24386a20711be69a683c837535a59355d3",
        //                         From = "2pL7foxBhMC1RVZMUEtkvYK4pWWaiLHBAQcXFdzfD5oZjYSr3e",
        //                         To = "JRmBduh4nXWi1aXgdUsj5gJrzeZb2LxmrAbf7W99faZSvoAaE",
        //                         MethodName = "DonateResourceToken",
        //                         Params = "EiIKID3kBhYftHeFZBYS6VOXPeigGAAwZWM85SlzN48xJARWGK8i",
        //                         Signature = "fde53OUTiv1D+NzYOxgjEV9aal3bFfrJmZwUG0kDcu93HVNf9npKSnowvBH9n9B8u4erVJExUlzx75AGvBWfxgE=",
        //                         Status = 3,
        //                         ExtraProperties = new Dictionary<string, string>()
        //                         {
        //                             ["Version"]="0",
        //                             ["RefBlockNumber"]="4399",
        //                             ["RefBlockPrefix"]="3de40616",
        //                             ["Bloom"]="",
        //                             ["ReturnValue"]="",
        //                             ["Error"]=""
        //                         },
        //                         LogEvents = new List<LogEventEto>(){}
        //                     }
        //                 }
        //             }
        //         }
        //     }
        //     );
        //
        //
        // _logger.LogInformation("Start publish LibFound Event to Rabbitmq");
        // var lastBlock = new BlockChainDataEto
        // {
        //     ChainId = "AELF",
        //     Blocks = new List<BlockEto>()
        //     {
        //         new BlockEto()
        //         {
        //             BlockHash = "fc4192b920415cae740c808e064696890b031944bc2756e18b99619aae50740e",
        //             BlockNumber = 336,
        //             BlockTime = DateTime.Now,
        //             PreviousBlockHash = "156ff3721154b30e08bdbd3aab85071c2bc8744dc3249471100c21268bb4641a",
        //             Signature =
        //                 "a41ce21264d62e141537fcdd0597c496969c8ce73ec51dcf4b48411fb66a6134759d2b7cf12c19aa916c72deb8aeba4f20cfbf9deb1a915a52c3ed168c5aa83900",
        //             SignerPubkey =
        //                 "04bcd1c887cd0edbd4ccf8d9d2b3f72e72511aa6183199600313687ba6c583f13c3d6d716fa40df8604aaed0fcab31135fe3c2d45c009800c075254a3782b4c4db",
        //             ExtraProperties = new Dictionary<string, string>()
        //             {
        //                 ["Version"] = "0",
        //                 ["Bloom"] =
        //                     "AAAAAAAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAAAAAEAAAAAAAAgAAABAAAAAAAAAAAABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAgAAAAAAAAAABAAAAAAAAAAAAAAIQAAAAABAAAAAgAAAAAAAAAAAAAAAAABACAAAAAAAAABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAIgAAAAAAAAAAAAAAAAAAAAAAAEAABAAAAAAAAAAAAAAAAAAAAgAAAAgAAAAABAAAAAAAAAAAAAACAAAAAAAAAAAAAAAAAAAAAAAAAAAgAAgAAAAAAAAAAAAAA==AAAAAAAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAAAAAEAAAAAAAAgAAABAAAAAAAAAAAABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAgAAAAAAAAAABAAAAAAAAAAAAAAIQAAAAABAAAAAgAAAAAAAAAAAAAAAAABACAAAAAAAAABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAIgAAAAAAAAAAAAAAAAAAAAAAAEAABAAAAAAAAAAAAAAAAAAAAgAAAAgAAAAABAAAAAAAAAAAAAACAAAAAAAAAAAAAAAAAAAAAAAAAAAgAAgAAAAAAAAAAAAAA==",
        //                 ["ExtraData"] =
        //                     "{ \"chainId\": 9992731, \"previousBlockHash\": \"156ff3721154b30e08bdbd3aab85071c2bc8744dc3249471100c21268bb4641a\", \"merkleTreeRootOfTransactions\": \"2818daae4558ecfcf143d2b4e46fcf55edcc2451faa310cc3973237d7374766a\", \"merkleTreeRootOfWorldState\": \"58ae5c52a15f38786b29ded30a70d96e085418c813f01c4398f8f735100a2172\", \"bloom\": \"AAAAAAAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAAAAAEAAAAAAAAgAAABAAAAAAAAAAAABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAgAAAAAAAAAABAAAAAAAAAAAAAAIQAAAAABAAAAAgAAAAAAAAAAAAAAAAABACAAAAAAAAABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAIgAAAAAAAAAAAAAAAAAAAAAAAEAABAAAAAAAAAAAAAAAAAAAAgAAAAgAAAAABAAAAAAAAAAAAAACAAAAAAAAAAAAAAAAAAAAAAAAAAAgAAgAAAAAAAAAAAAAA==\", \"height\": \"336\", \"extraData\": { \"CrossChain\": \"\", \"Consensus\": \"CkEEvNHIh80O29TM+NnSs/cuclEaphgxmWADE2h7psWD8Tw9bXFvpA34YEqu0PyrMRNf48LUXACYAMB1JUo3grTE2xKGBAgZEvsDCoIBMDRiY2QxYzg4N2NkMGVkYmQ0Y2NmOGQ5ZDJiM2Y3MmU3MjUxMWFhNjE4MzE5OTYwMDMxMzY4N2JhNmM1ODNmMTNjM2Q2ZDcxNmZhNDBkZjg2MDRhYWVkMGZjYWIzMTEzNWZlM2MyZDQ1YzAwOTgwMGMwNzUyNTRhMzc4MmI0YzRkYhLzAggBEAEiIgog2cHgeGZOkxJw2X1PiE2hHXDx06nHQ7rzin6nB+bq46QqIgog1lSTW/6zNSoxlnfDSmEufuqo5f6H64PV90/n0tib0Kw4zwJKggEwNGJjZDFjODg3Y2QwZWRiZDRjY2Y4ZDlkMmIzZjcyZTcyNTExYWE2MTgzMTk5NjAwMzEzNjg3YmE2YzU4M2YxM2MzZDZkNzE2ZmE0MGRmODYwNGFhZWQwZmNhYjMxMTM1ZmUzYzJkNDVjMDA5ODAwYzA3NTI1NGEzNzgyYjRjNGRiUiIKIPGQ8ga2zO3CXXtZkjlMma6CI7VSQFA7ZMYrIU6zGT/uWAFgAWoGCJjI3JgGagsImMjcmAYQ2ILvQmoLCJjI3JgGEICn8H1qDAiYyNyYBhD42Zu5AWoMCJjI3JgGENDErfMBagwImMjcmAYQ+NjarwJqDAiYyNyYBhCwr4zsAmoMCJjI3JgGEKDNirQDagYInMjcmAaAAQmIAdACUJzI3JgG\", \"SystemTransactionCount\": \"CAI=\" }, \"time\": \"2022-09-06T10:42:36Z\", \"merkleTreeRootOfTransactionStatus\": \"317637d50870824e2186aef029745a92b59a598e9cb186d13d7e8b0478342581\", \"signerPubkey\": \"BLzRyIfNDtvUzPjZ0rP3LnJRGqYYMZlgAxNoe6bFg/E8PW1xb6QN+GBKrtD8qzETX+PC1FwAmADAdSVKN4K0xNs=\", \"signature\": \"pBziEmTWLhQVN/zdBZfElpacjOc+xR3PS0hBH7ZqYTR1nSt88SwZqpFsct64rrpPIM+/nesakVpSw+0WjFqoOQA=\" }",
        //                 ["MerkleTreeRootOfTransactions"] =
        //                     "2818daae4558ecfcf143d2b4e46fcf55edcc2451faa310cc3973237d7374766a",
        //                 ["MerkleTreeRootOfWorldState"] =
        //                     "58ae5c52a15f38786b29ded30a70d96e085418c813f01c4398f8f735100a2172"
        //             },
        //             Transactions = new List<TransactionEto>()
        //             {
        //                 new TransactionEto()
        //                 {
        //                     TransactionId = "1ce2f46ebfe64f59bef89af5b3f44efb37e4b2ef9e28b28803e960c4a4a400b6",
        //                     From = "2pL7foxBhMC1RVZMUEtkvYK4pWWaiLHBAQcXFdzfD5oZjYSr3e",
        //                     To = "pGa4e5hNGsgkfjEGm72TEvbF7aRDqKBd4LuXtab4ucMbXLcgJ",
        //                     MethodName = "UpdateValue",
        //                     Params =
        //                         "CiIKINnB4HhmTpMScNl9T4hNoR1w8dOpx0O684p+pwfm6uOkEiIKINZUk1v+szUqMZZ3w0phLn7qqOX+h+uD1fdP59LYm9CsIiIKIPGQ8ga2zO3CXXtZkjlMma6CI7VSQFA7ZMYrIU6zGT/uKgYInMjcmAYwAVDPAlqpAQqCATA0YmNkMWM4ODdjZDBlZGJkNGNjZjhkOWQyYjNmNzJlNzI1MTFhYTYxODMxOTk2MDAzMTM2ODdiYTZjNTgzZjEzYzNkNmQ3MTZmYTQwZGY4NjA0YWFlZDBmY2FiMzExMzVmZTNjMmQ0NWMwMDk4MDBjMDc1MjU0YTM3ODJiNGM0ZGISIgog8ZDyBrbM7cJde1mSOUyZroIjtVJAUDtkxishTrMZP+5g0AI=",
        //                     Signature =
        //                         "1KblGpvuuo+HSDdh0OhRq/vg3Ts4HoqcIwBeni/356pdEbgnnR2yqbpgvzNs+oNeBb4Ux2kE1XY9lk+p60LfWgA=",
        //                     Status = 3,
        //                     ExtraProperties = new Dictionary<string, string>()
        //                     {
        //                         ["Version"] = "0",
        //                         ["RefBlockNumber"] = "335",
        //                         ["RefBlockPrefix"] = "156ff372",
        //                         ["Bloom"] =
        //                             "AAAAAAAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAAAAAEAAAAAAAAgAAABAAAAAAAAAAAABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAgAAAAAAAAAABAAAAAAAAAAAAAAIQAAAAABAAAAAgAAAAAAAAAAAAAAAAABACAAAAAAAAABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAIgAAAAAAAAAAAAAAAAAAAAAAAEAABAAAAAAAAAAAAAAAAAAAAgAAAAgAAAAABAAAAAAAAAAAAAACAAAAAAAAAAAAAAAAAAAAAAAAAAAgAAgAAAAAAAAAAAAAA==",
        //                         ["ReturnValue"] = "",
        //                         ["Error"] = ""
        //                     },
        //                     LogEvents = new List<LogEventEto>()
        //                     {
        //                         new LogEventEto()
        //                         {
        //                             ContractAddress = "pGa4e5hNGsgkfjEGm72TEvbF7aRDqKBd4LuXtab4ucMbXLcgJ",
        //                             EventName = "IrreversibleBlockFound",
        //                             Index = 0,
        //                             ExtraProperties = new Dictionary<string, string>()
        //                             {
        //                                 ["Indexed"] = "[ \"CMAC\" ]",
        //                                 ["NonIndexed"] = ""
        //                             }
        //                         },
        //                         new LogEventEto()
        //                         {
        //                             ContractAddress = "pGa4e5hNGsgkfjEGm72TEvbF7aRDqKBd4LuXtab4ucMbXLcgJ",
        //                             EventName = "MiningInformationUpdated",
        //                             Index = 1,
        //                             ExtraProperties = new Dictionary<string, string>()
        //                             {
        //                                 ["Indexed"] =
        //                                     "[ \"CoIBMDRiY2QxYzg4N2NkMGVkYmQ0Y2NmOGQ5ZDJiM2Y3MmU3MjUxMWFhNjE4MzE5OTYwMDMxMzY4N2JhNmM1ODNmMTNjM2Q2ZDcxNmZhNDBkZjg2MDRhYWVkMGZjYWIzMTEzNWZlM2MyZDQ1YzAwOTgwMGMwNzUyNTRhMzc4MmI0YzRkYg==\", \"EgYInMjcmAY=\", \"GgtVcGRhdGVWYWx1ZQ==\", \"INAC\", \"KiIKIBVv83IRVLMOCL29OquFBxwryHRNwySUcRAMISaLtGQa\" ]",
        //                                 ["NonIndexed"] = ""
        //                             }
        //                         }
        //                     }
        //                 },
        //                 new TransactionEto()
        //                 {
        //                     TransactionId = "5ba449c61035cf8fea16604cf333600b28cebc63f03557634c9531d8229d60c3",
        //                     From = "2pL7foxBhMC1RVZMUEtkvYK4pWWaiLHBAQcXFdzfD5oZjYSr3e",
        //                     To = "JRmBduh4nXWi1aXgdUsj5gJrzeZb2LxmrAbf7W99faZSvoAaE",
        //                     MethodName = "DonateResourceToken",
        //                     Params = "EiIKIBVv83IRVLMOCL29OquFBxwryHRNwySUcRAMISaLtGQaGM8C",
        //                     Signature =
        //                         "3USrQq3C0VJ28pg1SEA4DJ3vH3suBiW5oFIp53kW7989vdrbgWhCW82qD4ovb6Q9gZOJsqgu388++MMk/3cHDgE=",
        //                     Status = 3,
        //                     ExtraProperties = new Dictionary<string, string>()
        //                     {
        //                         ["Version"] = "0",
        //                         ["RefBlockNumber"] = "335",
        //                         ["RefBlockPrefix"] = "156ff372",
        //                         ["Bloom"] = "",
        //                         ["ReturnValue"] = "",
        //                         ["Error"] = ""
        //                     },
        //                     LogEvents = new List<LogEventEto>() { }
        //                 }
        //             }
        //         }
        //     }
        // };
        // _distributedEventBus.PublishAsync(lastBlock);
        // _logger.LogInformation("Test Block Event is already published");
    }
}
