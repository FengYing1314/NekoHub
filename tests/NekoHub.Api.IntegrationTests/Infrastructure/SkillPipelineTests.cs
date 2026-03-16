using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NekoHub.Api.IntegrationTests.Setup;
using NekoHub.Application.Abstractions.Processing;
using NekoHub.Application.Abstractions.Skills;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Infrastructure;

public class SkillPipelineTests : IClassFixture<NekoHubApplicationFactory>
{
    private readonly NekoHubApplicationFactory _factory;

    public SkillPipelineTests(NekoHubApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Skill_Definition_Should_Expose_Basic_Image_Enrich_Pipeline()
    {
        using var scope = _factory.Services.CreateScope();
        var definitionProvider = scope.ServiceProvider.GetRequiredService<IAssetSkillDefinitionProvider>();

        var definitions = definitionProvider.GetForAssetCreated(new AssetCreatedProcessingContext(
            AssetId: Guid.CreateVersion7(),
            StorageProvider: "local",
            StorageKey: "test/image.png",
            ContentType: "image/png",
            Extension: ".png",
            Size: 128,
            Width: 1,
            Height: 1,
            ChecksumSha256: null,
            PublicUrl: null,
            CreatedAtUtc: DateTimeOffset.UtcNow));

        definitions.Should().ContainSingle(definition => definition.Name == "basic_image_enrich");
        var skill = definitions.Single(definition => definition.Name == "basic_image_enrich");
        skill.Description.Should().NotBeNullOrWhiteSpace();
        skill.Steps.Select(static step => step.Name)
            .Should()
            .Equal("generate_thumbnail", "generate_basic_caption");
    }

    [Fact]
    public void Skill_Definition_Should_Expose_Exif_Strip_As_Standalone_Skill()
    {
        using var scope = _factory.Services.CreateScope();
        var definitionProvider = scope.ServiceProvider.GetRequiredService<IAssetSkillDefinitionProvider>();

        var definitions = definitionProvider.GetAll();

        definitions.Should().ContainSingle(definition => definition.Name == "exif-strip");
        var skill = definitions.Single(definition => definition.Name == "exif-strip");
        skill.Description.Should().NotBeNullOrWhiteSpace();
        skill.Steps.Select(static step => step.Name)
            .Should()
            .Equal("strip_exif_metadata");
    }

    [Fact]
    public void Skill_Definition_Should_Expose_Format_Convert_As_Standalone_Skill()
    {
        using var scope = _factory.Services.CreateScope();
        var definitionProvider = scope.ServiceProvider.GetRequiredService<IAssetSkillDefinitionProvider>();

        var definitions = definitionProvider.GetAll();

        definitions.Should().ContainSingle(definition => definition.Name == "format-convert");
        var skill = definitions.Single(definition => definition.Name == "format-convert");
        skill.Description.Should().NotBeNullOrWhiteSpace();
        skill.Steps.Select(static step => step.Name)
            .Should()
            .Equal("convert_image_format");
    }

    [Fact]
    public void Skill_Definition_Should_Expose_Watermark_As_Standalone_Skill()
    {
        using var scope = _factory.Services.CreateScope();
        var definitionProvider = scope.ServiceProvider.GetRequiredService<IAssetSkillDefinitionProvider>();

        var definitions = definitionProvider.GetAll();

        definitions.Should().ContainSingle(definition => definition.Name == "watermark");
        var skill = definitions.Single(definition => definition.Name == "watermark");
        skill.Description.Should().NotBeNullOrWhiteSpace();
        skill.Steps.Select(static step => step.Name)
            .Should()
            .Equal("draw_watermark");
    }
}
