using Microsoft.Extensions.Configuration;
using Yuzhu.Yarp.Swagger.Configuration;
using Yuzhu.Yarp.Swagger.Discovery.Metadata;

namespace Yuzhu.Yarp.Swagger.Tests;

public sealed class MetadataAccessorTests
{
    [Fact]
    public void DictionaryAccessor_ReadsFlatKey()
    {
        DictionarySwaggerMetadataAccessor accessor = new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MetadataKeys.Enabled] = "true",
        });

        Assert.Equal("true", accessor.Get(MetadataKeys.Enabled));
    }

    [Fact]
    public void DictionaryAccessor_ReturnsNullWhenMetadataIsNull()
    {
        DictionarySwaggerMetadataAccessor accessor = new(metadata: null);

        Assert.Null(accessor.Get(MetadataKeys.Enabled));
    }

    [Fact]
    public void ConfigurationAccessor_ReadsFlatKeyFromInMemoryProvider()
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Metadata:Swagger:Enabled"] = "true",
            })
            .Build();

        ConfigurationSwaggerMetadataAccessor accessor = new(config.GetSection("Metadata"));

        Assert.Equal("true", accessor.Get(MetadataKeys.Enabled));
    }

    [Fact]
    public void ConfigurationAccessor_ReadsNestedPathFromJsonStyleConfig()
    {
        // Simulates a JSON config provider where Swagger:Enabled flattens to
        // Metadata -> Swagger -> Enabled.
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Metadata:Swagger:Enabled"] = "true",
                ["Metadata:Swagger:Prefix"] = "/proxy-orders",
            })
            .Build();

        ConfigurationSwaggerMetadataAccessor accessor = new(config.GetSection("Metadata"));

        Assert.Equal("true", accessor.Get(MetadataKeys.Enabled));
        Assert.Equal("/proxy-orders", accessor.Get(MetadataKeys.Prefix));
    }

    [Fact]
    public void ConfigurationAccessor_ReturnsNullForMissingKey()
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Metadata:Swagger:Enabled"] = "true",
            })
            .Build();

        ConfigurationSwaggerMetadataAccessor accessor = new(config.GetSection("Metadata"));

        Assert.Null(accessor.Get(MetadataKeys.AccessTokenClient));
    }
}
