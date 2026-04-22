using Octokit;
using Microsoft.Extensions.Caching.Memory;

namespace GestorDocumentoApp.Services
{
    public enum GitHubCheckStatus
    {
        Valid = 1,
        NotFound = 2,
        Unavailable = 3
    }

    public sealed class GitHubCheckResult
    {
        public GitHubCheckStatus Status { get; init; }
        public string? Message { get; init; }
    }

    public class GithubService
    {
        private readonly GitHubClient _client;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<GithubService> _logger;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(3);

        public GithubService(IConfiguration configuration, IMemoryCache memoryCache, ILogger<GithubService> logger)
        {
            var token = configuration["GitHub:Token"];
            _memoryCache = memoryCache;
            _logger = logger;

            _client = new GitHubClient(new ProductHeaderValue("ScmDocumentApp"));
            if (!string.IsNullOrWhiteSpace(token))
            {
                _client.Credentials = new Credentials(token);
            }

        }

        public async Task<IReadOnlyList<Repository>> GetReposAsync()
        {
            return await _client.Repository.GetAllForCurrent(new RepositoryRequest
            {
                Type=RepositoryType.Public,
                Sort=RepositorySort.Created,
                Direction=SortDirection.Descending
            });
            
        }

        public virtual bool TryParseRepository(string repository, out string owner, out string name)
        {
            owner = string.Empty;
            name = string.Empty;
            if (string.IsNullOrWhiteSpace(repository))
            {
                return false;
            }

            var parts = repository.Trim().Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                return false;
            }

            owner = parts[0];
            name = parts[1];
            return true;
        }

