using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CrewPilot.Api.Contracts;
using FluentAssertions;
using Xunit;

namespace CrewPilot.IntegrationTests;

public sealed class AttachmentsApiTests(CrewPilotApiFactory factory) : IClassFixture<CrewPilotApiFactory>
{
    [Fact]
    public async Task UploadDownloadAndDelete_RoundTripsFileContent()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);
        var workItem = await ApiTestHelpers.CreateRequirementAsync(client, project.Id);
        var bytes = System.Text.Encoding.UTF8.GetBytes("这是一份测试报告\nline 2");

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(fileContent, "file", "report.txt");

        var upload = await client.PostAsync(
            $"/api/workitems/{workItem.Id}/attachments", form);
        upload.StatusCode.Should().Be(HttpStatusCode.Created);
        var attachment = await upload.Content.ReadFromJsonAsync<AttachmentDto>();
        attachment!.FileName.Should().Be("report.txt");
        attachment.Size.Should().Be(bytes.Length);

        var list = await client.GetFromJsonAsync<List<AttachmentDto>>(
            $"/api/workitems/{workItem.Id}/attachments");
        list.Should().Contain(a => a.Id == attachment.Id);

        var download = await client.GetAsync($"/api/attachments/{attachment.Id}/download");
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        (await download.Content.ReadAsByteArrayAsync()).Should().Equal(bytes);

        var deleted = await client.DeleteAsync($"/api/attachments/{attachment.Id}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterDelete = await client.GetAsync($"/api/attachments/{attachment.Id}/download");
        afterDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Upload_WithEmptyFile_Returns400()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);
        var workItem = await ApiTestHelpers.CreateRequirementAsync(client, project.Id);

        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent([]), "file", "empty.txt");

        var response = await client.PostAsync(
            $"/api/workitems/{workItem.Id}/attachments", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_ForUnknownWorkItem_Returns404()
    {
        var client = factory.CreateClient();

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([1, 2, 3]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(fileContent, "file", "bin.dat");

        var response = await client.PostAsync(
            $"/api/workitems/{Guid.NewGuid()}/attachments", form);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
