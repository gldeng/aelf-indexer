using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElfIndexer.Block;
using AElfIndexer.Block.Dtos;
using Microsoft.Extensions.Logging;

namespace AElfIndexer.Grains.Grain.BlockScan;

public class LogEventFilterProvider : BlockFilterProviderBase, IBlockFilterProvider
{
    private readonly ILogger<LogEventFilterProvider> _logger;

    public BlockFilterType FilterType { get; } = BlockFilterType.LogEvent;

    public LogEventFilterProvider(IBlockAppService blockAppService, ILogger<LogEventFilterProvider> logger)
        : base(blockAppService)
    {
        _logger = logger;
    }

    public async Task<List<BlockWithTransactionDto>> GetBlocksAsync(string chainId, long startBlockHeight, long endBlockHeight,
        bool onlyConfirmed, List<FilterContractEventInput> filters)
    {
        var logEvents = await BlockAppService.GetLogEventsAsync(new GetLogEventsInput()
        {
            ChainId = chainId,
            StartBlockHeight = startBlockHeight,
            EndBlockHeight = endBlockHeight,
            IsOnlyConfirmed = onlyConfirmed,
            Events = filters
        });

        var transactions = new Dictionary<string, TransactionDto>();
        foreach (var logEvent in logEvents)
        {
            if (transactions.TryGetValue(logEvent.TransactionId, out var transaction))
            {
                transaction.LogEvents.Add(logEvent);
            }
            else
            {
                transaction = new TransactionDto
                {
                    ChainId = logEvent.ChainId,
                    BlockHash = logEvent.BlockHash,
                    BlockHeight = logEvent.BlockHeight,
                    BlockTime = logEvent.BlockTime,
                    PreviousBlockHash = logEvent.PreviousBlockHash,
                    Confirmed = logEvent.Confirmed,
                    TransactionId = logEvent.TransactionId,
                    LogEvents = new List<LogEventDto> { logEvent }
                };
                transactions.Add(logEvent.TransactionId, transaction);
            }
        }

        var blocks = new Dictionary<string, BlockWithTransactionDto>();
        foreach (var transaction in transactions.Values)
        {
            if (blocks.TryGetValue(transaction.BlockHash, out var block))
            {
                block.Transactions.Add(transaction);
            }
            else
            {
                block = new BlockWithTransactionDto
                {
                    ChainId = transaction.ChainId,
                    BlockHash = transaction.BlockHash,
                    BlockHeight = transaction.BlockHeight,
                    BlockTime = transaction.BlockTime,
                    Confirmed = transaction.Confirmed,
                    PreviousBlockHash = transaction.PreviousBlockHash,
                    Transactions = new List<TransactionDto>
                    {
                        transaction
                    }
                };
                blocks.Add(block.BlockHash, block);
            }
        }

        var result = blocks.Values.ToList();
        result = await FillVacantBlockAsync(chainId, result, startBlockHeight, endBlockHeight, onlyConfirmed);
        
        if (result.Count != 0 && result.First().BlockHeight != startBlockHeight)
        {
            throw new ApplicationException(
                $"Get LogEvent filed, ChainId {chainId} StartBlockHeight {startBlockHeight} EndBlockHeight {endBlockHeight} OnlyConfirmed {onlyConfirmed}, Result first block height {result.First().BlockHeight}");
        }
        
        return result;
    }

