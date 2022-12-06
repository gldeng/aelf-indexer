using AElfIndexer.Block;
using AElfIndexer.Block.Dtos;

namespace AElfIndexer.Grains.Grain.BlockScan;

public class TransactionFilterProvider : IBlockFilterProvider
{
    private readonly IBlockAppService _blockAppService;

    public BlockFilterType FilterType { get; } = BlockFilterType.Transaction;

    public TransactionFilterProvider(IBlockAppService blockAppService)
    {
        _blockAppService = blockAppService;
    }

    public async Task<List<BlockDto>> GetBlocksAsync(string chainId, long startBlockNumber, long endBlockNumber,
        bool onlyConfirmed, List<FilterContractEventInput> filters)
    {
        var transactions = await _blockAppService.GetTransactionsAsync(new GetTransactionsInput()
        {
            ChainId = chainId,
            StartBlockHeight = startBlockNumber,
            EndBlockHeight = endBlockNumber,
            IsOnlyConfirmed = onlyConfirmed, 
            Events = filters
        });

        var blocks = new Dictionary<string, BlockDto>();
        foreach (var transaction in transactions)
        {
            if (blocks.TryGetValue(transaction.BlockHash, out var block))
            {
                block.Transactions.Add(transaction);
            }
            else
            {
                block = new BlockDto
                {
                    ChainId = transaction.ChainId,
                    BlockHash = transaction.BlockHash,
                    BlockHeight = transaction.BlockHeight,
                    PreviousBlockHash = transaction.PreviousBlockHash,
                    BlockTime = transaction.BlockTime,
                    IsConfirmed = transaction.IsConfirmed,
                    Transactions = new List<TransactionDto>
                    {
                        transaction
                    }
                };
                blocks.Add(block.BlockHash, block);
            }
        }

        return blocks.Values.ToList();
    }

    public async Task<List<BlockDto>> FilterBlocksAsync(List<BlockDto> blocks, List<FilterContractEventInput> filters)
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

        var result = new List<BlockDto>();
        foreach (var block in blocks)
        {
            var filteredBlock = new BlockDto
            {
                ChainId = block.ChainId,
                BlockHash = block.BlockHash,
                BlockHeight = block.BlockHeight,
                BlockTime = block.BlockTime,
                PreviousBlockHash = block.PreviousBlockHash,
                IsConfirmed = block.IsConfirmed,
                Transactions = new List<TransactionDto>()
            };

            foreach (var transaction in block.Transactions.Where(transaction => transaction.LogEvents.Any(logEvent =>
                         (contractAddressFilter.Count > 0 &&
                          contractAddressFilter.Contains(logEvent.ContractAddress)) ||
                         (logEventFilter.Count > 0 &&
                          logEventFilter.Contains(logEvent.ContractAddress + logEvent.EventName)))))
            {
                filteredBlock.Transactions.Add(transaction);
            }

            result.Add(filteredBlock);
        }

        return result;
    }
}