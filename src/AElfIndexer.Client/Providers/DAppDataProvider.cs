using System.Collections.Concurrent;
using AElfIndexer.Grains.Grain.Client;
using Newtonsoft.Json;
using Nito.AsyncEx;
using Orleans;
using Volo.Abp.DependencyInjection;

namespace AElfIndexer.Client.Providers;

internal class DAppDataProvider : IDAppDataProvider, ISingletonDependency
{
    private readonly IClusterClient _clusterClient;

    private readonly ConcurrentDictionary<string, string> _libValues = new();
    private readonly ConcurrentDictionary<string, string> _toCommitLibValues = new();

    public DAppDataProvider(IClusterClient clusterClient)
    {
        _clusterClient = clusterClient;
    }

    public async Task<T> GetLibValueAsync<T>(string key)
    {
        if (!_libValues.TryGetValue(key, out var value))
        {
            var dappDataGrain = _clusterClient.GetGrain<IDappDataGrain>(key);
            value = await dappDataGrain.GetLIBValue();
            _libValues[key] = value;
        }
        
        return value != null ? JsonConvert.DeserializeObject<T>(value) : default;
    }

    public async Task SetLibValueAsync<T>(string key, T value)
    {
        var jsonValue = JsonConvert.SerializeObject(value);
        _toCommitLibValues[key] = jsonValue;
        _libValues[key] = jsonValue;
    }
    
    public async Task SetLibValueAsync(string key, string value)
    {
        _toCommitLibValues[key] = value;
        _libValues[key] = value;
    }

    public async Task CommitAsync()
    {
        var tasks = _toCommitLibValues.Select(async o =>
        {
            var dappDataGrain = _clusterClient.GetGrain<IDappDataGrain>(o.Key);
            await dappDataGrain.SetLIBValue(_libValues[o.Key]);
        });
        await tasks.WhenAll();
        _toCommitLibValues.Clear();
    }
}