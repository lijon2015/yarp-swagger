using Duende.AccessTokenManagement;
using Yarp.Configs;
using Yarp.Transformations;
using Yuzhu.Yarp.Swagger.Adapters.Swashbuckle;
using Yuzhu.Yarp.Swagger.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

IConfigurationSection configuration = builder.Configuration.GetSection("ReverseProxy");
IReverseProxyBuilder reverseProxyBuilder = builder.Services
    .AddReverseProxy()
    .LoadFromConfig(configuration)
    .AddTransformFactory<HeaderTransformFactory>();

if (builder.Environment.IsDevelopment())
{
    _ = builder.Services.AddEndpointsApiExplorer();
    _ = builder.Services.AddSwaggerGen();

    _ = builder.Services.AddHybridCache();
    _ = builder.Services.AddClientCredentialsTokenManagement()
        .AddClient(ClientCredentialsClientName.Parse("Identity"), client =>
        {
            IdentityConfig identityConfig = builder.Configuration.GetSection("Identity").Get<IdentityConfig>()!;
            client.TokenEndpoint = new Uri($"{identityConfig.Url}/connect/token");
            client.ClientId = ClientId.Parse(identityConfig.ClientId);
            client.ClientSecret = ClientSecret.Parse(identityConfig.ClientSecret);
        });

    _ = reverseProxyBuilder.AddSwaggerAggregation();
}

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // Library-owned middleware: 200/404/503 semantics for /swagger/{document}/swagger.json.
    // Must run before UseSwagger so its responses take precedence over Swashbuckle's
    // default exception handling.
    _ = app.UseSwaggerAggregationDocuments();
    _ = app.UseSwagger();
    _ = app.UseSwaggerUI(options =>
        options.ConfigureAggregatedEndpoints(app.Services));
}

app.UseHttpsRedirection();

app.MapReverseProxy();

app.Run();
