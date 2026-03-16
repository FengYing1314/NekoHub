using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NekoHub.Application.Abstractions.Storage;
using NekoHub.Domain.Storage;
using NekoHub.Infrastructure.Options;
using NekoHub.Infrastructure.Storage;
using NekoHub.Infrastructure.Storage.Local;
using NekoHub.Infrastructure.Storage.S3;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Infrastructure;

public class StorageContractTests
{
    [Theory]
    [InlineData(StorageProvider.Local, "local")]
    [InlineData(StorageProvider.S3Compatible, "s3")]
    [InlineData(StorageProvider.GitHubRepo, "github-repo")]
    public void StorageProvider_Should_Map_To_Correct_Name(StorageProvider provider, string expectedName)
    {
        // 1. StorageProvider 的名称映射稳定
        provider.ToProviderName().Should().Be(expectedName);
    }

    [Theory]
    [InlineData("local", StorageProvider.Local, true)]
    [InlineData("LOCAL", StorageProvider.Local, true)] // 验证大小写不敏感
    [InlineData("Local", StorageProvider.Local, true)]
    [InlineData("s3", StorageProvider.S3Compatible, true)]
    [InlineData("S3", StorageProvider.S3Compatible, true)]
    [InlineData("github-repo", StorageProvider.GitHubRepo, true)]
    [InlineData("GITHUB-REPO", StorageProvider.GitHubRepo, true)]
    [InlineData("unknown", default(StorageProvider), false)]
    [InlineData(null, default(StorageProvider), false)]
    public void StorageProvider_TryParseProvider_Should_Work_Correctly(string? input, StorageProvider expectedProvider, bool expectedResult)
    {
        // 1. 不允许出现大小写或字符串漂移导致的隐式不兼容
        var result = StorageProviderExtensions.TryParseProvider(input, out var provider);
        
        result.Should().Be(expectedResult);
        if (result)
        {
            provider.Should().Be(expectedProvider);
        }
    }

    [Fact]
    public void LocalAssetStorage_Should_Expose_Correct_ProviderName()
    {
        var optionsWrapper = Options.Create(new StorageOptions());
        var localOptions = Options.Create(new LocalStorageOptions { RootPath = "test_path" });
        var localAssetStorage = new LocalAssetStorage(localOptions, optionsWrapper);

        localAssetStorage.ProviderName.Should().Be("local");
        localAssetStorage.ProviderType.Should().Be(StorageProviderTypes.Local);
        localAssetStorage.Capabilities.Should().Be(StorageProviderCapabilityCatalog.GetRequired(StorageProviderTypes.Local));
    }

    [Fact]
    public void Resolver_Should_Resolve_Local_By_Default_When_Configured()
    {
        // 2. Resolver 的默认 provider 解析行为
        var resolver = CreateResolver(new StorageOptions { Provider = "local" });
        
        var defaultStorage = resolver.ResolveDefault();
        defaultStorage.Should().BeOfType<LocalAssetStorage>();
    }

    [Fact]
    public void Resolver_Should_Fail_Explicitly_For_Unknown_Default_Provider()
    {
        // 2. 当配置为未知 provider 时应明确失败，而不是静默回退
        var resolver = CreateResolver(new StorageOptions { Provider = "s3" });
        
        var action = () => resolver.ResolveDefault();
        
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Unsupported default storage provider 's3'. Currently supported providers: local.");
    }

    [Theory]
    [InlineData("local")]
    [InlineData("LOCAL")]
    public void Resolver_Should_Resolve_By_Name_Correctly(string providerName)
    {
        // 3. Resolver 的按名称解析行为：传入 "local" 时返回 LocalAssetStorage
        var resolver = CreateResolver(new StorageOptions { Provider = "local" });
        
        var storage = resolver.Resolve(providerName);
        storage.Should().BeOfType<LocalAssetStorage>();
    }

    [Fact]
    public void Resolver_Should_Fail_Explicitly_For_Unknown_Name()
    {
        // 3. 传入未知 provider 时应明确失败
        var resolver = CreateResolver(new StorageOptions { Provider = "local" });
        
        var action = () => resolver.Resolve("azure_blob");
        
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Unsupported storage provider 'azure_blob'. Currently supported providers: local.");
    }

    [Fact]
    public void Resolver_Should_Resolve_By_Enum_Correctly()
    {
        var resolver = CreateResolver(new StorageOptions { Provider = "local" });
        
        var storage = resolver.Resolve(StorageProvider.Local);
        storage.Should().BeOfType<LocalAssetStorage>();
    }

    [Fact]
    public void S3AssetStorage_Should_Expose_Correct_ProviderName()
    {
        var s3Options = Options.Create(new S3StorageOptions { ProviderName = "my_s3" });
        var storageOptions = Options.Create(new StorageOptions { PublicBaseUrl = "http://test-server/content" });
        var s3Storage = new S3AssetStorage(s3Options, storageOptions);

        s3Storage.ProviderName.Should().Be("my_s3");
        s3Storage.ProviderType.Should().Be(StorageProviderTypes.S3Compatible);
        s3Storage.Capabilities.Should().Be(StorageProviderCapabilityCatalog.GetRequired(StorageProviderTypes.S3Compatible));
    }

