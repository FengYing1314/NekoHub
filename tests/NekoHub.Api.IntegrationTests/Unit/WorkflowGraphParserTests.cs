using FluentAssertions;
using System.Text.Json.Nodes;
using NekoHub.Application.Common.Exceptions;
using NekoHub.Application.Workflows.Parsing;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Unit;

public class WorkflowGraphParserTests
{
    private readonly WorkflowGraphParser _parser = new();

    [Fact]
    public void ExtractSkills_Should_Preserve_Node_Order_And_Prefer_DataSkillId()
    {
        const string graphJson =
            """
            {
              "nodes": [
                { "id": "node-1", "type": "thumbnail" },
                { "id": "node-2", "type": "visual-node", "data": { "skillId": "ai-caption" } },
                { "id": "node-3", "type": "basic_image_enrich" }
              ]
            }
            """;

        var skills = _parser.ExtractSkills(graphJson);

        skills.Select(static skill => skill.SkillId)
            .Should()
            .Equal("thumbnail", "ai-caption", "basic_image_enrich");
    }

    [Fact]
    public void ExtractSkills_Should_Extract_Parameters_From_Node_Data()
    {
        const string graphJson =
            """
            {
              "nodes": [
                {
                  "id": "node-1",
                  "data": {
                    "skillId": "thumbnail",
                    "parameters": {
                      "width": 128,
                      "format": "png"
                    }
                  }
                },
                {
                  "id": "node-2",
                  "type": "ai-caption",
                  "data": {
                    "prompt": "describe briefly",
                    "temperature": 0.2
                  }
                }
              ]
            }
            """;

        var skills = _parser.ExtractSkills(graphJson);

        skills.Should().HaveCount(2);
        skills[0].SkillId.Should().Be("thumbnail");
        var thumbnailParameters = skills[0].Parameters;
        thumbnailParameters.Should().NotBeNull();
        thumbnailParameters!["width"]?.GetValue<int>().Should().Be(128);
        thumbnailParameters["format"]?.GetValue<string>().Should().Be("png");

        skills[1].SkillId.Should().Be("ai-caption");
        var captionParameters = skills[1].Parameters;
        captionParameters.Should().NotBeNull();
        captionParameters!["skillId"].Should().BeNull();
        captionParameters["prompt"]?.GetValue<string>().Should().Be("describe briefly");
        captionParameters["temperature"]?.GetValue<double>().Should().Be(0.2d);
    }

    [Fact]
    public void ExtractSkills_Should_Ignore_Nodes_Without_Executable_Skill_Metadata()
    {
        const string graphJson =
            """
            {
              "nodes": [
                { "id": "node-1" },
                { "id": "node-2", "data": { "label": "Display only" } },
                { "id": "node-3", "type": "thumbnail" }
              ]
            }
            """;

        var skills = _parser.ExtractSkills(graphJson);

        skills.Select(static skill => skill.SkillId).Should().Equal("thumbnail");
    }

    [Fact]
    public void ExtractSkills_With_Invalid_Json_Should_Throw_ValidationException()
    {
        const string graphJson = "{ invalid-json";

        var action = () => _parser.ExtractSkills(graphJson);

        action.Should()
            .Throw<ValidationException>()
            .WithMessage("*GraphJson must be a valid workflow graph JSON object.*");
    }
}
