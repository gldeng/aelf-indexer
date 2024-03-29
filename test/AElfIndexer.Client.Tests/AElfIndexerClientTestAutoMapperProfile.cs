using AElfIndexer.Client.Handlers;
using AElfIndexer.Grains.State.Client;
using AutoMapper;

namespace AElfIndexer.Client;

public class AElfIndexerClientTestAutoMapperProfile : Profile
{
    public AElfIndexerClientTestAutoMapperProfile()
    {
        CreateMap<BlockInfo, TestBlockIndex>();
        CreateMap<TransactionInfo, TestTransactionIndex>();
        CreateMap<LogEventContext, TestTransferredIndex>();
        CreateMap<BlockInfo, TestIndex>();
    }
}