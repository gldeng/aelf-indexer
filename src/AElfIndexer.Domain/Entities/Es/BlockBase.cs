using System;
using System.Collections.Generic;
using AElfIndexer.Entities;
using Nest;

namespace AElfIndexer.Entities.Es;

public class BlockBase:AElfIndexerEntity<string>,IBlockchainData
{
    [Keyword]public override string Id { get; set; }
    [Keyword]public string ChainId { get; set; }
    [Keyword]public string BlockHash { get; set; }
    public long BlockHeight { get; set; }
    [Keyword]public string PreviousBlockHash { get; set; }
    public DateTime BlockTime { get; set; }
    [Keyword]public string SignerPubkey { get; set; }
    [Keyword]public string Signature { get; set; }
    public bool Confirmed{get;set;}
    public Dictionary<string,string> ExtraProperties {get;set;}

}