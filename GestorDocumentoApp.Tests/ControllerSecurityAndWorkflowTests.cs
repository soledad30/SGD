using System.Security.Claims;
using GestorDocumentoApp.Controllers;
using GestorDocumentoApp.Data;
using GestorDocumentoApp.Models;
using GestorDocumentoApp.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GestorDocumentoApp.Tests;

public class ControllerSecurityAndWorkflowTests
{
    [Fact]
    public async Task ChangeRequest_Edit_Should_Reject_Invalid_Status_Transition()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var userId = "user-1";

        var project = new Project { Id = 1, Name = "P1", UserId = userId, CreationDate = DateTime.UtcNow };
        var element = new Element { Id = 1, Name = "E1", ProjectId = 1, CreatedDate = DateTime.UtcNow };
        var cr = new ChangeRequest
        {
            Id = 1,
            ElementId = 1,
            Code = "CR-1",
            ClasificationType = ClasificationTypeCR.BugFixing,
            Priority = PriorityCR.Urgent,
            Status = StatusCR.Initiated,
            CreatedAt = DateTime.UtcNow
        };

        context.Projects.Add(project);
        context.Elements.Add(element);
        context.ChangeRequests.Add(cr);
        await context.SaveChangesAsync();

        var controller = new ChangeRequestController(context, new GestorDocumentoApp.Services.ChangeRequestLifecycleService(context), CreateGithubService());
        SetAuthenticatedUser(controller, userId);

        var vm = new ChangeRequestCreateVM
        {
            ElementId = 1,
            Code = "CR-1",
            ClasificationType = ClasificationTypeCR.BugFixing,
            Priority = PriorityCR.Urgent,
            Status = StatusCR.Analyzed, // invalid jump: Initiated -> Analyzed
            Action = ActionCR.InWait
        };

