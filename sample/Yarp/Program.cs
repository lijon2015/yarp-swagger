using Duende.AccessTokenManagement;
using Yarp.Configs;
using Yuzhu.Yarp.Swagger.Adapters.Swashbuckle;
using Yuzhu.Yarp.Swagger.Extensions;
using Yarp.Transformations;

var builder = WebApplication.CreateBuilder(args);

var configuration = builder.Configuration.GetSection("ReverseProxy");
var reverseProxyBuilder = builder.Services
    .AddReverseProxy()
    .LoadFromConfig(configuration)
    .AddTransformFactory<HeaderTransformFactory>();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddHybridCache();
    builder.Services.AddClientCredentialsTokenManagement()
        .AddClient(ClientCredentialsClientName.Parse("Identity"), client =>
        {
            var identityConfig = builder.Configuration.GetSection("Identity").Get<IdentityConfig>()!;
            client.TokenEndpoint = new Uri($"{identityConfig.Url}/connect/token");
            client.ClientId = ClientId.Parse(identityConfig.ClientId);
            client.ClientSecret = ClientSecret.Parse(identityConfig.ClientSecret);
        });

    reverseProxyBuilder.AddSwaggerAggregation();
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.ConfigureAggregatedEndpoints(app.Services);
    });
}

app.UseHttpsRedirection();

app.MapReverseProxy();

app.Run();
