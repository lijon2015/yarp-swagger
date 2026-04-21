using Identity.Configs;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

IdentityConfig identityConfig = builder.Configuration.GetSection("Identity").Get<IdentityConfig>()!;

builder.Services.AddIdentityServer()
    .AddDeveloperSigningCredential()
    .AddInMemoryApiScopes(identityConfig.GetApiScopes())
    .AddInMemoryClients(identityConfig.GetClients());

WebApplication app = builder.Build();

app.UseIdentityServer();

app.Run();