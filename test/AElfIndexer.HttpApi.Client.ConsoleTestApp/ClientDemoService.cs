﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElfIndexer.Block.Dtos;
using AElfIndexer.Grains.State.BlockScan;
using AElfIndexer.Orleans.EventSourcing.Grain.BlockScan;
using Volo.Abp.Account;
using Volo.Abp.DependencyInjection;
using Volo.Abp.IdentityModel;
using Microsoft.AspNetCore.SignalR.Client;

namespace AElfIndexer.HttpApi.Client.ConsoleTestApp;

public class ClientDemoService : ITransientDependency
{
    private readonly IProfileAppService _profileAppService;
    private readonly IIdentityModelAuthenticationService _authenticationService;

    public ClientDemoService(IProfileAppService profileAppService, IIdentityModelAuthenticationService authenticationService)
    {
        _profileAppService = profileAppService;
        _authenticationService = authenticationService;
    }

    public async Task RunAsync()
    {
        try
        {
            var accessToken = await _authenticationService.GetAccessTokenAsync(new IdentityClientConfiguration
            {
                Authority = "http://localhost:8080",
                Scope = "AElfIndexer",
                GrantType = "client_credentials",
                ClientId = "AElfIndexer_DApp",
                ClientSecret = "1q2w3e*",
                RequireHttps = false
            });

            var connection = new HubConnectionBuilder()
                .WithUrl("http://localhost:8081/signalr-hubs/block",
                    options => { options.AccessTokenProvider = () => Task.FromResult(accessToken); })
                .Build();

            connection.On<List<BlockDto>>("ReceiveBlock",
                s =>
                {
                    Console.WriteLine(
                        $"Receive Block From {s.First().BlockHeight} To {s.Last().BlockHeight}");
                });
            
            await connection.StartAsync().ConfigureAwait(false);
            await connection.InvokeAsync("Subscribe", new List<SubscribeInfo>
            {
                new SubscribeInfo
                {
                    ChainId = "AELF",
                    OnlyConfirmedBlock = true,
                    StartBlockNumber = 40,
                    FilterType = BlockFilterType.LogEvent,
                    SubscribeEvents = new List<FilterContractEventInput>
                    {
                        new FilterContractEventInput
                        {
                            ContractAddress = "ContractAddress",
                            EventNames = new List<string>{"EventNames"}
                        }
                    }
                }
            });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}
