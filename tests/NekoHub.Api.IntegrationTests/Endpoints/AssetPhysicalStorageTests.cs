using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using NekoHub.Api.Contracts.Responses;
using NekoHub.Api.IntegrationTests.Setup;
using Xunit;

namespace NekoHub.Api.IntegrationTests.Endpoints;

public class AssetPhysicalStorageTests : IntegrationTestBase
{
    public AssetPhysicalStorageTests(NekoHubApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Asset_Physical_File_Lifecycle_And_Repeated_Deletes()
    {
        // 1. 上传后物理文件存在
        var uploadResponse = await UploadTestImage("test_physical.png", "image/png", new byte[100]);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        
        var asset = await GetResponseDataAsync<AssetResponse>(uploadResponse);
        var assetId = asset!.Id;
        var storageKey = asset.StorageKey;
        
        // 构建物理文件路径 (LocalAssetStorage 直接组合 rootPath 和 storageKey)
        var physicalPath = Path.Combine(Factory.TestStoragePath, storageKey.Replace('\\', '/').TrimStart('/'));
        File.Exists(physicalPath).Should().BeTrue("上传后，物理文件应当存在于磁盘上");

        // DELETE 操作
        var deleteResponse = await Client.DeleteAsync($"/api/v1/assets/{assetId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 验证 DELETE 后物理文件确实消失
        File.Exists(physicalPath).Should().BeFalse("删除接口被调用后，物理文件应当被清理");

        // 2. DELETE 后再次访问 content 返回 404 + asset_not_found
        var contentResponse = await Client.GetAsync($"/api/v1/assets/{assetId}/content");
        contentResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var contentError = await GetErrorAsync(contentResponse);
        contentError!.Code.Should().Be("asset_not_found");

        // 3. 同一资产重复 DELETE：第一次成功，第二次 404 + asset_not_found
        var secondDeleteResponse = await Client.DeleteAsync($"/api/v1/assets/{assetId}");
        secondDeleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var deleteError = await GetErrorAsync(secondDeleteResponse);
        deleteError!.Code.Should().Be("asset_not_found");
    }

    [Fact]
    public async Task Manual_Physical_File_Deletion_Should_Not_Break_Api_Delete()
    {
        // 4. 底层物理文件被手动删除后，再调用 DELETE，系统应仍能收敛到已删除状态
        var uploadResponse = await UploadTestImage("test_manual_del.png", "image/png", new byte[50]);
        var asset = await GetResponseDataAsync<AssetResponse>(uploadResponse);
        var assetId = asset!.Id;
        var physicalPath = Path.Combine(Factory.TestStoragePath, asset.StorageKey.Replace('\\', '/').TrimStart('/'));

        // 模拟外部意外手动删除了物理文件
        File.Delete(physicalPath);
        File.Exists(physicalPath).Should().BeFalse();

        // 再次调用 API 的 DELETE
        var deleteResponse = await Client.DeleteAsync($"/api/v1/assets/{assetId}");
        
        // 这里验证：物理文件丢失不应该导致数据库删除失败（即应该返回 OK 而不是抛出 500 异常）
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK, "即使用户手动删除了物理文件，API Delete 也应当成功回收数据库记录并收敛状态");
    }

    private async Task<HttpResponseMessage> UploadTestImage(string fileName, string contentType, byte[] content)
    {
        using var requestContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        requestContent.Add(fileContent, "File", fileName);

        return await Client.PostAsync("/api/v1/assets", requestContent);
    }
}
