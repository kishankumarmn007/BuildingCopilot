using BuildingCopilot.Api.ExceptionHandling;
using BuildingCopilot.Application.Abstractions;
using BuildingCopilot.Application.Services;
using BuildingCopilot.Infrastructure;
using BuildingCopilot.Api.Mcp;
using ModelContextProtocol.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
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

app.MapControllers();
app.MapMcp("/mcp");

app.Run();

public partial class Program;
