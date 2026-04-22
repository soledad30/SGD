using System.Security.Cryptography;
using System.Text;
using GestorDocumentoApp.Controllers.Api;
using GestorDocumentoApp.Data;
using GestorDocumentoApp.Models;
using GestorDocumentoApp.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace GestorDocumentoApp.Tests;

public class GithubWebhookIntegrationTests
{
    [Fact]
    public async Task Webhook_Should_Return_Unauthorized_When_Signature_Invalid()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GitHub:WebhookSecret"] = "secret-ok"
            })
            .Build();
        var controller = CreateController(context, config);

        var payload = """{"action":"closed","number":12,"pull_request":{"number":12,"html_url":"https://github.com/owner/repo/pull/12","merged":true,"state":"closed"},"repository":{"full_name":"owner/repo"}}""";
        var httpContext = BuildHttpContext(payload, "pull_request", "sha256=invalid");
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.Webhook();

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Webhook_Should_Update_TraceLink_When_Signature_Valid()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        context.GitTraceLinks.Add(new GitTraceLink
        {
            Id = 1,
            ChangeRequestId = 99,
            Repository = "owner/repo",
            PullRequestNumber = 12,
            LinkedByUserId = "user-1",
            LinkedAt = DateTime.UtcNow.AddDays(-1)
        });
        await context.SaveChangesAsync();

        var secret = "secret-ok";
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GitHub:WebhookSecret"] = secret
            })
            .Build();
        var controller = CreateController(context, config);

        var payload = """{"action":"closed","number":12,"pull_request":{"number":12,"html_url":"https://github.com/owner/repo/pull/12","merged":true,"state":"closed"},"repository":{"full_name":"owner/repo"}}""";
        var signature = ComputeSignature(secret, payload);
        var httpContext = BuildHttpContext(payload, "pull_request", signature);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.Webhook();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode ?? 200);
        var link = await context.GitTraceLinks.AsNoTracking().FirstAsync(x => x.Id == 1);
        Assert.Equal("https://github.com/owner/repo/pull/12", link.PullRequestUrl);
        Assert.True(await context.ChangeRequestAudits.AnyAsync(x => x.ChangeRequestId == 99 && x.EventType == "Trazabilidad Git Webhook"));
    }

    private static GithubController CreateController(ScmDocumentContext context, IConfiguration configuration)
    {
        var githubService = new GithubService(
            configuration,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<GithubService>.Instance);
        return new GithubController(githubService, context, configuration);
    }

    private static ScmDocumentContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ScmDocumentContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ScmDocumentContext(options);
    }

    private static DefaultHttpContext BuildHttpContext(string payload, string eventName, string signature)
    {
        var httpContext = new DefaultHttpContext();
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        httpContext.Request.Body = stream;
        httpContext.Request.Headers["X-GitHub-Event"] = eventName;
        httpContext.Request.Headers["X-Hub-Signature-256"] = signature;
        return httpContext;
    }

    private static string ComputeSignature(string secret, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
