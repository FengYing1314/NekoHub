using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using NekoHub.Infrastructure.DependencyInjection;
using NekoHub.Infrastructure.Persistence;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Infrastructure;

public class PersistenceProviderRegistrationTests
{
    [Fact]
    public void AddInfrastructure_Should_Reject_Sqlite_DbContext_When_Configured()
    {
        using var serviceProvider = CreateServiceProvider(new Dictionary<string, string?>
        {
            ["Persistence:Database:Provider"] = "sqlite",
            ["Persistence:Database:ConnectionString"] = "Data Source=:memory:",
            ["Storage:Provider"] = "local",
            ["Storage:Local:RootPath"] = Path.Combine(Path.GetTempPath(), "NekoHubTests", Guid.NewGuid().ToString())
        });

        using var scope = serviceProvider.CreateScope();
        var resolve = () => scope.ServiceProvider.GetRequiredService<AssetDbContext>();
        resolve.Should().Throw<InvalidOperationException>()
            .WithMessage("*PostgreSQL is the only supported provider*");
    }

    [Fact]
    public void AddInfrastructure_Should_Register_PostgreSql_DbContext_When_Configured()
    {
        using var serviceProvider = CreateServiceProvider(new Dictionary<string, string?>
        {
            ["Persistence:Database:Provider"] = "postgresql",
            ["Persistence:Database:ConnectionString"] = "Host=localhost;Database=nekohub_test;Username=test;Password=test",
            ["Storage:Provider"] = "local",
            ["Storage:Local:RootPath"] = Path.Combine(Path.GetTempPath(), "NekoHubTests", Guid.NewGuid().ToString())
        });

        using var scope = serviceProvider.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<AssetDbContext>();

        dbContext.Database.ProviderName.Should().Be("Npgsql.EntityFrameworkCore.PostgreSQL");
    }

    private static ServiceProvider CreateServiceProvider(IDictionary<string, string?> configValues)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        services.AddInfrastructure(configuration);

        return services.BuildServiceProvider(validateScopes: true);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "NekoHub.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
