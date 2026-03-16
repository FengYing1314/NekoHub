using NekoHub.Api.Extensions;
using NekoHub.Application.DependencyInjection;
using NekoHub.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApiLayer(builder.Configuration)
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseApiLayer();

app.Run();

public partial class Program;