    [Fact]
    public void CapabilityCatalog_Should_Define_Expected_Local_S3_And_GitHub_Matrix()
    {
        var local = StorageProviderCapabilityCatalog.GetRequired(StorageProviderTypes.Local);
        local.SupportsPublicRead.Should().BeTrue();
        local.SupportsPrivateRead.Should().BeTrue();
        local.SupportsVisibilityToggle.Should().BeTrue();
        local.SupportsDelete.Should().BeTrue();
        local.SupportsDirectPublicUrl.Should().BeFalse();
        local.RequiresAccessProxy.Should().BeTrue();
        local.RecommendedForPrimaryStorage.Should().BeTrue();
        local.IsPlatformBacked.Should().BeFalse();
        local.IsExperimental.Should().BeFalse();
        local.RequiresTokenForPrivateRead.Should().BeFalse();

        var s3 = StorageProviderCapabilityCatalog.GetRequired(StorageProviderTypes.S3Compatible);
        s3.SupportsPublicRead.Should().BeTrue();
        s3.SupportsPrivateRead.Should().BeTrue();
        s3.SupportsVisibilityToggle.Should().BeTrue();
        s3.SupportsDelete.Should().BeTrue();
        s3.SupportsDirectPublicUrl.Should().BeTrue();
        s3.RequiresAccessProxy.Should().BeFalse();
        s3.RecommendedForPrimaryStorage.Should().BeTrue();
        s3.IsPlatformBacked.Should().BeFalse();
        s3.IsExperimental.Should().BeFalse();
        s3.RequiresTokenForPrivateRead.Should().BeFalse();

        var githubReleases = StorageProviderCapabilityCatalog.GetRequired(StorageProviderTypes.GitHubReleases);
        githubReleases.SupportsPublicRead.Should().BeTrue();
        githubReleases.SupportsPrivateRead.Should().BeFalse();
        githubReleases.SupportsVisibilityToggle.Should().BeFalse();
        githubReleases.SupportsDelete.Should().BeFalse();
        githubReleases.SupportsDirectPublicUrl.Should().BeTrue();
        githubReleases.RequiresAccessProxy.Should().BeFalse();
        githubReleases.RecommendedForPrimaryStorage.Should().BeFalse();
        githubReleases.IsPlatformBacked.Should().BeTrue();
        githubReleases.IsExperimental.Should().BeTrue();
        githubReleases.RequiresTokenForPrivateRead.Should().BeFalse();

        var githubRepo = StorageProviderCapabilityCatalog.GetRequired(StorageProviderTypes.GitHubRepo);
        githubRepo.SupportsPublicRead.Should().BeTrue();
        githubRepo.SupportsPrivateRead.Should().BeTrue();
        githubRepo.SupportsVisibilityToggle.Should().BeFalse();
        githubRepo.SupportsDelete.Should().BeTrue();
        githubRepo.SupportsDirectPublicUrl.Should().BeTrue();
        githubRepo.RequiresAccessProxy.Should().BeTrue();
        githubRepo.RecommendedForPrimaryStorage.Should().BeFalse();
        githubRepo.IsPlatformBacked.Should().BeTrue();
        githubRepo.IsExperimental.Should().BeTrue();
        githubRepo.RequiresTokenForPrivateRead.Should().BeTrue();
    }

    [Fact]
    public void Resolver_Should_Resolve_S3_When_Registered()
    {
        var optionsWrapper = Options.Create(new StorageOptions { Provider = "s3" });
        var localOptions = Options.Create(new LocalStorageOptions { RootPath = "test_path" });
        var s3Options = Options.Create(new S3StorageOptions { ProviderName = "s3" });
        
        var localAssetStorage = new LocalAssetStorage(localOptions, optionsWrapper);
        var s3AssetStorage = new S3AssetStorage(s3Options, optionsWrapper);
        
        var resolver = new AssetStorageResolver(new IAssetStorage[] { localAssetStorage, s3AssetStorage }, optionsWrapper);

        var defaultStorage = resolver.ResolveDefault();
        defaultStorage.Should().BeOfType<S3AssetStorage>();

        var resolvedByName = resolver.Resolve("s3");
        resolvedByName.Should().BeOfType<S3AssetStorage>();

        var resolvedLocal = resolver.Resolve("local");
        resolvedLocal.Should().BeOfType<LocalAssetStorage>();
    }

    [Fact]
    public async Task S3AssetStorage_EnsureConfigured_Should_Fail_When_Options_Missing()
    {
        var s3Options = Options.Create(new S3StorageOptions { ProviderName = "s3" }); // Missing Endpoint, Bucket, etc.
        var storageOptions = Options.Create(new StorageOptions { PublicBaseUrl = "http://test-server/content" });
        var s3Storage = new S3AssetStorage(s3Options, storageOptions);

        Func<Task> action = async () => await s3Storage.GetPublicUrlAsync("test.png");

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Storage:S3:Endpoint is required.");
    }

    // Helper method to create a resolver instance for testing
    private AssetStorageResolver CreateResolver(StorageOptions options)
    {
        var optionsWrapper = Options.Create(options);
        var localOptions = Options.Create(new LocalStorageOptions { RootPath = "test_path" });
        var localAssetStorage = new LocalAssetStorage(localOptions, optionsWrapper);
        return new AssetStorageResolver(new IAssetStorage[] { localAssetStorage }, optionsWrapper);
    }
}
