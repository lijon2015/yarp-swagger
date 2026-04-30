namespace Yuzhu.Yarp.Swagger.Configuration;

/// <summary>
/// Constants used by the Swagger aggregation pipeline.
/// </summary>
public static class SwaggerConstants
{
    /// <summary>Named <see cref="System.Net.Http.HttpClient"/> the loader uses.</summary>
    public const string HttpClientName = "YuzhuYarpSwagger";

    /// <summary>Configuration paths checked for YARP clusters when reading from <see cref="Microsoft.Extensions.Configuration.IConfiguration"/>.</summary>
    public static readonly IReadOnlyList<string> YarpClusterConfigSections =
    [
        "ReverseProxy:Clusters",
        "Yarp:Clusters",
    ];

    /// <summary>Default Swagger document path on the backend.</summary>
    public const string DefaultSwaggerPath = "/swagger/v1/swagger.json";

    /// <summary>Maximum allowed path filter regex length (ReDoS guard).</summary>
    public const int MaxPathFilterLength = 500;

    /// <summary>Regex match timeout (ReDoS guard).</summary>
    public static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    /// <summary>Default upper bound for any one Swagger document, in bytes.</summary>
    public const int DefaultMaxDocumentSizeBytes = 10 * 1024 * 1024;
}
