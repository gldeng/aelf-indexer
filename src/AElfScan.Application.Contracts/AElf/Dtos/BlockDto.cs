using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace AElfScan.AElf.Dtos;

public class BlockDto:EntityDto<string>
{
    public string ChainId { get; set; }
    public string BlockHash { get; set; }
    public long BlockNumber { get; set; }
    public string PreviousBlockHash { get; set; }
    public DateTime BlockTime { get; set; }
    public string SignerPubkey { get; set; }
    public string Signature { get; set; }
    public bool IsConfirmed{get;set;}
    public Dictionary<string,string> ExtraProperties {get;set;}
    public List<TransactionDto> Transactions {get;set;}
}