using App1;
using App1.Configs;
using App1.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => _ = options.AddSecurity());

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        IdentityConfig identityConfig = builder.Configuration.GetSection("Identity").Get<IdentityConfig>()!;

        options.Authority = identityConfig.Url;
        options.RequireHttpsMetadata = true;
        options.TokenValidationParameters.ValidateAudience = false;
    });

WebApplication app = builder.Build();

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapSwagger()
    .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme });
app.UseSwaggerUI();

app.Run();