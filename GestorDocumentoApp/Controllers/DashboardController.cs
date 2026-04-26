using GestorDocumentoApp.Data;
using GestorDocumentoApp.Models;
using GestorDocumentoApp.Utils;
using GestorDocumentoApp.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GestorDocumentoApp.Controllers
{
    
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ScmDocumentContext _scmDocumentContext;
        private readonly IMemoryCache _memoryCache;

        public DashboardController(ScmDocumentContext scmDocumentContext, IMemoryCache memoryCache)
        {
            _scmDocumentContext = scmDocumentContext;
            _memoryCache = memoryCache;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var userId = GetCurrentUserId();
                var cacheKey = $"dashboard:{userId}";
                if (_memoryCache.TryGetValue(cacheKey, out DasboardVM? cachedVm) && cachedVm is not null)
                {
                    return View(cachedVm);
                }

                var changeSummary = await _scmDocumentContext.ChangeRequests
                    .Include(cr => cr.Element)
                    .ThenInclude(e => e.Project)
                    .Where(cr => cr.Element.Project.UserId == userId || cr.Element.Project.Members.Any(m => m.UserId == userId && m.Active))
                    .GroupBy(cr => new { cr.Element.Project.Name, cr.Status })
                    .Select(g => new ChangeRequestSummaryVM
                    {
                        ProjectName = g.Key.Name,
                        Status = g.Key.Status.GetDisplayNames(),
                        Total = g.Count()
                    })
                    .ToListAsync();

                var projectConfigs = await _scmDocumentContext.Projects
                    .Where(p => p.UserId == userId || p.Members.Any(m => m.UserId == userId && m.Active))
                    .SelectMany(p => p.Elements.Select(e => new ProjectConfigurationVM
                    {
                        ProjectName = p.Name,
                        ElementName = e.Name,
                        LatestVersion = e.Versions
                            .OrderByDescending(v => v.UploadDate)
                            .Select(v => v.VersionCode)
                            .FirstOrDefault(),
                        VersionDate = e.Versions
                            .OrderByDescending(v => v.UploadDate)
                            .Select(v => v.UploadDate)
                            .FirstOrDefault(),
                        Status = e.Versions
                            .OrderByDescending(v => v.UploadDate)
                            .Select(v => v.State)
                            .FirstOrDefault(),
                    }))
                    .ToListAsync();

                var userChangeRequests = await _scmDocumentContext.ChangeRequests
                    .Include(cr => cr.Element)
                    .Where(cr => cr.Element.Project.UserId == userId || cr.Element.Project.Members.Any(m => m.UserId == userId && m.Active))
                    .AsNoTracking()
                    .ToListAsync();

                var now = DateTime.UtcNow;
                var openStates = new[]
                {
                    StatusCR.Initiated, StatusCR.Received, StatusCR.Analyzed, StatusCR.Action,
                    StatusCR.Assigned, StatusCR.Checkout, StatusCR.ModifiedAndTested, StatusCR.Reviewed, StatusCR.Checkin
                };

                var openRequests = userChangeRequests.Where(cr => openStates.Contains(cr.Status)).ToList();
                var baselinedRequests = userChangeRequests.Where(cr => cr.Status == StatusCR.Baselined).ToList();

                var kpis = new DashboardKpiVM
                {
                    TotalCr = userChangeRequests.Count,
                    OpenCr = openRequests.Count,
                    BaselinedCr = baselinedRequests.Count,
                    ApprovedCr = userChangeRequests.Count(cr => cr.Action == ActionCR.Approved),
                    AvgOpenAgeDays = openRequests.Count == 0
                        ? 0
                        : Math.Round(openRequests.Average(cr => (now - cr.CreatedAt).TotalDays), 2),
                    AvgBaselinedLeadTimeDays = baselinedRequests.Count == 0
                        ? 0
                        : Math.Round(baselinedRequests.Average(cr => (now - cr.CreatedAt).TotalDays), 2)
                };

                var vm = new DasboardVM
                {
                    ChangeRequestSummary = changeSummary,
                    ProjectConfigurations = projectConfigs,
                    Kpis = kpis
                };

                _memoryCache.Set(cacheKey, vm, TimeSpan.FromSeconds(45));

                return View(vm);
            }catch(Exception)
            {
                return NotFound();
            }

        }

        private string GetCurrentUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        }
    }
}
