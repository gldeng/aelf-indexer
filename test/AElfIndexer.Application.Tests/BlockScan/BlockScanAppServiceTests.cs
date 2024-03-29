using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElfIndexer.Block.Dtos;
using AElfIndexer.Grains;
using AElfIndexer.Grains.Grain.BlockScan;
using AElfIndexer.Grains.Grain.Client;
using AElfIndexer.Grains.State.BlockScan;
using AElfIndexer.Grains.State.Client;
using Orleans;
using Shouldly;
using Xunit;

namespace AElfIndexer.BlockScan;

public class BlockScanAppServiceTests : AElfIndexerApplicationOrleansTestBase
{
    private IBlockScanAppService _blockScanAppService;
    private IClusterClient _clusterClient;

    public BlockScanAppServiceTests()
    {
        _blockScanAppService = GetRequiredService<IBlockScanAppService>();
        _clusterClient = GetRequiredService<IClusterClient>();
    }

    [Fact]
    public async Task ScanTest()
    {
        var clientId = "Client";
        var subscriptionInfo1 = new List<SubscriptionInfo>
        {
            new SubscriptionInfo
            {
                ChainId = "AELF",
                FilterType = BlockFilterType.Block
            }
        };

        var version1 = await _blockScanAppService.SubmitSubscriptionInfoAsync(clientId, subscriptionInfo1);

        var subscription = await _blockScanAppService.GetSubscriptionInfoAsync(clientId);
        subscription.CurrentVersion.Version.ShouldBe(version1);
        subscription.CurrentVersion.SubscriptionInfos[0].FilterType.ShouldBe(BlockFilterType.Block);
        subscription.NewVersion.ShouldBeNull();

        var version = await _blockScanAppService.GetClientVersionAsync(clientId);
        version.CurrentVersion.ShouldBe(version1);
        version.NewVersion.ShouldBeNull();
        
        await _blockScanAppService.UpgradeVersionAsync(clientId);
        
        version = await _blockScanAppService.GetClientVersionAsync(clientId);
        version.CurrentVersion.ShouldBe(version1);
        version.NewVersion.ShouldBeNull();
        
        var streamIds = await _blockScanAppService.GetMessageStreamIdsAsync(clientId, version1);
        var id = GrainIdHelper.GenerateGrainId(subscriptionInfo1[0].ChainId, clientId, version1,
            subscriptionInfo1[0].FilterType);
        var blockScanInfoGrain = _clusterClient.GetGrain<IBlockScanInfoGrain>(id);
        var streamId = await blockScanInfoGrain.GetMessageStreamIdAsync();
        streamIds.Count.ShouldBe(1);
        streamIds[0].ShouldBe(streamId);

        await _blockScanAppService.StartScanAsync(clientId, version1);
        
        var clientGrain = _clusterClient.GetGrain<IClientGrain>(clientId);
        var scanIds = await clientGrain.GetBlockScanIdsAsync(version1);
        scanIds.Count.ShouldBe(1);
        scanIds[0].ShouldBe(id);

        var versionStatus = await clientGrain.GetVersionStatusAsync(version1);
        versionStatus.ShouldBe(VersionStatus.Started);

        var subscriptionInfo2 = new List<SubscriptionInfo>
        {
            new SubscriptionInfo
            {
                ChainId = "AELF",
                FilterType = BlockFilterType.Transaction
            }
        };
        
        var version2 = await _blockScanAppService.SubmitSubscriptionInfoAsync(clientId, subscriptionInfo2);

        subscription = await _blockScanAppService.GetSubscriptionInfoAsync(clientId);
        subscription.CurrentVersion.Version.ShouldBe(version1);
        subscription.CurrentVersion.SubscriptionInfos[0].FilterType.ShouldBe(BlockFilterType.Block);
        subscription.NewVersion.Version.ShouldBe(version2);
        subscription.NewVersion.SubscriptionInfos[0].FilterType.ShouldBe(BlockFilterType.Transaction);
        
        version = await _blockScanAppService.GetClientVersionAsync(clientId);
        version.CurrentVersion.ShouldBe(version1);
        version.NewVersion.ShouldBe(version2);
        
        await _blockScanAppService.StartScanAsync(clientId, version2);
        
        var subscriptionInfo3 = new List<SubscriptionInfo>
        {
            new SubscriptionInfo
            {
                ChainId = "AELF",
                FilterType = BlockFilterType.LogEvent
            }
        };
        
        var version3 = await _blockScanAppService.SubmitSubscriptionInfoAsync(clientId, subscriptionInfo3);

        subscription = await _blockScanAppService.GetSubscriptionInfoAsync(clientId);
        subscription.CurrentVersion.Version.ShouldBe(version1);
        subscription.CurrentVersion.SubscriptionInfos[0].FilterType.ShouldBe(BlockFilterType.Block);
        subscription.NewVersion.Version.ShouldBe(version3);
        subscription.NewVersion.SubscriptionInfos[0].FilterType.ShouldBe(BlockFilterType.LogEvent);
        
        version = await _blockScanAppService.GetClientVersionAsync(clientId);
        version.CurrentVersion.ShouldBe(version1);
        version.NewVersion.ShouldBe(version3);
        
        await _blockScanAppService.StartScanAsync(clientId, version3);
        
        var blockScanManagerGrain = _clusterClient.GetGrain<IBlockScanManagerGrain>(0);
        var allScanIds = await blockScanManagerGrain.GetAllBlockScanIdsAsync();
        allScanIds["AELF"].ShouldNotContain(subscriptionInfo2[0].ChainId + clientId + version2 + subscriptionInfo2[0].FilterType);

        scanIds = await clientGrain.GetBlockScanIdsAsync(version2);
        scanIds.Count.ShouldBe(0);

        await _blockScanAppService.UpgradeVersionAsync(clientId);
        
        version = await _blockScanAppService.GetClientVersionAsync(clientId);
        version.CurrentVersion.ShouldBe(version3);
        version.NewVersion.ShouldBeNull();
        
        allScanIds = await blockScanManagerGrain.GetAllBlockScanIdsAsync();
        allScanIds["AELF"].ShouldNotContain(subscriptionInfo1[0].ChainId + clientId + version1 + subscriptionInfo1[0].FilterType);
        
        await _blockScanAppService.StartScanAsync(clientId, version3);

        await _blockScanAppService.StopAsync(clientId, version3);
        
        version = await _blockScanAppService.GetClientVersionAsync(clientId);
        version.CurrentVersion.ShouldBeNull();
        version.NewVersion.ShouldBeNull();
        
        allScanIds = await blockScanManagerGrain.GetAllBlockScanIdsAsync();
        allScanIds["AELF"].ShouldNotContain(subscriptionInfo3[0].ChainId + clientId + version3 + subscriptionInfo3[0].FilterType);
    }