        var result = await controller.Edit(1, vm);

        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey(nameof(vm.Status)));
        Assert.Same(vm, view.Model);
    }

    [Fact]
    public async Task ChangeRequest_Create_Should_Return_Forbid_When_Element_Not_Owner()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);

        var ownerProject = new Project { Id = 10, Name = "OwnerP", UserId = "owner-user", CreationDate = DateTime.UtcNow };
        var ownerElement = new Element { Id = 10, Name = "OwnerE", ProjectId = 10, CreatedDate = DateTime.UtcNow };
        context.Projects.Add(ownerProject);
        context.Elements.Add(ownerElement);
        await context.SaveChangesAsync();

        var controller = new ChangeRequestController(context, new GestorDocumentoApp.Services.ChangeRequestLifecycleService(context), CreateGithubService());
        SetAuthenticatedUser(controller, "different-user");

        var vm = new ChangeRequestCreateVM
        {
            ElementId = 10,
            Code = "CR-10",
            ClasificationType = ClasificationTypeCR.Other,
            Priority = PriorityCR.Desirable,
            Status = StatusCR.Received,
            Action = ActionCR.InWait
        };

        var result = await controller.Create(vm);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Project_Show_Should_Return_NotFound_When_Project_Not_Owner()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);

        context.Projects.Add(new Project
        {
            Id = 99,
            Name = "Private Project",
            UserId = "owner-user",
            CreationDate = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var controller = new ProjectController(context, NullLogger<ElementTypeController>.Instance);
        SetAuthenticatedUser(controller, "another-user");

        var result = await controller.Show(99);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ChangeRequest_Approve_Should_Return_Forbid_When_User_Is_Not_Assignee()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);

        var ownerUser = "owner-user";
        var project = new Project { Id = 50, Name = "P", UserId = ownerUser, CreationDate = DateTime.UtcNow };
        var element = new Element { Id = 50, Name = "E", ProjectId = 50, CreatedDate = DateTime.UtcNow };
        var cr = new ChangeRequest
        {
            Id = 50,
            ElementId = 50,
            Code = "CR-50",
            ClasificationType = ClasificationTypeCR.Enhancement,
            Priority = PriorityCR.AsSoonAsPossible,
            Status = StatusCR.Reviewed,
            Action = ActionCR.InWait,
            ApprovalStatus = ApprovalStatus.Pending,
            ApprovalAssigneeUserId = "approver-user",
            ApprovalRequestedAt = DateTime.UtcNow.AddHours(-1),
            ApprovalDueAt = DateTime.UtcNow.AddHours(23),
            CreatedAt = DateTime.UtcNow
        };

        context.Projects.Add(project);
        context.Elements.Add(element);
        context.ChangeRequests.Add(cr);
        await context.SaveChangesAsync();

        var controller = new ChangeRequestController(context, new GestorDocumentoApp.Services.ChangeRequestLifecycleService(context), CreateGithubService());
        SetAuthenticatedUser(controller, ownerUser);

        var result = await controller.Approve(50);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task ChangeRequest_AddGitTrace_Should_Reject_Version_Outside_ChangeRequest()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);

        var userId = "owner-user";
        var project = new Project { Id = 70, Name = "P70", UserId = userId, CreationDate = DateTime.UtcNow };
        var element = new Element { Id = 70, Name = "E70", ProjectId = 70, CreatedDate = DateTime.UtcNow };
        var cr = new ChangeRequest
        {
            Id = 70,
            ElementId = 70,
            Code = "CR-70",
            ClasificationType = ClasificationTypeCR.Other,
            Priority = PriorityCR.Desirable,
            Status = StatusCR.Received,
            CreatedAt = DateTime.UtcNow
        };
        var unrelatedCr = new ChangeRequest
        {
            Id = 71,
            ElementId = 70,
            Code = "CR-71",
            ClasificationType = ClasificationTypeCR.Other,
            Priority = PriorityCR.Desirable,
            Status = StatusCR.Received,
            CreatedAt = DateTime.UtcNow
        };
        var foreignVersion = new GestorDocumentoApp.Models.Version
        {
            Id = 500,
            Name = "v500",
            VersionCode = "1.0.0",
            ElementId = 70,
            UserId = userId,
            RequirementTypeId = 1,
            ChangeRequestId = 71,
            UploadDate = DateTime.UtcNow,
            State = "inactive"
        };
        context.RequirementTypes.Add(new RequirementType { Id = 1, Name = "Req" });
        context.Projects.Add(project);
        context.Elements.Add(element);
        context.ChangeRequests.Add(cr);
        context.ChangeRequests.Add(unrelatedCr);
        context.Versions.Add(foreignVersion);
        await context.SaveChangesAsync();

        var controller = new ChangeRequestController(context, new GestorDocumentoApp.Services.ChangeRequestLifecycleService(context), CreateGithubService());
        SetAuthenticatedUser(controller, userId);

        var result = await controller.AddGitTrace(70, "org/repo", "abc123", null, null, 500);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
        Assert.Empty(context.GitTraceLinks);
    }

    [Fact]
    public async Task ChangeRequest_TraceabilityReport_Should_Return_NotFound_When_Not_Owner()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);

        context.Projects.Add(new Project
        {
            Id = 801,
            Name = "Private project",
            UserId = "owner-user",
            CreationDate = DateTime.UtcNow
        });
        context.Elements.Add(new Element
        {
            Id = 801,
            Name = "Private element",
            ProjectId = 801,
            CreatedDate = DateTime.UtcNow
        });
        context.ChangeRequests.Add(new ChangeRequest
        {
            Id = 801,
            ElementId = 801,
            Code = "CR-801",
            ClasificationType = ClasificationTypeCR.BugFixing,
            Priority = PriorityCR.Urgent,
            Status = StatusCR.Received,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var controller = new ChangeRequestController(context, new GestorDocumentoApp.Services.ChangeRequestLifecycleService(context), CreateGithubService());
        SetAuthenticatedUser(controller, "other-user");

        var result = await controller.TraceabilityReport(801);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ChangeRequest_Edit_Should_Reject_Baselined_When_No_Git_Trace()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var userId = "owner-user";

        context.Projects.Add(new Project
        {
            Id = 811,
            Name = "P811",
            UserId = userId,
            CreationDate = DateTime.UtcNow
        });
        context.Elements.Add(new Element
        {
            Id = 811,
            Name = "E811",
            ProjectId = 811,
            CreatedDate = DateTime.UtcNow
        });
        context.ChangeRequests.Add(new ChangeRequest
        {
            Id = 811,
            ElementId = 811,
            Code = "CR-811",
            ClasificationType = ClasificationTypeCR.BugFixing,
            Priority = PriorityCR.Urgent,
            Status = StatusCR.Checkin,
            Action = ActionCR.Approved,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var controller = new ChangeRequestController(context, new GestorDocumentoApp.Services.ChangeRequestLifecycleService(context), CreateGithubService());
        SetAuthenticatedUser(controller, userId);

        var vm = new ChangeRequestCreateVM
        {
            ElementId = 811,
            Code = "CR-811",
            ClasificationType = ClasificationTypeCR.BugFixing,
            Priority = PriorityCR.Urgent,
            Status = StatusCR.Baselined,
            Action = ActionCR.Approved
        };

        var result = await controller.Edit(811, vm);

        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey(nameof(vm.Status)));
        Assert.Same(vm, view.Model);
    }

    [Fact]
    public async Task ChangeRequest_Edit_Should_Reject_Baselined_When_Pr_Not_Merged()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var userId = "owner-user";

        context.Projects.Add(new Project
        {
            Id = 821,
            Name = "P821",
            UserId = userId,
            CreationDate = DateTime.UtcNow
        });
        context.Elements.Add(new Element
        {
            Id = 821,
            Name = "E821",
            ProjectId = 821,
            CreatedDate = DateTime.UtcNow
        });
        context.ChangeRequests.Add(new ChangeRequest
        {
            Id = 821,
            ElementId = 821,
            Code = "CR-821",
            ClasificationType = ClasificationTypeCR.BugFixing,
            Priority = PriorityCR.Urgent,
            Status = StatusCR.Checkin,
            Action = ActionCR.Approved,
            CreatedAt = DateTime.UtcNow
        });
        context.GitTraceLinks.Add(new GitTraceLink
        {
            ChangeRequestId = 821,
            Repository = "owner/repo",
            PullRequestNumber = 10,
            LinkedByUserId = userId,
            LinkedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var github = new FakeGithubService(
            repositoryExists: true,
            commitExists: false,
            pullRequestMerged: false);
        var controller = new ChangeRequestController(context, new GestorDocumentoApp.Services.ChangeRequestLifecycleService(context), github);
        SetAuthenticatedUser(controller, userId);

        var vm = new ChangeRequestCreateVM
        {
            ElementId = 821,
            Code = "CR-821",
            ClasificationType = ClasificationTypeCR.BugFixing,
            Priority = PriorityCR.Urgent,
            Status = StatusCR.Baselined,
            Action = ActionCR.Approved
        };

        var result = await controller.Edit(821, vm);

        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey(nameof(vm.Status)));
        Assert.Same(vm, view.Model);
    }

    [Fact]
    public async Task ChangeRequest_Edit_Should_Allow_Baselined_When_Commit_Exists_In_Github()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var userId = "owner-user";

        context.Projects.Add(new Project
        {
            Id = 831,
            Name = "P831",
            UserId = userId,
            CreationDate = DateTime.UtcNow
        });
        context.Elements.Add(new Element
        {
            Id = 831,
            Name = "E831",
            ProjectId = 831,
            CreatedDate = DateTime.UtcNow
        });
        context.ChangeRequests.Add(new ChangeRequest
        {
            Id = 831,
            ElementId = 831,
            Code = "CR-831",
            ClasificationType = ClasificationTypeCR.BugFixing,
            Priority = PriorityCR.Urgent,
            Status = StatusCR.Checkin,
            Action = ActionCR.Approved,
            CreatedAt = DateTime.UtcNow
        });
        context.GitTraceLinks.Add(new GitTraceLink
        {
            ChangeRequestId = 831,
            Repository = "owner/repo",
            CommitSha = "abc123",
            LinkedByUserId = userId,
            LinkedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var github = new FakeGithubService(
            repositoryExists: true,
            commitExists: true,
            pullRequestMerged: false);
        var controller = new ChangeRequestController(context, new GestorDocumentoApp.Services.ChangeRequestLifecycleService(context), github);
        SetAuthenticatedUser(controller, userId);

        var vm = new ChangeRequestCreateVM
        {
            ElementId = 831,
            Code = "CR-831",
            ClasificationType = ClasificationTypeCR.BugFixing,
            Priority = PriorityCR.Urgent,
            Status = StatusCR.Baselined,
            Action = ActionCR.Approved
        };

        var result = await controller.Edit(831, vm);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
    }

    [Fact]
    public async Task Version_Compare_Should_Return_NotFound_When_Element_Not_Owner()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);

        context.RequirementTypes.Add(new RequirementType { Id = 21, Name = "Req-21" });
        context.Projects.Add(new Project
        {
            Id = 901,
            Name = "Private project",
            UserId = "owner-user",
            CreationDate = DateTime.UtcNow
        });
        context.Elements.Add(new Element
        {
            Id = 901,
            Name = "Private element",
            ProjectId = 901,
            CreatedDate = DateTime.UtcNow
        });
        context.ChangeRequests.Add(new ChangeRequest
        {
            Id = 901,
            ElementId = 901,
            Code = "CR-901",
            ClasificationType = ClasificationTypeCR.Other,
            Priority = PriorityCR.Desirable,
            Status = StatusCR.Received,
            CreatedAt = DateTime.UtcNow
        });
        context.Versions.Add(new GestorDocumentoApp.Models.Version
        {
            Id = 910,
            Name = "v1",
            VersionCode = "1.0.0",
            ElementId = 901,
            UserId = "owner-user",
            RequirementTypeId = 21,
            ChangeRequestId = 901,
            UploadDate = DateTime.UtcNow,
            State = "inactive"
        });
        context.Versions.Add(new GestorDocumentoApp.Models.Version
        {
            Id = 911,
            Name = "v2",
            VersionCode = "1.1.0",
            ElementId = 901,
            UserId = "owner-user",
            RequirementTypeId = 21,
            ChangeRequestId = 901,
            UploadDate = DateTime.UtcNow,
            State = "active"
        });
        await context.SaveChangesAsync();

        var controller = new VersionController(context, NullLogger<VersionController>.Instance, new GestorDocumentoApp.Services.ChangeRequestLifecycleService(context));
        SetAuthenticatedUser(controller, "other-user");

        var result = await controller.Compare(901, 910, 911);

        Assert.IsType<NotFoundResult>(result);
    }

    private static ScmDocumentContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ScmDocumentContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ScmDocumentContext(options);
    }

    private static void SetAuthenticatedUser(Controller controller, string userId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, "test-user")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };
    }

    private static GestorDocumentoApp.Services.GithubService CreateGithubService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        return new GestorDocumentoApp.Services.GithubService(
            config,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<GestorDocumentoApp.Services.GithubService>.Instance);
    }

    private sealed class FakeGithubService : GestorDocumentoApp.Services.GithubService
    {
        private readonly bool _repositoryExists;
        private readonly bool _commitExists;
        private readonly bool _pullRequestMerged;

        public FakeGithubService(bool repositoryExists, bool commitExists, bool pullRequestMerged)
            : base(
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build(),
                new MemoryCache(new MemoryCacheOptions()),
                NullLogger<GestorDocumentoApp.Services.GithubService>.Instance)
        {
            _repositoryExists = repositoryExists;
            _commitExists = commitExists;
            _pullRequestMerged = pullRequestMerged;
        }

        public override Task<bool> RepositoryExistsAsync(string owner, string name) => Task.FromResult(_repositoryExists);
        public override Task<bool> CommitExistsAsync(string owner, string name, string sha) => Task.FromResult(_commitExists);
        public override Task<bool> IsPullRequestMergedAsync(string owner, string name, int number) => Task.FromResult(_pullRequestMerged);
        public override Task<GestorDocumentoApp.Services.GitHubCheckResult> CheckRepositoryAsync(string owner, string name)
            => Task.FromResult(new GestorDocumentoApp.Services.GitHubCheckResult
            {
                Status = _repositoryExists ? GestorDocumentoApp.Services.GitHubCheckStatus.Valid : GestorDocumentoApp.Services.GitHubCheckStatus.NotFound
            });
        public override Task<GestorDocumentoApp.Services.GitHubCheckResult> CheckCommitAsync(string owner, string name, string sha)
            => Task.FromResult(new GestorDocumentoApp.Services.GitHubCheckResult
            {
                Status = _commitExists ? GestorDocumentoApp.Services.GitHubCheckStatus.Valid : GestorDocumentoApp.Services.GitHubCheckStatus.NotFound
            });
        public override Task<GestorDocumentoApp.Services.GitHubCheckResult> CheckPullRequestMergedAsync(string owner, string name, int number)
            => Task.FromResult(new GestorDocumentoApp.Services.GitHubCheckResult
            {
                Status = _pullRequestMerged ? GestorDocumentoApp.Services.GitHubCheckStatus.Valid : GestorDocumentoApp.Services.GitHubCheckStatus.NotFound
            });
    }
}
