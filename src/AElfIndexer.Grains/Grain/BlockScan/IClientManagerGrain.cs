using System.Collections.Generic;
using System.Threading.Tasks;
using Orleans;

namespace AElfIndexer.Grains.Grain.BlockScan;

public interface IClientManagerGrain : IGrainWithIntegerKey
{
    Task<List<string>> GetClientIdsByChainAsync(string chainId);
    Task<Dictionary<string, HashSet<string>>> GetAllClientIdsAsync();
    Task AddClientAsync(string chainId, string clientId);
    Task RemoveClientAsync(string chainId, string clientId);
}