        public virtual bool TryNormalizeRepository(string? repositoryInput, out string normalizedRepository)
        {
            normalizedRepository = string.Empty;
            if (string.IsNullOrWhiteSpace(repositoryInput))
            {
                return false;
            }

            var input = repositoryInput.Trim();
            if (TryParseRepository(input, out var owner, out var repo))
            {
                normalizedRepository = $"{owner}/{repo}";
                return true;
            }

            if (!Uri.TryCreate(input, UriKind.Absolute, out var uri))
            {
                return false;
            }

            if (!string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(uri.Host, "www.github.com", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var segments = uri.AbsolutePath
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (segments.Length < 2)
            {
                return false;
            }

            normalizedRepository = $"{segments[0]}/{segments[1]}";
            return true;
        }

        public virtual bool TryExtractPullRequestNumber(string? pullRequestUrl, out int pullRequestNumber)
        {
            pullRequestNumber = 0;
            if (string.IsNullOrWhiteSpace(pullRequestUrl))
            {
                return false;
            }

            if (!Uri.TryCreate(pullRequestUrl.Trim(), UriKind.Absolute, out var uri))
            {
                return false;
            }

            var segments = uri.AbsolutePath
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var pullIndex = Array.FindIndex(segments, x => string.Equals(x, "pull", StringComparison.OrdinalIgnoreCase));
            if (pullIndex < 0 || pullIndex + 1 >= segments.Length)
            {
                return false;
            }

            return int.TryParse(segments[pullIndex + 1], out pullRequestNumber) && pullRequestNumber > 0;
        }

        public virtual bool TryExtractCommitSha(string? commitUrl, out string commitSha)
        {
            commitSha = string.Empty;
            if (string.IsNullOrWhiteSpace(commitUrl))
            {
                return false;
            }

            if (!Uri.TryCreate(commitUrl.Trim(), UriKind.Absolute, out var uri))
            {
                return false;
            }

            var segments = uri.AbsolutePath
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var commitIndex = Array.FindIndex(segments, x => string.Equals(x, "commit", StringComparison.OrdinalIgnoreCase));
            if (commitIndex < 0 || commitIndex + 1 >= segments.Length)
            {
                return false;
            }

            commitSha = segments[commitIndex + 1];
            return !string.IsNullOrWhiteSpace(commitSha);
        }

        public virtual async Task<bool> RepositoryExistsAsync(string owner, string name)
        {
            var result = await CheckRepositoryAsync(owner, name);
            return result.Status == GitHubCheckStatus.Valid;
        }

        public virtual async Task<bool> CommitExistsAsync(string owner, string name, string sha)
        {
            var result = await CheckCommitAsync(owner, name, sha);
            return result.Status == GitHubCheckStatus.Valid;
        }

        public virtual async Task<PullRequest?> GetPullRequestAsync(string owner, string name, int number)
        {
            try
            {
                return await _client.PullRequest.Get(owner, name, number);
            }
            catch (NotFoundException)
            {
                return null;
            }
            catch
            {
                return null;
            }
        }

        public virtual async Task<bool> IsPullRequestMergedAsync(string owner, string name, int number)
        {
            var result = await CheckPullRequestMergedAsync(owner, name, number);
            return result.Status == GitHubCheckStatus.Valid;
        }

        public virtual async Task<GitHubCheckResult> CheckRepositoryAsync(string owner, string name)
        {
            var cacheKey = $"gh:repo:{owner}/{name}".ToLowerInvariant();
            if (_memoryCache.TryGetValue(cacheKey, out GitHubCheckResult? cached) && cached is not null)
            {
                return cached;
            }

            GitHubCheckResult result;
            try
            {
                _ = await _client.Repository.Get(owner, name);
                result = new GitHubCheckResult { Status = GitHubCheckStatus.Valid };
            }
            catch (NotFoundException)
            {
                result = new GitHubCheckResult { Status = GitHubCheckStatus.NotFound, Message = "Repository not found." };
            }
            catch (RateLimitExceededException ex)
            {
                _logger.LogWarning(ex, "GitHub rate limit reached while checking repository {Owner}/{Repo}", owner, name);
                result = new GitHubCheckResult { Status = GitHubCheckStatus.Unavailable, Message = "GitHub rate limit reached." };
            }
            catch (AbuseException ex)
            {
                _logger.LogWarning(ex, "GitHub abuse limit hit while checking repository {Owner}/{Repo}", owner, name);
                result = new GitHubCheckResult { Status = GitHubCheckStatus.Unavailable, Message = "GitHub temporarily unavailable (abuse protection)." };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GitHub unavailable while checking repository {Owner}/{Repo}", owner, name);
                result = new GitHubCheckResult { Status = GitHubCheckStatus.Unavailable, Message = "GitHub unavailable." };
            }

            _memoryCache.Set(cacheKey, result, CacheTtl);
            return result;
        }

        public virtual async Task<GitHubCheckResult> CheckCommitAsync(string owner, string name, string sha)
        {
            var normalizedSha = sha.Trim();
            var cacheKey = $"gh:commit:{owner}/{name}:{normalizedSha}".ToLowerInvariant();
            if (_memoryCache.TryGetValue(cacheKey, out GitHubCheckResult? cached) && cached is not null)
            {
                return cached;
            }

            GitHubCheckResult result;
            try
            {
                _ = await _client.Repository.Commit.Get(owner, name, normalizedSha);
                result = new GitHubCheckResult { Status = GitHubCheckStatus.Valid };
            }
            catch (NotFoundException)
            {
                result = new GitHubCheckResult { Status = GitHubCheckStatus.NotFound, Message = "Commit not found." };
            }
            catch (RateLimitExceededException ex)
            {
                _logger.LogWarning(ex, "GitHub rate limit reached while checking commit {Owner}/{Repo}:{Sha}", owner, name, normalizedSha);
                result = new GitHubCheckResult { Status = GitHubCheckStatus.Unavailable, Message = "GitHub rate limit reached." };
            }
            catch (AbuseException ex)
            {
                _logger.LogWarning(ex, "GitHub abuse limit hit while checking commit {Owner}/{Repo}:{Sha}", owner, name, normalizedSha);
                result = new GitHubCheckResult { Status = GitHubCheckStatus.Unavailable, Message = "GitHub temporarily unavailable (abuse protection)." };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GitHub unavailable while checking commit {Owner}/{Repo}:{Sha}", owner, name, normalizedSha);
                result = new GitHubCheckResult { Status = GitHubCheckStatus.Unavailable, Message = "GitHub unavailable." };
            }

            _memoryCache.Set(cacheKey, result, CacheTtl);
            return result;
        }

        public virtual async Task<GitHubCheckResult> CheckPullRequestMergedAsync(string owner, string name, int number)
        {
            var cacheKey = $"gh:pr-merged:{owner}/{name}:{number}".ToLowerInvariant();
            if (_memoryCache.TryGetValue(cacheKey, out GitHubCheckResult? cached) && cached is not null)
            {
                return cached;
            }

            GitHubCheckResult result;
            try
            {
                var pr = await _client.PullRequest.Get(owner, name, number);
                result = pr.Merged
                    ? new GitHubCheckResult { Status = GitHubCheckStatus.Valid }
                    : new GitHubCheckResult { Status = GitHubCheckStatus.NotFound, Message = "Pull request is not merged yet." };
            }
            catch (NotFoundException)
            {
                result = new GitHubCheckResult { Status = GitHubCheckStatus.NotFound, Message = "Pull request not found." };
            }
            catch (RateLimitExceededException ex)
            {
                _logger.LogWarning(ex, "GitHub rate limit reached while checking pull request {Owner}/{Repo}#{Pr}", owner, name, number);
                result = new GitHubCheckResult { Status = GitHubCheckStatus.Unavailable, Message = "GitHub rate limit reached." };
            }
            catch (AbuseException ex)
            {
                _logger.LogWarning(ex, "GitHub abuse limit hit while checking pull request {Owner}/{Repo}#{Pr}", owner, name, number);
                result = new GitHubCheckResult { Status = GitHubCheckStatus.Unavailable, Message = "GitHub temporarily unavailable (abuse protection)." };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GitHub unavailable while checking pull request {Owner}/{Repo}#{Pr}", owner, name, number);
                result = new GitHubCheckResult { Status = GitHubCheckStatus.Unavailable, Message = "GitHub unavailable." };
            }

            _memoryCache.Set(cacheKey, result, CacheTtl);
            return result;
        }
    }
}