    [Fact]
    public async Task UpdateSubscriptionTest()
    {
        var clientId = "ClientTest";
        var subscriptionInfo1 = new List<SubscriptionInfo>
        {
            new SubscriptionInfo
            {
                ChainId = "AELF",
                FilterType = BlockFilterType.Transaction,
                OnlyConfirmedBlock = true,
                StartBlockNumber = 999,
                SubscribeEvents = new List<FilterContractEventInput>()
                {
                    new FilterContractEventInput()
                    {
                        ContractAddress = "23GxsoW9TRpLqX1Z5tjrmcRMMSn5bhtLAf4HtPj8JX9BerqTqp",
                        EventNames = new List<string>()
                        {
                            "Transfer"
                        }
                    }
                }
            }
        };
        var version1 = await _blockScanAppService.SubmitSubscriptionInfoAsync(clientId, subscriptionInfo1);
        
        var subscription = await _blockScanAppService.GetSubscriptionInfoAsync(clientId);
        subscription.CurrentVersion.Version.ShouldBe(version1);
        subscription.CurrentVersion.SubscriptionInfos[0].FilterType.ShouldBe(BlockFilterType.Transaction);
        subscription.NewVersion.ShouldBeNull();
        
        var subscriptionInfo2 = new List<SubscriptionInfo>
        {
            new SubscriptionInfo
            {
                ChainId = "AELF",
                FilterType = BlockFilterType.Transaction,
                OnlyConfirmedBlock = true,
                StartBlockNumber = 999,
                SubscribeEvents = new List<FilterContractEventInput>()
                {
                    new FilterContractEventInput()
                    {
                        ContractAddress = "23GxsoW9TRpLqX1Z5tjrmcRMMSn5bhtLAf4HtPj8JX9BerqTqp",
                        EventNames = new List<string>()
                        {
                            "Transfer",
                            "SetNumbered"
                        }
                    }
                }
            }
        };
        await _blockScanAppService.UpdateSubscriptionInfoAsync(clientId, version1, subscriptionInfo2);
        var subscription2 = await _blockScanAppService.GetSubscriptionInfoAsync(clientId);
        subscription2.CurrentVersion.Version.ShouldBe(version1);
        subscription2.CurrentVersion.SubscriptionInfos[0].FilterType.ShouldBe(BlockFilterType.Transaction);
        subscription2.CurrentVersion.SubscriptionInfos[0].SubscribeEvents.Count.ShouldBe(1);
        subscription2.CurrentVersion.SubscriptionInfos[0].SubscribeEvents[0].EventNames.Count.ShouldBe(2);
    }
}