using System;
using System.Threading.Tasks;
using AElfIndexer.BlockScan;
using AElfIndexer.Grains.State.BlockScan;
using Orleans;

namespace AElfIndexer.Grains.Grain.BlockScan;

public class BlockScanInfoGrain : Grain<BlockScanInfoState>, IBlockScanInfoGrain
{
    public override async Task OnActivateAsync()
    {
        await this.ReadStateAsync();
        await base.OnActivateAsync();
    }

    public Task<ClientInfo> GetClientInfoAsync()
    {
        return Task.FromResult(State.ClientInfo);
    }

    public Task<SubscriptionInfo> GetSubscriptionInfoAsync()
    {
        return Task.FromResult(State.SubscriptionInfo);
    }

    public async Task SetScanNewBlockStartHeightAsync(long height)
    {
        State.ClientInfo.ScanModeInfo.ScanMode = ScanMode.NewBlock;
        State.ClientInfo.ScanModeInfo.ScanNewBlockStartHeight = height;
        await WriteStateAsync();
    }
    
    public async Task SetHistoricalBlockScanModeAsync()
    {
        State.ClientInfo.ScanModeInfo.ScanMode = ScanMode.HistoricalBlock;
        State.ClientInfo.ScanModeInfo.ScanNewBlockStartHeight = 0;
        await WriteStateAsync();
    }
    
    public async Task SetHandleHistoricalBlockTimeAsync(DateTime time)
    {
        State.ClientInfo.LastHandleHistoricalBlockTime = time;
        await WriteStateAsync();
    }

    public async Task InitializeAsync(string chainId, string clientId, string version, SubscriptionInfo info)
    {
        var blockScanManager = GrainFactory.GetGrain<IBlockScanManagerGrain>(0);
        await blockScanManager.AddBlockScanAsync(chainId, this.GetPrimaryKeyString());

        State.ClientInfo = new ClientInfo
        {
            ChainId = chainId,
            ClientId = clientId,
            Version = version,
            LastHandleHistoricalBlockTime = DateTime.UtcNow,
            ScanModeInfo = new ScanModeInfo
            {
                ScanMode = ScanMode.HistoricalBlock,
                ScanNewBlockStartHeight = 0
            }
        };
        State.SubscriptionInfo = info;
        await WriteStateAsync();
    }

    public async Task UpdateSubscriptionInfoAsync(SubscriptionInfo info)
    {
        State.SubscriptionInfo = info;
        await WriteStateAsync();
    }

    public async Task StopAsync()
    {
        var blockScanManager = GrainFactory.GetGrain<IBlockScanManagerGrain>(0);
        await blockScanManager.RemoveBlockScanAsync(State.ClientInfo.ChainId, this.GetPrimaryKeyString());
    }
    
    public async Task<Guid> GetMessageStreamIdAsync()
    {
        if (State.MessageStreamId == Guid.Empty)
        {
            State.MessageStreamId = Guid.NewGuid();
            await WriteStateAsync();
        }

        return State.MessageStreamId;
    }
}