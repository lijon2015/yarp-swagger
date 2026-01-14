using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace App1.Extensions;

public static class SwaggerGenOptionsExtensions
{
    public static SwaggerGenOptions AddSecurity(this SwaggerGenOptions options)
    {
        options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
        {
            In = ParameterLocation.Header,
            Description = "Please enter a valid token",
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            BearerFormat = "JWT",
            Scheme = JwtBearerDefaults.AuthenticationScheme
        });

        var securitySchemeReference = new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme);

        options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
        {
            { securitySchemeReference, new List<string>() }
        });

        return options;
    }
}
