using System;
using System.Collections.Generic;
using Nest;

namespace AElfScan.AElf.Entities.Es;

[ElasticsearchType]
public class LogEvent:IBlockchainData
{
    [Keyword]public string ChainId { get; set; }
    [Keyword]public string BlockHash { get; set; }
    
    /// <summary>
    /// block height
    /// </summary>
    public long BlockNumber { get; set; }
    
    [Keyword]public string TransactionId { get; set; }
    
    public DateTime BlockTime { get; set; }
    
    [Keyword]public string ContractAddress { get; set; }
    
    [Keyword]public string EventName { get; set; }
    
    /// <summary>
    /// The ranking position of the event within the transaction
    /// </summary>
    public int Index { get; set; }
    
    public bool IsConfirmed{get;set;}
    
    public Dictionary<string,string> ExtraProperties {get;set;}
}