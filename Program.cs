using BuildingCopilot.Api.ExceptionHandling;
using BuildingCopilot.Api.Authentication;
using BuildingCopilot.Application.Abstractions;
using BuildingCopilot.Application.Services;
using BuildingCopilot.Infrastructure;
using BuildingCopilot.Api.Mcp;
using ModelContextProtocol.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using KeycloakAuthenticationOptions = BuildingCopilot.Api.Authentication.AuthenticationOptions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
var authentication = builder.Configuration
    .GetSection(KeycloakAuthenticationOptions.SectionName)
    .Get<KeycloakAuthenticationOptions>() ?? new KeycloakAuthenticationOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authentication.Authority;
        if (!string.IsNullOrWhiteSpace(authentication.MetadataAddress))
        {
            options.MetadataAddress = authentication.MetadataAddress;
        }
        if (!string.IsNullOrWhiteSpace(authentication.BackchannelAuthority))
        {
            options.BackchannelHttpHandler = new OidcBackchannelHandler(
                new Uri(authentication.Authority),
                new Uri(authentication.BackchannelAuthority));
        }
        options.RequireHttpsMetadata = authentication.RequireHttpsMetadata;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = authentication.Issuer,
            ValidateAudience = true,
            ValidAudience = authentication.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = "preferred_username",
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddTransient<IClaimsTransformation, KeycloakClaimsTransformation>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.BuildingUser, policy =>
        policy.RequireAuthenticatedUser().RequireRole("building-user", "building-admin"));
    options.AddPolicy(AuthorizationPolicies.BuildingAdmin, policy =>
        policy.RequireAuthenticatedUser().RequireRole("building-admin"));
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireRole("building-user", "building-admin")
        .Build();
});
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IRagService, RagService>();
builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
    .WithTools<BuildingCopilotMcpTools>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers().RequireAuthorization(AuthorizationPolicies.BuildingUser);
app.MapMcp("/mcp").RequireAuthorization(AuthorizationPolicies.BuildingUser);

app.Run();

public partial class Program;
