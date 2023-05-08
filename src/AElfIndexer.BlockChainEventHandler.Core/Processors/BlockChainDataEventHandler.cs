using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AElf.Contracts.Consensus.AEDPoS;
using AElfIndexer.DTOs;
using AElfIndexer.Etos;
using AElfIndexer.Grains.EventData;
using AElfIndexer.Grains.Grain.Blocks;
using AElfIndexer.Providers;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Orleans;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;
using Volo.Abp.Threading;

namespace AElfIndexer.Processors;

public class BlockChainDataEventHandler : IDistributedEventHandler<BlockChainDataEto>, ITransientDependency
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<BlockChainDataEventHandler> _logger;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly IObjectMapper _objectMapper;
    private readonly IBlockGrainProvider _blockGrainProvider;
    private readonly BlockChainEventHandlerOptions _blockChainEventHandlerOptions;

    public BlockChainDataEventHandler(
        IClusterClient clusterClient,
        ILogger<BlockChainDataEventHandler> logger,
        IObjectMapper objectMapper,
        IBlockGrainProvider blockGrainProvider,
        IOptionsSnapshot<BlockChainEventHandlerOptions> blockChainEventHandlerOptions,
        IDistributedEventBus distributedEventBus)
    {
        _clusterClient = clusterClient;
        _logger = logger;
        _distributedEventBus = distributedEventBus;
        _objectMapper = objectMapper;
        _blockChainEventHandlerOptions = blockChainEventHandlerOptions.Value;
        _blockGrainProvider = blockGrainProvider;
    }

    public async Task HandleEventAsync(BlockChainDataEto eventData)
    {
        _logger.LogInformation(
            $"Received BlockChainDataEto form {eventData.ChainId}, start block: {eventData.Blocks.First().BlockHeight}, end block: {eventData.Blocks.Last().BlockHeight},");

        //prepare data
        List<Task<NewBlockTaskEntity>> prepareDataTaskList = new List<Task<NewBlockTaskEntity>>();
        List<NewBlockEto> newBlockEtos = new List<NewBlockEto>();
        List<BlockData> blockEventDatas = new List<BlockData>();
        foreach (var blockItem in eventData.Blocks)
        {
            Func<NewBlockTaskEntity> funcBlockConvertTask = () =>
            {
                return ConvertBlockDataTask(blockItem, eventData.ChainId);
            };
            Task<NewBlockTaskEntity> task = Task.Run(funcBlockConvertTask);
            prepareDataTaskList.Add(task);
        }

        await Task.WhenAll(prepareDataTaskList.ToArray());

        foreach (var task in prepareDataTaskList)
        {
            var newBlockTaskEntity = task.Result;
            newBlockEtos.Add(newBlockTaskEntity.newBlockEto);
            blockEventDatas.Add(newBlockTaskEntity.BlockData);
        }

        var blockBranchGrain = await _blockGrainProvider.GetBlockBranchGrain(eventData.ChainId);
        List<BlockData> libBlockList = await blockBranchGrain.SaveBlocks(blockEventDatas);

        if (libBlockList != null)
        {
            // _logger.LogInformation("newBlockEtos count: " + newBlockEtos.Count);
            //publish new block event
            await PublishNewBlocksEtoAsync(new NewBlocksEto()
                { NewBlocks = newBlockEtos });

            if (libBlockList.Count > 0)
            {
                libBlockList = libBlockList.OrderBy(b => b.BlockHeight).ToList();

                //publish confirm blocks event
                var confirmBlockList =
                    _objectMapper.Map<List<BlockData>, List<ConfirmBlockEto>>(libBlockList);
                await PublishConfirmBlocksEtoAsync(new ConfirmBlocksEto()
                    { ConfirmBlocks = confirmBlockList });
            }
        }


    }

    private async Task PublishNewBlocksEtoAsync(NewBlocksEto eventData)
    { 
        var retryCount = 0;
        while (retryCount < _blockChainEventHandlerOptions.RetryTimes)
        {
            try
            {
                await _distributedEventBus.PublishAsync(eventData);
                break;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Publish new block event failed, retrying..." + retryCount);
                retryCount++;
                Thread.Sleep(_blockChainEventHandlerOptions.RetryInterval);
            }
        }
        
        if (retryCount >= _blockChainEventHandlerOptions.RetryTimes)
        {
            await _distributedEventBus.PublishAsync(eventData);
        }
    }

    private async Task PublishConfirmBlocksEtoAsync(ConfirmBlocksEto eventData)
    {
        var retryCount = 0;
        while (retryCount < _blockChainEventHandlerOptions.RetryTimes)
        {
            try
            {
                await _distributedEventBus.PublishAsync(eventData);
                break;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Publish confirm block event failed, retrying..." + retryCount);
                retryCount++;
                Thread.Sleep(_blockChainEventHandlerOptions.RetryInterval);
            }
        }
        
        if (retryCount >= _blockChainEventHandlerOptions.RetryTimes)
        {
            await _distributedEventBus.PublishAsync(eventData);
        }
    }

    // public async Task HandleEventAsync(BlockChainDataEto eventData)
    // {
    //     _logger.LogInformation($"Received BlockChainDataEto form {eventData.ChainId}, start block: {eventData.Blocks.First().BlockHeight}, end block: {eventData.Blocks.Last().BlockHeight},");
    //     // var blockGrain = _clusterClient.GetGrain<IBlockGrain>(_orleansClientOption.AElfBlockGrainPrimaryKey);
    //     var blockGrain = await _blockGrainProvider.GetBlockGrain(eventData.ChainId);
    //     int processedBlockCount = 0;
    //
    //     List<Task<NewBlockTaskEntity>> taskList = new List<Task<NewBlockTaskEntity>>();
    //     List<NewBlockEto> newBlockEtos = new List<NewBlockEto>();
    //     List<BlockEventData> blockEventDatas = new List<BlockEventData>();
    //     foreach (var blockItem in eventData.Blocks)
    //     {
    //         Func<NewBlockTaskEntity> funcBlockConvertTask = () =>
    //         {
    //             return ConvertBlockDataTask(blockItem, eventData.ChainId);
    //         };
    //         Task<NewBlockTaskEntity> task = Task.Run(funcBlockConvertTask);
    //         taskList.Add(task);
    //     }
    //
    //     await Task.WhenAll(taskList.ToArray());
    //
    //     foreach (var task in taskList)
    //     {
    //         var newBlockTaskEntity = task.Result;
    //         newBlockEtos.Add(newBlockTaskEntity.newBlockEto);
    //         blockEventDatas.Add(newBlockTaskEntity.blockEventData);
    //     }
    //
    //     int blockLimit = _blockChainEventHandlerOptions.BlockPartionLimit;
    //     int partion = (eventData.Blocks.Count % blockLimit) == 0
    //         ? (eventData.Blocks.Count / blockLimit)
    //         : (eventData.Blocks.Count / blockLimit) + 1;
    //     
    //     _logger.LogInformation($"blockLimit:{blockLimit} partion:{partion} ");
    //
    //     for (var i = 0; i < partion; i++)
    //     {
    //         _logger.LogInformation("skip: "+(i*blockLimit).ToString());
    //         List<BlockEventData> libBlockList = await blockGrain.SaveBlocks(blockEventDatas.Skip(i*blockLimit).Take(blockLimit).ToList());
    //
    //         if (libBlockList != null)
    //         {
    //             var newBlockPartEtos = newBlockEtos.Skip(i * blockLimit).Take(blockLimit).ToList();
    //             _logger.LogInformation("newBlockPartEtos count: " + newBlockPartEtos.Count);
    //             //publish new block event
    //             await _distributedEventBus.PublishAsync(new NewBlocksEto()
    //                 { NewBlocks = newBlockPartEtos });
    //
    //             processedBlockCount = processedBlockCount + newBlockPartEtos.Count;
    //
    //             if (libBlockList.Count > 0)
    //             {
    //                 libBlockList = libBlockList.OrderBy(b => b.BlockHeight).ToList();
    //                 //publish confirm blocks event
    //                 var confirmBlockList =
    //                     _objectMapper.Map<List<BlockEventData>, List<ConfirmBlockEto>>(libBlockList);
    //                 await _distributedEventBus.PublishAsync(new ConfirmBlocksEto()
    //                     { ConfirmBlocks = confirmBlockList });
    //             }
    //         }
    //     }
    //
    //     var primaryKeyGrain = _clusterClient.GetGrain<IPrimaryKeyGrain>(eventData.ChainId + AElfIndexerConsts.PrimaryKeyGrainIdSuffix);
    //     await primaryKeyGrain.SetCounter(processedBlockCount);
    // }

    private long AnalysisBlockLibFoundEvent(string logEventIndexed)
    {
        List<string> IndexedList =
            JsonConvert.DeserializeObject<List<string>>(logEventIndexed);
        var libFound = new IrreversibleBlockFound();
        libFound.MergeFrom(ByteString.FromBase64(IndexedList[0]));
        _logger.LogInformation(
            $"IrreversibleBlockFound: {libFound}");
        return libFound.IrreversibleBlockHeight;
    }

    private NewBlockEto ConvertToNewBlockEto(BlockEto blockItem,string chainId)
    {
        NewBlockEto newBlock = _objectMapper.Map<BlockEto, NewBlockEto>(blockItem);
        // newBlock.Id = Guid.NewGuid();
        newBlock.Id = newBlock.BlockHash;
        newBlock.ChainId = chainId;
        newBlock.Confirmed = false;
        foreach (var transaction in newBlock.Transactions)
        {
            transaction.ChainId = chainId;
            transaction.BlockHash = newBlock.BlockHash;
            transaction.PreviousBlockHash = newBlock.PreviousBlockHash;
            transaction.BlockHeight = newBlock.BlockHeight;
            transaction.BlockTime = newBlock.BlockTime;
            transaction.Confirmed = false;
        
            foreach (var logEvent in transaction.LogEvents)
            {
                logEvent.ChainId = chainId;
                logEvent.BlockHash = newBlock.BlockHash;
                logEvent.PreviousBlockHash = newBlock.PreviousBlockHash;
                logEvent.BlockHeight = newBlock.BlockHeight;
                logEvent.BlockTime = newBlock.BlockTime;
                logEvent.Confirmed = false;
                logEvent.TransactionId = transaction.TransactionId;
            }
        }

        return newBlock;
    }

    private NewBlockTaskEntity ConvertBlockDataTask(BlockEto blockItem,string chainId)
    {
        var newBlockEto = ConvertToNewBlockEto(blockItem, chainId);
        BlockData block = new BlockData();
        block = _objectMapper.Map<NewBlockEto, BlockData>(newBlockEto);

        //analysis lib found event content
        var libLogEvent = blockItem.Transactions?.SelectMany(t => t.LogEvents)
            .FirstOrDefault(e => e.EventName == "IrreversibleBlockFound");
        if (libLogEvent != null)
        {
            block.LibBlockHeight = AnalysisBlockLibFoundEvent(libLogEvent.ExtraProperties["Indexed"]);
        }

        NewBlockTaskEntity resultEntity = new NewBlockTaskEntity();
        resultEntity.newBlockEto = newBlockEto;
        resultEntity.BlockData = block;

        return resultEntity;
    }
}