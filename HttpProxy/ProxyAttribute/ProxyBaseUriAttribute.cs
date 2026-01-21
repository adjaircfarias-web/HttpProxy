namespace HttpProxy.ProxyAttribute;

[AttributeUsage(AttributeTargets.Interface)]
public class ProxyBaseUriAttribute : Attribute
{
    public string BaseUri { get; private set; }

    /// <summary>
    /// Attribute class for parameters request
    /// </summary>
    /// <param name="baseUri">Base Uri for request</param>
    public ProxyBaseUriAttribute(string baseUri)
    {
        BaseUri = baseUri;
    }

    public static ProxyBaseUriAttribute GetCustomAttribute(Type type)
    {
        return (ProxyBaseUriAttribute)Attribute.GetCustomAttribute(type, typeof(ProxyBaseUriAttribute));
    }
}