    public async Task<List<BlockWithTransactionDto>> FilterBlocksAsync(List<BlockWithTransactionDto> blocks, List<FilterContractEventInput> filters)
    {
        if (filters == null || filters.Count == 0)
        {
            return blocks;
        }

        var contractAddressFilter = new HashSet<string>();
        var logEventFilter = new HashSet<string>();
        foreach (var filter in filters)
        {
            if (filter.EventNames == null || filter.EventNames.Count == 0)
            {
                contractAddressFilter.Add(filter.ContractAddress);
            }
            else
            {
                foreach (var eventName in filter.EventNames)
                {
                    logEventFilter.Add(filter.ContractAddress + eventName);
                }
            }
        }

        var result = new List<BlockWithTransactionDto>();
        foreach (var block in blocks)
        {
            var filteredBlock = new BlockWithTransactionDto
            {
                ChainId = block.ChainId,
                BlockHash = block.BlockHash,
                BlockHeight = block.BlockHeight,
                BlockTime = block.BlockTime,
                PreviousBlockHash = block.PreviousBlockHash,
                Confirmed = block.Confirmed,
                Transactions = new List<TransactionDto>()
            };

            foreach (var transaction in block.Transactions)
            {
                var filteredTransaction = new TransactionDto
                {
                    ChainId = transaction.ChainId,
                    BlockHash = transaction.BlockHash,
                    BlockHeight = transaction.BlockHeight,
                    BlockTime = transaction.BlockTime,
                    PreviousBlockHash = transaction.PreviousBlockHash,
                    Confirmed = transaction.Confirmed,
                    TransactionId = transaction.TransactionId,
                    LogEvents = new List<LogEventDto>()
                };

                foreach (var logEvent in transaction.LogEvents.Where(logEvent =>
                             (contractAddressFilter.Count > 0 &&
                              contractAddressFilter.Contains(logEvent.ContractAddress)) ||
                             (logEventFilter.Count > 0 &&
                              logEventFilter.Contains(logEvent.ContractAddress + logEvent.EventName))))
                {
                    filteredTransaction.LogEvents.Add(logEvent);
                }

                if (filteredTransaction.LogEvents.Count > 0)
                {
                    filteredBlock.Transactions.Add(filteredTransaction);
                }
            }

            result.Add(filteredBlock);
        }

        return result;
    }
    
    public async Task<List<BlockWithTransactionDto>> FilterIncompleteBlocksAsync(string chainId, List<BlockWithTransactionDto> blocks)
    {
        var filteredBlocks = new List<BlockWithTransactionDto>();
        var blockDtos = (await BlockAppService.GetBlocksAsync(new GetBlocksInput
        {
            ChainId = chainId,
            IsOnlyConfirmed = false,
            StartBlockHeight = blocks.First().BlockHeight,
            EndBlockHeight = blocks.Last().BlockHeight
        })).ToDictionary(o=>o.BlockHash, o=>o);

        foreach (var block in blocks)
        {
            if (!blockDtos.TryGetValue(block.BlockHash, out var blockDto) ||
                block.Transactions.Sum(o=>o.LogEvents.Count) != blockDto.LogEventCount)
            {
                _logger.LogWarning(
                    $"Wrong Transactions or LogEvents: block hash {block.BlockHash}, block height {block.BlockHeight}, transaction count {block.Transactions.Count}, logevent count {block.Transactions.Sum(o => o.LogEvents.Count)}");
                break;            }

            filteredBlocks.Add(block);
        }

        return filteredBlocks;
    }

    public async Task<List<BlockWithTransactionDto>> FilterIncompleteConfirmedBlocksAsync(string chainId, 
        List<BlockWithTransactionDto> blocks, string previousBlockHash, long previousBlockHeight)
    {
        var filteredBlocks = new List<BlockWithTransactionDto>();
        var blockDtos = (await BlockAppService.GetBlocksAsync(new GetBlocksInput
        {
            ChainId = chainId,
            IsOnlyConfirmed = true,
            StartBlockHeight = blocks.First().BlockHeight,
            EndBlockHeight = blocks.Last().BlockHeight
        })).ToDictionary(o=>o.BlockHash, o=>o);

        foreach (var block in blocks)
        {
            if (block.PreviousBlockHash != previousBlockHash && previousBlockHash!=null  || block.BlockHeight != previousBlockHeight + 1)
            {
                _logger.LogWarning($"Wrong confirmed previousBlockHash or previousBlockHash: block hash {block.BlockHash}, block height {block.BlockHeight}");
                break;
            }
            
            if (!blockDtos.TryGetValue(block.BlockHash, out var blockDto) ||
                block.Transactions.Sum(o=>o.LogEvents.Count) != blockDto.LogEventCount)
            {
                _logger.LogWarning(
                    $"Wrong confirmed Transactions or LogEvents: block hash {block.BlockHash}, block height {block.BlockHeight}, transaction count {block.Transactions.Count}, logevent count {block.Transactions.Sum(o => o.LogEvents.Count)}");
                break;
            }

            filteredBlocks.Add(block);
            
            previousBlockHash = block.BlockHash;
            previousBlockHeight = block.BlockHeight;
        }


        return filteredBlocks;
    }
}