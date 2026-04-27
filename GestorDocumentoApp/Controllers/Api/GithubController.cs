using GestorDocumentoApp.Data;
using GestorDocumentoApp.Models;
using GestorDocumentoApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Octokit;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace GestorDocumentoApp.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]

    [Authorize]
    public class GithubController : ControllerBase
    {
        public readonly GithubService _githubService;
        private readonly ScmDocumentContext _context;
        private readonly IConfiguration _configuration;

        public GithubController(GithubService githubService, ScmDocumentContext context, IConfiguration configuration)
        {
            this._githubService = githubService;
            _context = context;
            _configuration = configuration;
        }

        [HttpGet("repos")]
        
        public async Task<IReadOnlyList<Repository>> GetAll()
        {
            return await _githubService.GetReposAsync();
        }

        [HttpGet("config-status")]
        public IActionResult ConfigStatus()
        {
            var token = _configuration["GitHub:Token"];
            var webhookSecret = _configuration["GitHub:WebhookSecret"];

            return Ok(new
            {
                HasToken = !string.IsNullOrWhiteSpace(token),
                HasWebhookSecret = !string.IsNullOrWhiteSpace(webhookSecret),
                WebhookEndpoint = $"{Request.Scheme}://{Request.Host}/api/github/webhook",
                Notes = new[]
                {
                    "Token is required to validate repositories, commits, and pull requests.",
                    "WebhookSecret is required to validate X-Hub-Signature-256 signatures."
                }
            });
        }

        [HttpPost("trace-links")]
        public async Task<IActionResult> AddTraceLink([FromBody] AddGitTraceRequest request)
        {
            if (!TryNormalizeGitInputs(request))
            {
                return BadRequest("Repository is required. Use owner/repo or a GitHub URL.");
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var changeRequest = await _context.ChangeRequests
                .Include(x => x.Element)
                .FirstOrDefaultAsync(x => x.Id == request.ChangeRequestId &&
                    (x.Element.Project.UserId == userId || x.Element.Project.Members.Any(m => m.UserId == userId && m.Active)));
            if (changeRequest is null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(request.CommitSha) && string.IsNullOrWhiteSpace(request.PullRequestUrl) && !request.PullRequestNumber.HasValue)
            {
                return BadRequest("CommitSha or pull request data is required.");
            }

            var validationMessage = await ValidateGithubTraceAsync(request.Repository, request.CommitSha, request.PullRequestNumber);
            if (validationMessage is not null)
            {
                return BadRequest(validationMessage);
            }

            if (request.VersionId.HasValue)
            {
                var versionBelongsToChangeRequest = await _context.Versions
                    .AnyAsync(x => x.Id == request.VersionId.Value && x.ChangeRequestId == request.ChangeRequestId &&
                        (x.Element.Project.UserId == userId || x.Element.Project.Members.Any(m => m.UserId == userId && m.Active)));
                if (!versionBelongsToChangeRequest)
                {
                    return BadRequest("Version does not belong to the target change request.");
                }
            }

            var link = new GitTraceLink
            {
                ChangeRequestId = request.ChangeRequestId,
                VersionId = request.VersionId,
                Repository = request.Repository.Trim(),
                CommitSha = string.IsNullOrWhiteSpace(request.CommitSha) ? null : request.CommitSha.Trim(),
                PullRequestUrl = string.IsNullOrWhiteSpace(request.PullRequestUrl) ? null : request.PullRequestUrl.Trim(),
                PullRequestNumber = request.PullRequestNumber,
                LinkedByUserId = userId,
                LinkedAt = DateTime.UtcNow
            };

            _context.GitTraceLinks.Add(link);
            _context.ChangeRequestAudits.Add(new ChangeRequestAudit
            {
                ChangeRequestId = request.ChangeRequestId,
                ChangedAt = DateTime.UtcNow,
                ChangedByUserId = userId,
                EventType = "Trazabilidad Git API",
                Summary = $"Vinculo agregado via API. Repo: {link.Repository}, commit: {link.CommitSha ?? "-"}, PR: {link.PullRequestNumber?.ToString() ?? "-"}."
            });
            await _context.SaveChangesAsync();

            return Ok(new { link.Id });
        }

        [HttpPut("trace-links/{traceId:int}")]
        public async Task<IActionResult> UpdateTraceLink(int traceId, [FromBody] AddGitTraceRequest request)
        {
            if (!TryNormalizeGitInputs(request))
            {
                return BadRequest("Repository is required. Use owner/repo or a GitHub URL.");
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var trace = await _context.GitTraceLinks
                .Include(x => x.ChangeRequest)
                .ThenInclude(x => x.Element)
                .FirstOrDefaultAsync(x => x.Id == traceId &&
                    (x.ChangeRequest.Element.Project.UserId == userId || x.ChangeRequest.Element.Project.Members.Any(m => m.UserId == userId && m.Active)));
            if (trace is null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(request.CommitSha) && string.IsNullOrWhiteSpace(request.PullRequestUrl) && !request.PullRequestNumber.HasValue)
            {
                return BadRequest("CommitSha or pull request data is required.");
            }

            var validationMessage = await ValidateGithubTraceAsync(request.Repository, request.CommitSha, request.PullRequestNumber);
            if (validationMessage is not null)
            {
                return BadRequest(validationMessage);
            }

            var targetChangeRequestId = request.ChangeRequestId <= 0 ? trace.ChangeRequestId : request.ChangeRequestId;
            var changeRequest = await _context.ChangeRequests
                .Include(x => x.Element)
                .FirstOrDefaultAsync(x => x.Id == targetChangeRequestId &&
                    (x.Element.Project.UserId == userId || x.Element.Project.Members.Any(m => m.UserId == userId && m.Active)));
            if (changeRequest is null)
            {
                return NotFound("Target change request was not found.");
            }

            if (request.VersionId.HasValue)
            {
                var versionBelongsToChangeRequest = await _context.Versions
                    .AnyAsync(x => x.Id == request.VersionId.Value && x.ChangeRequestId == targetChangeRequestId &&
                        (x.Element.Project.UserId == userId || x.Element.Project.Members.Any(m => m.UserId == userId && m.Active)));
                if (!versionBelongsToChangeRequest)
                {
                    return BadRequest("Version does not belong to the target change request.");
                }
            }

            trace.ChangeRequestId = targetChangeRequestId;
            trace.Repository = request.Repository.Trim();
            trace.CommitSha = string.IsNullOrWhiteSpace(request.CommitSha) ? null : request.CommitSha.Trim();
            trace.PullRequestNumber = request.PullRequestNumber;
            trace.PullRequestUrl = string.IsNullOrWhiteSpace(request.PullRequestUrl) ? null : request.PullRequestUrl.Trim();
            trace.VersionId = request.VersionId;
            trace.LinkedByUserId = userId;
            trace.LinkedAt = DateTime.UtcNow;

            _context.ChangeRequestAudits.Add(new ChangeRequestAudit
            {
                ChangeRequestId = trace.ChangeRequestId,
                ChangedAt = DateTime.UtcNow,
                ChangedByUserId = userId,
                EventType = "Trazabilidad Git API",
                Summary = $"Vinculo actualizado via API. TraceId: {traceId}, Repo: {trace.Repository}, commit: {trace.CommitSha ?? "-"}, PR: {trace.PullRequestNumber?.ToString() ?? "-"}."
            });

            await _context.SaveChangesAsync();
            return Ok(new { trace.Id });
        }

        [HttpDelete("trace-links/{traceId:int}")]
        public async Task<IActionResult> DeleteTraceLink(int traceId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var trace = await _context.GitTraceLinks
                .Include(x => x.ChangeRequest)
                .ThenInclude(x => x.Element)
                .FirstOrDefaultAsync(x => x.Id == traceId &&
                    (x.ChangeRequest.Element.Project.UserId == userId || x.ChangeRequest.Element.Project.Members.Any(m => m.UserId == userId && m.Active)));
            if (trace is null)
            {
                return NotFound();
            }

            _context.GitTraceLinks.Remove(trace);
            _context.ChangeRequestAudits.Add(new ChangeRequestAudit
            {
                ChangeRequestId = trace.ChangeRequestId,
                ChangedAt = DateTime.UtcNow,
                ChangedByUserId = userId,
                EventType = "Trazabilidad Git API",
                Summary = $"Vinculo eliminado via API. TraceId: {traceId}."
            });
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [AllowAnonymous]
        [HttpPost("webhook")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Webhook()
        {
            var signature = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();
            var secret = _configuration["GitHub:WebhookSecret"];
            using var reader = new StreamReader(Request.Body);
            var payload = await reader.ReadToEndAsync();

            if (!IsValidSignature(payload, signature, secret))
            {
                return Unauthorized();
            }

            var eventName = Request.Headers["X-GitHub-Event"].FirstOrDefault();
            if (!string.Equals(eventName, "pull_request", StringComparison.OrdinalIgnoreCase))
            {
                return Ok(new { status = "ignored", reason = "unsupported event" });
            }

            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (!root.TryGetProperty("pull_request", out var prElement) ||
                !root.TryGetProperty("repository", out var repositoryElement))
            {
                return BadRequest("Invalid pull_request payload.");
            }

            var fullName = repositoryElement.TryGetProperty("full_name", out var fullNameElement)
                ? fullNameElement.GetString()
                : null;
            var prNumber = prElement.TryGetProperty("number", out var numberElement)
                ? numberElement.GetInt32()
                : 0;
            var prUrl = prElement.TryGetProperty("html_url", out var urlElement)
                ? urlElement.GetString()
                : null;
            var merged = prElement.TryGetProperty("merged", out var mergedElement) && mergedElement.GetBoolean();
            var state = prElement.TryGetProperty("state", out var stateElement)
                ? stateElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(fullName) || prNumber <= 0)
            {
                return BadRequest("Repository name or PR number missing.");
            }

            var affectedLinks = await _context.GitTraceLinks
                .Where(x => x.Repository == fullName && x.PullRequestNumber == prNumber)
                .ToListAsync();

            if (affectedLinks.Count == 0)
            {
                return Ok(new { status = "ok", updated = 0 });
            }

            foreach (var link in affectedLinks)
            {
                link.PullRequestUrl = string.IsNullOrWhiteSpace(prUrl) ? link.PullRequestUrl : prUrl;
                link.LinkedAt = DateTime.UtcNow;
                _context.ChangeRequestAudits.Add(new ChangeRequestAudit
                {
                    ChangeRequestId = link.ChangeRequestId,
                    ChangedAt = DateTime.UtcNow,
                    ChangedByUserId = "github-webhook",
                    EventType = "Trazabilidad Git Webhook",
                    Summary = $"PR #{prNumber} sincronizado. state={state ?? "-"}, merged={merged}."
                });
            }

            await _context.SaveChangesAsync();
            return Ok(new { status = "ok", updated = affectedLinks.Count });
        }

        public class AddGitTraceRequest
        {
            public int ChangeRequestId { get; set; }
            public string Repository { get; set; } = string.Empty;
            public string? CommitSha { get; set; }
            public string? PullRequestUrl { get; set; }
            public int? PullRequestNumber { get; set; }
            public int? VersionId { get; set; }
        }

        private async Task<string?> ValidateGithubTraceAsync(string repository, string? commitSha, int? pullRequestNumber)
        {
            if (!_githubService.TryParseRepository(repository, out var owner, out var repo))
            {
                return "Invalid repository format. Use owner/repo.";
            }

            var repositoryCheck = await _githubService.CheckRepositoryAsync(owner, repo);
            if (repositoryCheck.Status == GitHubCheckStatus.Unavailable)
            {
                return "GitHub is temporarily unavailable (rate limit or transient error). Try again later.";
            }

            if (repositoryCheck.Status != GitHubCheckStatus.Valid)
            {
                return "Repository was not found or is not accessible with the configured token.";
            }

            if (!string.IsNullOrWhiteSpace(commitSha))
            {
                var commitCheck = await _githubService.CheckCommitAsync(owner, repo, commitSha.Trim());
                if (commitCheck.Status == GitHubCheckStatus.Unavailable)
                {
                    return "GitHub is temporarily unavailable to validate the commit.";
                }

                if (commitCheck.Status != GitHubCheckStatus.Valid)
                {
                    return "Commit SHA was not found in repository.";
                }
            }

            if (pullRequestNumber.HasValue)
            {
                var pullRequestCheck = await _githubService.CheckPullRequestAsync(owner, repo, pullRequestNumber.Value);
                if (pullRequestCheck.Status == GitHubCheckStatus.Unavailable)
                {
                    return "GitHub is temporarily unavailable to validate the pull request.";
                }

                if (pullRequestCheck.Status != GitHubCheckStatus.Valid)
                {
                    return "Pull request number was not found in repository.";
                }
            }

            return null;
        }

        private bool TryNormalizeGitInputs(AddGitTraceRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Repository))
            {
                if (!string.IsNullOrWhiteSpace(request.PullRequestUrl) &&
                    _githubService.TryNormalizeRepository(request.PullRequestUrl, out var fromPrUrl))
                {
                    request.Repository = fromPrUrl;
                }
            }
            else if (_githubService.TryNormalizeRepository(request.Repository, out var normalizedRepository))
            {
                request.Repository = normalizedRepository;
            }

            if (string.IsNullOrWhiteSpace(request.Repository))
            {
                return false;
            }

            if (!request.PullRequestNumber.HasValue &&
                !string.IsNullOrWhiteSpace(request.PullRequestUrl) &&
                _githubService.TryExtractPullRequestNumber(request.PullRequestUrl, out var extractedPr))
            {
                request.PullRequestNumber = extractedPr;
            }

            if (string.IsNullOrWhiteSpace(request.CommitSha) &&
                !string.IsNullOrWhiteSpace(request.PullRequestUrl) &&
                _githubService.TryExtractCommitSha(request.PullRequestUrl, out var extractedSha))
            {
                request.CommitSha = extractedSha;
            }

            return true;
        }

        private static bool IsValidSignature(string payload, string? signatureHeader, string? secret)
        {
            if (string.IsNullOrWhiteSpace(secret))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(signatureHeader) || !signatureHeader.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var signatureValue = signatureHeader["sha256=".Length..];
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var expected = Convert.ToHexString(hash).ToLowerInvariant();
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(signatureValue.ToLowerInvariant()));
        }

    }
}
