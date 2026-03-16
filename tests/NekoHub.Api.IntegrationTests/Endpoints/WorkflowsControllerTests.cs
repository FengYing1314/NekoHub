using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Api.IntegrationTests.Setup;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Endpoints;

public class WorkflowsControllerTests : IntegrationTestBase
{
    private const string BasePath = "/api/v1/system/workflows";

    public WorkflowsControllerTests(NekoHubApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Create_List_Get_And_Delete_Should_RoundTrip_Workflow_Profile()
    {
        var name = UniqueName("crud");
        const string description = "Roundtrip workflow profile";
        var graphJson = GraphJson("thumbnail");

        var createResponse = await Client.PostAsJsonAsync(BasePath, new WorkflowRequest(
            Name: name,
            Description: description,
            IsAutoRun: false,
            GraphJson: graphJson));

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await GetResponseDataAsync<WorkflowProfileResponse>(createResponse);
        created.Should().NotBeNull();
        created!.Name.Should().Be(name);
        created.Description.Should().Be(description);
        created.IsAutoRun.Should().BeFalse();
        created.GraphJson.Should().Be(graphJson);
        createResponse.Headers.Location.Should().NotBeNull();
        createResponse.Headers.Location!.ToString().Should().EndWith($"{BasePath}/{created.Id}");

        var listResponse = await Client.GetAsync(BasePath);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var workflows = await GetResponseDataAsync<List<WorkflowProfileResponse>>(listResponse);
        workflows.Should().NotBeNull();
        workflows!.Should().ContainSingle(item => item.Id == created.Id
            && item.Name == name
            && item.GraphJson == graphJson);

        var detailResponse = await Client.GetAsync($"{BasePath}/{created.Id}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await GetResponseDataAsync<WorkflowProfileResponse>(detailResponse);
        detail.Should().NotBeNull();
        detail!.Id.Should().Be(created.Id);
        detail.Name.Should().Be(name);
        detail.Description.Should().Be(description);
        detail.IsAutoRun.Should().BeFalse();
        detail.GraphJson.Should().Be(graphJson);

        var deleteResponse = await Client.DeleteAsync($"{BasePath}/{created.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterDeleteResponse = await Client.GetAsync($"{BasePath}/{created.Id}");
        afterDeleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await GetErrorAsync(afterDeleteResponse);
        error.Should().NotBeNull();
        error!.Code.Should().Be("workflow_profile_not_found");
    }

    [Fact]
    public async Task Update_Should_Replace_Workflow_Definition()
    {
        var created = await CreateWorkflowAsync(
            name: UniqueName("update-source"),
            description: "Initial description",
            isAutoRun: false,
            graphJson: GraphJson("thumbnail"));

        var updatedName = UniqueName("update-target");
        var updatedGraphJson = GraphJson("ai-caption");

        var updateResponse = await Client.PutAsJsonAsync($"{BasePath}/{created.Id}", new WorkflowRequest(
            Name: updatedName,
            Description: "Updated workflow profile",
            IsAutoRun: false,
            GraphJson: updatedGraphJson));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await GetResponseDataAsync<WorkflowProfileResponse>(updateResponse);
        updated.Should().NotBeNull();
        updated!.Id.Should().Be(created.Id);
        updated.Name.Should().Be(updatedName);
        updated.Description.Should().Be("Updated workflow profile");
        updated.IsAutoRun.Should().BeFalse();
        updated.GraphJson.Should().Be(updatedGraphJson);

        var detailResponse = await Client.GetAsync($"{BasePath}/{created.Id}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await GetResponseDataAsync<WorkflowProfileResponse>(detailResponse);
        detail.Should().NotBeNull();
        detail!.Name.Should().Be(updatedName);
        detail.Description.Should().Be("Updated workflow profile");
        detail.IsAutoRun.Should().BeFalse();
        detail.GraphJson.Should().Be(updatedGraphJson);
    }

    [Fact]
    public async Task Create_With_Duplicate_Name_Should_Return_BadRequest()
    {
        var name = UniqueName("duplicate-create");
        await CreateWorkflowAsync(
            name: name,
            description: "Original workflow",
            isAutoRun: false,
            graphJson: GraphJson("thumbnail"));

        var duplicateResponse = await Client.PostAsJsonAsync(BasePath, new WorkflowRequest(
            Name: name.ToUpperInvariant(),
            Description: "Conflicting workflow",
            IsAutoRun: false,
            GraphJson: GraphJson("ai-caption")));

        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await GetErrorAsync(duplicateResponse);
        error.Should().NotBeNull();
        error!.Code.Should().Be("workflow_profile_name_conflict");
        error.Message.Should().Contain("already exists");
        error.Status.Should().Be((int)HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_With_Duplicate_Name_Should_Return_BadRequest()
    {
        var existing = await CreateWorkflowAsync(
            name: UniqueName("duplicate-existing"),
            description: "Existing workflow",
            isAutoRun: false,
            graphJson: GraphJson("thumbnail"));

        var candidate = await CreateWorkflowAsync(
            name: UniqueName("duplicate-candidate"),
            description: "Candidate workflow",
            isAutoRun: false,
            graphJson: GraphJson("ai-caption"));

        var updateResponse = await Client.PutAsJsonAsync($"{BasePath}/{candidate.Id}", new WorkflowRequest(
            Name: existing.Name.ToUpperInvariant(),
            Description: "Should conflict",
            IsAutoRun: false,
            GraphJson: GraphJson("thumbnail")));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await GetErrorAsync(updateResponse);
        error.Should().NotBeNull();
        error!.Code.Should().Be("workflow_profile_name_conflict");

        var detailResponse = await Client.GetAsync($"{BasePath}/{candidate.Id}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await GetResponseDataAsync<WorkflowProfileResponse>(detailResponse);
        detail.Should().NotBeNull();
        detail!.Name.Should().Be(candidate.Name);
    }

    [Fact]
    public async Task Create_And_Update_With_Invalid_GraphJson_Should_Return_BadRequest_Without_Dirty_Write()
    {
        var createResponse = await Client.PostAsJsonAsync(BasePath, new WorkflowRequest(
            Name: UniqueName("invalid-create"),
            Description: "Invalid create payload",
            IsAutoRun: false,
            GraphJson: "{\"nodes\": ["));

        createResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var createError = await GetErrorAsync(createResponse);
        createError.Should().NotBeNull();
        createError!.Code.Should().Be("workflow_profile_graph_json_invalid");
        createError.Message.Should().Be("GraphJson must be a valid JSON object.");

        var created = await CreateWorkflowAsync(
            name: UniqueName("invalid-update"),
            description: "Before invalid update",
            isAutoRun: false,
            graphJson: GraphJson("thumbnail"));

        var updateResponse = await Client.PutAsJsonAsync($"{BasePath}/{created.Id}", new WorkflowRequest(
            Name: created.Name,
            Description: "Should not persist",
            IsAutoRun: true,
            GraphJson: "[]"));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var updateError = await GetErrorAsync(updateResponse);
        updateError.Should().NotBeNull();
        updateError!.Code.Should().Be("workflow_profile_graph_json_invalid");
        updateError.Message.Should().Be("GraphJson must be a valid JSON object.");

        var detailResponse = await Client.GetAsync($"{BasePath}/{created.Id}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await GetResponseDataAsync<WorkflowProfileResponse>(detailResponse);
        detail.Should().NotBeNull();
        detail!.Name.Should().Be(created.Name);
        detail.Description.Should().Be(created.Description);
        detail.IsAutoRun.Should().Be(created.IsAutoRun);
        detail.GraphJson.Should().Be(created.GraphJson);
    }

    [Fact]
    public async Task Patch_AutoRun_Should_Switch_Current_AutoRun_Workflow()
    {
        var first = await CreateWorkflowAsync(
            name: UniqueName("autorun-first"),
            description: "First autorun workflow",
            isAutoRun: true,
            graphJson: GraphJson("thumbnail"));

        var second = await CreateWorkflowAsync(
            name: UniqueName("autorun-second"),
            description: "Second workflow",
            isAutoRun: false,
            graphJson: GraphJson("ai-caption"));

        var setAutoRunResponse = await PatchAsync($"{BasePath}/{second.Id}/autorun");
        setAutoRunResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var currentAutoRun = await GetResponseDataAsync<WorkflowProfileResponse>(setAutoRunResponse);
        currentAutoRun.Should().NotBeNull();
        currentAutoRun!.Id.Should().Be(second.Id);
        currentAutoRun.IsAutoRun.Should().BeTrue();

        var firstDetail = await GetWorkflowAsync(first.Id);
        firstDetail.Should().NotBeNull();
        firstDetail!.IsAutoRun.Should().BeFalse();

        var secondDetail = await GetWorkflowAsync(second.Id);
        secondDetail.Should().NotBeNull();
        secondDetail!.IsAutoRun.Should().BeTrue();

        var listAfterPatch = await ListWorkflowsAsync();
        listAfterPatch.Should().ContainSingle(item => item.Id == second.Id && item.IsAutoRun);
        listAfterPatch.Count(item => item.IsAutoRun).Should().Be(1);
    }

    [Fact]
    public async Task Update_IsAutoRun_False_Should_Clear_Current_AutoRun_Workflow()
    {
        var workflow = await CreateWorkflowAsync(
            name: UniqueName("autorun-clear"),
            description: "Workflow to clear autorun",
            isAutoRun: true,
            graphJson: GraphJson("thumbnail"));

        var clearAutoRunResponse = await Client.PutAsJsonAsync($"{BasePath}/{workflow.Id}", new WorkflowRequest(
            Name: workflow.Name,
            Description: workflow.Description,
            IsAutoRun: false,
            GraphJson: workflow.GraphJson));

        clearAutoRunResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var cleared = await GetResponseDataAsync<WorkflowProfileResponse>(clearAutoRunResponse);
        cleared.Should().NotBeNull();
        cleared!.IsAutoRun.Should().BeFalse();

        var detail = await GetWorkflowAsync(workflow.Id);
        detail.Should().NotBeNull();
        detail!.IsAutoRun.Should().BeFalse();

        var listAfterClear = await ListWorkflowsAsync();
        listAfterClear.Should().NotContain(item => item.IsAutoRun);
    }

    private async Task<WorkflowProfileResponse> CreateWorkflowAsync(
        string name,
        string? description,
        bool isAutoRun,
        string graphJson)
    {
        var response = await Client.PostAsJsonAsync(BasePath, new WorkflowRequest(
            Name: name,
            Description: description,
            IsAutoRun: isAutoRun,
            GraphJson: graphJson));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var workflow = await GetResponseDataAsync<WorkflowProfileResponse>(response);
        workflow.Should().NotBeNull();
        return workflow!;
    }

    private async Task<List<WorkflowProfileResponse>> ListWorkflowsAsync()
    {
        var response = await Client.GetAsync(BasePath);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var workflows = await GetResponseDataAsync<List<WorkflowProfileResponse>>(response);
        workflows.Should().NotBeNull();
        return workflows!;
    }

    private async Task<WorkflowProfileResponse?> GetWorkflowAsync(Guid id)
    {
        var response = await Client.GetAsync($"{BasePath}/{id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await GetResponseDataAsync<WorkflowProfileResponse>(response);
    }

    private async Task<HttpResponseMessage> PatchAsync(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, path);
        return await Client.SendAsync(request);
    }

    private static string UniqueName(string prefix)
    {
        return $"workflow-{prefix}-{Guid.NewGuid():N}";
    }

    private static string GraphJson(string skillId)
    {
        return $"{{\"nodes\":[{{\"id\":\"node-1\",\"data\":{{\"skillId\":\"{skillId}\"}}}}]}}";
    }

    private sealed record WorkflowRequest(
        string Name,
        string? Description,
        bool? IsAutoRun,
        string GraphJson);
}
