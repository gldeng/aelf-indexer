using Volo.Abp.DependencyInjection;

namespace AElfIndexer.Client.Providers;

public class AElfIndexerClientInfoProvider : IAElfIndexerClientInfoProvider, ISingletonDependency
{
    private string _clientId;
    private string _version;


    public string GetClientId()
    {
        return _clientId;
    }

    public void SetClientId(string clientId)
    {
        _clientId = clientId;
    }

    public string GetVersion()
    {
        return _version;
    }

    public void SetVersion(string version)
    {
        _version = version;
    }
}