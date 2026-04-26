using GestorDocumentoApp.Data;
using GestorDocumentoApp.Extensions;
using GestorDocumentoApp.Models;
using GestorDocumentoApp.Services;
using GestorDocumentoApp.Utils;
using GestorDocumentoApp.ViewModels;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;

namespace GestorDocumentoApp.Controllers
{
    public class ChangeRequestController : Controller
    {
        public readonly ScmDocumentContext _scmDocumentContext;
        private readonly ChangeRequestLifecycleService _changeRequestLifecycleService;
        private readonly GithubService _githubService;
        private static readonly Dictionary<StatusCR, StatusCR?> _nextStatusByCurrent = new()
        {
            { StatusCR.Initiated, StatusCR.Received },
            { StatusCR.Received, StatusCR.Analyzed },
            { StatusCR.Analyzed, StatusCR.Action },
            { StatusCR.Action, StatusCR.Assigned },
            { StatusCR.Assigned, StatusCR.Checkout },
            { StatusCR.Checkout, StatusCR.ModifiedAndTested },
            { StatusCR.ModifiedAndTested, StatusCR.Reviewed },
            { StatusCR.Reviewed, StatusCR.Approved },
            { StatusCR.Approved, StatusCR.Checkin },
            { StatusCR.Checkin, StatusCR.Baselined },
            { StatusCR.Baselined, null }
        };

        public ChangeRequestController(
            ScmDocumentContext scmDocumentContext,
            ChangeRequestLifecycleService changeRequestLifecycleService,
            GithubService githubService)
        {
            _scmDocumentContext = scmDocumentContext;
            _changeRequestLifecycleService = changeRequestLifecycleService;
            _githubService = githubService;
        }

        public async Task<IActionResult> Index(
            int? elementId,
            int? projectId,
            string? search,
            PriorityCR? priorityFilter,
            StatusCR? statusFilter,
            ActionCR? actionFilter,
            int? phaseFilter,
            int? iterationFilter,
            string? sortBy,
            string? sortDir,
            int pageNumber = 1,
            int pageSize = 10)
        {
            try
            {
                var userId = GetCurrentUserId();
                Element? element = null;

                if (elementId.HasValue)
                {
                    element = await _scmDocumentContext.Elements
                        .FirstOrDefaultAsync(x => x.Id == elementId && (x.Project.UserId == userId || x.Project.Members.Any(m => m.UserId == userId && m.Active)));
                    if (element is null)
                    {
                        return NotFound();
                    }
                }

                IQueryable<ChangeRequest> query = _scmDocumentContext.ChangeRequests
                    .Include(c => c.Element)
                    .Where(c => c.Element.Project.UserId == userId || c.Element.Project.Members.Any(m => m.UserId == userId && m.Active));

                if (elementId.HasValue)
                {
                    query = query.Where(c => c.ElementId == elementId);
                }
                else if (projectId.HasValue)
                {
                    query = query.Where(c => c.Element.ProjectId == projectId.Value);
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var normalizedSearch = search.Trim();
                    query = query.Where(c =>
                        c.Code.Contains(normalizedSearch) ||
                        (c.Description != null && c.Description.Contains(normalizedSearch)));
                }

                if (statusFilter.HasValue)
                {
                    query = query.Where(c => c.Status == statusFilter.Value);
                }

                if (priorityFilter.HasValue)
                {
                    query = query.Where(c => c.Priority == priorityFilter.Value);
                }

                if (actionFilter.HasValue)
                {
                    query = query.Where(c => c.Action == actionFilter.Value);
                }

                if (phaseFilter.HasValue)
                {
                    query = query.Where(c => _scmDocumentContext.Versions.Any(v => v.ChangeRequestId == c.Id && v.Phase == phaseFilter.Value));
                }

                if (iterationFilter.HasValue)
                {
                    query = query.Where(c => _scmDocumentContext.Versions.Any(v => v.ChangeRequestId == c.Id && v.iteration == iterationFilter.Value));
                }

                var normalizedSortBy = NormalizeSortBy(sortBy);
                var normalizedSortDir = NormalizeSortDir(sortDir);
                query = ApplySorting(query, normalizedSortBy, normalizedSortDir);

                var changeRequests = await query
                    .AsNoTracking()
                    .ToPagedListAsync(pageNumber, pageSize);

                var elements = await _scmDocumentContext.Elements.Include(x => x.Project).AsNoTracking()
                    .Where(x => x.Project.UserId == userId || x.Project.Members.Any(m => m.UserId == userId && m.Active))
                    .OrderBy(x => x.Name).ToListAsync();
                var projects = await _scmDocumentContext.Projects.AsNoTracking()
                    .Where(x => x.UserId == userId || x.Members.Any(m => m.UserId == userId && m.Active))
                    .OrderBy(x => x.Name)
                    .ToListAsync();

                var vmList = changeRequests.Items.Select(c => new ChangeRequestVM
                {
                    Id = c.Id,
                    Code = c.Code,
                    Description = c.Description,
                    Priority = EnumHelper.GetDisplayName(c.Priority),
                    Clasification =EnumHelper.GetDisplayName(c.ClasificationType),
                    Action = c.Action??ActionCR.InWait,
                    Status = c.Status,
                    CreatedAt = c.CreatedAt,
                }).ToList();

                return View(
                    new ChangeRequestIndexVM
                    {
                        Items = vmList,
                        PageNumber = changeRequests.PageNumber,
                        PageSize = changeRequests.PageSize,
                        TotalCount = changeRequests.TotalCount,
                        ElementId = element?.Id,
                        ElementName = element?.Name,
                        ProjectId = projectId,
                        Search = search,
                        PriorityFilter = priorityFilter,
                        StatusFilter = statusFilter,
                        ActionFilter = actionFilter,
                        PhaseFilter = phaseFilter,
                        IterationFilter = iterationFilter,
                        SortBy = normalizedSortBy,
                        SortDir = normalizedSortDir,
                        HasNext=changeRequests.HasNext,
                        HasPrevious = changeRequests.HasPrevious,
                        Projects = projects.Select(x => new SelectListItem { Text = x.Name, Value = x.Id.ToString() }),
                        Elements = elements.Select(x => new SelectListItem { Text = $"{x.Project.Name} - {x.Name}", Value = x.Id.ToString() }),
                        PriorityOptions = EnumHelper.GetSelectList<PriorityCR>(),
                        StatusOptions = EnumHelper.GetSelectList<StatusCR>(),
                        ActionOptions = EnumHelper.GetSelectList<ActionCR>(),
                        PhaseOptions = BuildPhaseOptions()

                    });
            }
            catch (Exception)
            {
                return NotFound();
            }
        }

        public async Task<IActionResult> ExportCsv(
            int? elementId,
            int? projectId,
            string? search,
            PriorityCR? priorityFilter,
            StatusCR? statusFilter,
            ActionCR? actionFilter,
            int? phaseFilter,
            int? iterationFilter,
            string? sortBy,
            string? sortDir)
        {
            var userId = GetCurrentUserId();
            IQueryable<ChangeRequest> query = _scmDocumentContext.ChangeRequests
                .Include(c => c.Element)
                .Where(c => c.Element.Project.UserId == userId || c.Element.Project.Members.Any(m => m.UserId == userId && m.Active));

            if (elementId.HasValue)
            {
                query = query.Where(c => c.ElementId == elementId);
            }
            else if (projectId.HasValue)
            {
                query = query.Where(c => c.Element.ProjectId == projectId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var normalizedSearch = search.Trim();
                query = query.Where(c =>
                    c.Code.Contains(normalizedSearch) ||
                    (c.Description != null && c.Description.Contains(normalizedSearch)));
            }

            if (statusFilter.HasValue)
            {
                query = query.Where(c => c.Status == statusFilter.Value);
            }

            if (priorityFilter.HasValue)
            {
                query = query.Where(c => c.Priority == priorityFilter.Value);
            }

            if (actionFilter.HasValue)
            {
                query = query.Where(c => c.Action == actionFilter.Value);
            }

            if (phaseFilter.HasValue)
            {
                query = query.Where(c => _scmDocumentContext.Versions.Any(v => v.ChangeRequestId == c.Id && v.Phase == phaseFilter.Value));
            }

            if (iterationFilter.HasValue)
            {
                query = query.Where(c => _scmDocumentContext.Versions.Any(v => v.ChangeRequestId == c.Id && v.iteration == iterationFilter.Value));
            }

            var normalizedSortBy = NormalizeSortBy(sortBy);
            var normalizedSortDir = NormalizeSortDir(sortDir);
            var items = await ApplySorting(query, normalizedSortBy, normalizedSortDir)
                .AsNoTracking()
                .ToListAsync();

            var builder = new StringBuilder();
            builder.AppendLine("Id,Codigo,Clasificacion,Prioridad,Proceso,Estado,Descripcion,FechaCreacion");
            foreach (var item in items)
            {
                builder.AppendLine(string.Join(',',
                    item.Id,
                    EscapeCsv(item.Code),
                    EscapeCsv(EnumHelper.GetDisplayName(item.ClasificationType)),
                    EscapeCsv(EnumHelper.GetDisplayName(item.Priority)),
                    EscapeCsv(EnumHelper.GetDisplayName(item.Status)),
                    EscapeCsv(EnumHelper.GetDisplayName(item.Action ?? ActionCR.InWait)),
                    EscapeCsv(item.Description ?? string.Empty),
                    EscapeCsv(item.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"))));
            }

            var bytes = Encoding.UTF8.GetBytes(builder.ToString());
            return File(bytes, "text/csv; charset=utf-8", $"change-requests-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
        }


        public async Task<IActionResult> Create()
        {
            var userId = GetCurrentUserId();
            var elements = await _scmDocumentContext.Elements.AsNoTracking().Include(x => x.Project)
                .Where(x => x.Project.UserId == userId || x.Project.Members.Any(m => m.UserId == userId && m.Active))
                .OrderBy(x => x.Name).ToListAsync();
            var changeVM = new ChangeRequestCreateVM
            {
                ClasificationOptions = EnumHelper.GetSelectList<ClasificationTypeCR>(),
                PriorityOptions = EnumHelper.GetSelectList<PriorityCR>(),
                StatusOptions = EnumHelper.GetSelectList<StatusCR>(),
                ActionOptions = EnumHelper.GetSelectList<ActionCR>(),
                Code = await GenerateNextChangeRequestCodeAsync(),
                Elements = elements.Select(x => new SelectListItem { Text = $"{x.Name} - {x.Project.Name}", Value = x.Id.ToString() })

            };
            return View(changeVM);

        }

        [HttpPost]
        public async Task<IActionResult> Create(ChangeRequestCreateVM vm)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(vm.Code))
            {
                vm.Code = await GenerateNextChangeRequestCodeAsync();
                ModelState.Remove(nameof(vm.Code));
            }
            vm.Code = vm.Code.Trim();

            if (!ModelState.IsValid)
            {
                var elements = await _scmDocumentContext.Elements.AsNoTracking().Include(x => x.Project)
                    .Where(x => x.Project.UserId == userId || x.Project.Members.Any(m => m.UserId == userId && m.Active))
                    .OrderBy(x => x.Name).ToListAsync();
                vm.ClasificationOptions = EnumHelper.GetSelectList<ClasificationTypeCR>();
                vm.PriorityOptions = EnumHelper.GetSelectList<PriorityCR>();
                vm.StatusOptions = EnumHelper.GetSelectList<StatusCR>();
                vm.ActionOptions = EnumHelper.GetSelectList<ActionCR>();
                vm.Elements = elements.Select(x => new SelectListItem { Text = $"{x.Name} - {x.Project.Name}", Value = x.Id.ToString() });

                return View(vm);
            }

            var existsCode = await _scmDocumentContext.ChangeRequests
                .AnyAsync(x => x.Code == vm.Code);
            if (existsCode)
            {
                ModelState.AddModelError(nameof(vm.Code), "El codigo ya existe. Usa otro o acepta el sugerido.");
                vm.Code = await GenerateNextChangeRequestCodeAsync();
                var elements = await _scmDocumentContext.Elements.AsNoTracking().Include(x => x.Project)
                    .Where(x => x.Project.UserId == userId || x.Project.Members.Any(m => m.UserId == userId && m.Active))
                    .OrderBy(x => x.Name).ToListAsync();
                vm.ClasificationOptions = EnumHelper.GetSelectList<ClasificationTypeCR>();
                vm.PriorityOptions = EnumHelper.GetSelectList<PriorityCR>();
                vm.StatusOptions = EnumHelper.GetSelectList<StatusCR>();
                vm.ActionOptions = EnumHelper.GetSelectList<ActionCR>();
                vm.Elements = elements.Select(x => new SelectListItem { Text = $"{x.Name} - {x.Project.Name}", Value = x.Id.ToString() });
                return View(vm);
            }
            if (vm.ElementId is null || vm.ClasificationType is null || vm.Priority is null || vm.Status is null)
            {
                ModelState.AddModelError(string.Empty, "Debe completar los campos requeridos.");
                var elements = await _scmDocumentContext.Elements.AsNoTracking().Include(x => x.Project)
                    .Where(x => x.Project.UserId == userId || x.Project.Members.Any(m => m.UserId == userId && m.Active))
                    .OrderBy(x => x.Name).ToListAsync();
                vm.ClasificationOptions = EnumHelper.GetSelectList<ClasificationTypeCR>();
                vm.PriorityOptions = EnumHelper.GetSelectList<PriorityCR>();
                vm.StatusOptions = EnumHelper.GetSelectList<StatusCR>();
                vm.ActionOptions = EnumHelper.GetSelectList<ActionCR>();
                vm.Elements = elements.Select(x => new SelectListItem { Text = $"{x.Name} - {x.Project.Name}", Value = x.Id.ToString() });
                return View(vm);
            }

            var selectedElement = await _scmDocumentContext.Elements.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == vm.ElementId && (x.Project.UserId == userId || x.Project.Members.Any(m => m.UserId == userId && m.Active)));
            if (selectedElement is null)
            {
                return Forbid();
            }

            if (vm.Status.Value == StatusCR.Baselined && vm.Action != ActionCR.Approved)
            {
                ModelState.AddModelError(nameof(vm.Action), "Para baselinar, la accion debe ser Aprobado.");
                var elements = await _scmDocumentContext.Elements.AsNoTracking().Include(x => x.Project)
                    .Where(x => x.Project.UserId == userId || x.Project.Members.Any(m => m.UserId == userId && m.Active))
                    .OrderBy(x => x.Name).ToListAsync();
                vm.ClasificationOptions = EnumHelper.GetSelectList<ClasificationTypeCR>();
                vm.PriorityOptions = EnumHelper.GetSelectList<PriorityCR>();
                vm.StatusOptions = EnumHelper.GetSelectList<StatusCR>();
                vm.ActionOptions = EnumHelper.GetSelectList<ActionCR>();
                vm.Elements = elements.Select(x => new SelectListItem { Text = $"{x.Name} - {x.Project.Name}", Value = x.Id.ToString() });
                return View(vm);
            }

            if (vm.Status.Value == StatusCR.Baselined)
            {
                ModelState.AddModelError(nameof(vm.Status), "No puedes baselinar una CR nueva sin evidencia Git. Vincula commit o PR primero.");
                var elements = await _scmDocumentContext.Elements.AsNoTracking().Include(x => x.Project)
                    .Where(x => x.Project.UserId == userId || x.Project.Members.Any(m => m.UserId == userId && m.Active))
                    .OrderBy(x => x.Name).ToListAsync();
                vm.ClasificationOptions = EnumHelper.GetSelectList<ClasificationTypeCR>();
                vm.PriorityOptions = EnumHelper.GetSelectList<PriorityCR>();
                vm.StatusOptions = EnumHelper.GetSelectList<StatusCR>();
                vm.ActionOptions = EnumHelper.GetSelectList<ActionCR>();
                vm.Elements = elements.Select(x => new SelectListItem { Text = $"{x.Name} - {x.Project.Name}", Value = x.Id.ToString() });
                return View(vm);
            }

            var changeRequest = new ChangeRequest
            {
                ElementId = vm.ElementId.Value,
                ClasificationType = vm.ClasificationType.Value,
                Description = vm.Description,
                Priority = vm.Priority.Value,
                Status = vm.Status.Value,
                Remarks = vm.Remarks,
                Code = vm.Code,
                Action = vm.Action,
                CreatedAt = DateTime.UtcNow
            };

            _scmDocumentContext.ChangeRequests.Add(changeRequest);
            await _scmDocumentContext.SaveChangesAsync();
            await _changeRequestLifecycleService.RegisterCreatedAsync(
                changeRequest,
                userId,
                $"CR creada con estado {EnumHelper.GetDisplayName(changeRequest.Status)}.");

            return RedirectToAction(nameof(Index), new { elementId = vm.ElementId });

        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, ChangeRequestCreateVM vm)
        {
            var userId = GetCurrentUserId();
            var changeRequest = await _scmDocumentContext.ChangeRequests
                .Include(x => x.Element)
                .FirstOrDefaultAsync(x => x.Id == id && (x.Element.Project.UserId == userId || x.Element.Project.Members.Any(m => m.UserId == userId && m.Active)));

            if (changeRequest is null)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                var elements = await _scmDocumentContext.Elements.AsNoTracking().Include(x => x.Project)
                    .Where(x => x.Project.UserId == userId || x.Project.Members.Any(m => m.UserId == userId && m.Active))
                    .OrderBy(x => x.Name).ToListAsync();
                vm.ClasificationOptions = EnumHelper.GetSelectList<ClasificationTypeCR>();
                vm.PriorityOptions = EnumHelper.GetSelectList<PriorityCR>();
                vm.StatusOptions = EnumHelper.GetSelectList<StatusCR>();
                vm.ActionOptions = EnumHelper.GetSelectList<ActionCR>();
                vm.Elements = elements.Select(x => new SelectListItem { Text = $"{x.Name} - {x.Project.Name}", Value = x.Id.ToString() });

                return View(vm);
            }
            if (vm.ElementId is null || vm.ClasificationType is null || vm.Priority is null || vm.Status is null)
            {
                ModelState.AddModelError(string.Empty, "Debe completar los campos requeridos.");
                var elements = await _scmDocumentContext.Elements.AsNoTracking().Include(x => x.Project)
                    .Where(x => x.Project.UserId == userId || x.Project.Members.Any(m => m.UserId == userId && m.Active))
                    .OrderBy(x => x.Name).ToListAsync();
                vm.ClasificationOptions = EnumHelper.GetSelectList<ClasificationTypeCR>();
                vm.PriorityOptions = EnumHelper.GetSelectList<PriorityCR>();
                vm.StatusOptions = EnumHelper.GetSelectList<StatusCR>();
                vm.ActionOptions = EnumHelper.GetSelectList<ActionCR>();
                vm.Elements = elements.Select(x => new SelectListItem { Text = $"{x.Name} - {x.Project.Name}", Value = x.Id.ToString() });
                return View(vm);
            }

            var selectedElement = await _scmDocumentContext.Elements.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == vm.ElementId && (x.Project.UserId == userId || x.Project.Members.Any(m => m.UserId == userId && m.Active)));
            if (selectedElement is null)
            {
                return Forbid();
            }

            if (!IsValidStatusTransition(changeRequest.Status, vm.Status.Value))
            {
                ModelState.AddModelError(nameof(vm.Status), "Transicion de estado no valida.");
                var elements = await _scmDocumentContext.Elements.AsNoTracking().Include(x => x.Project)
                    .Where(x => x.Project.UserId == userId || x.Project.Members.Any(m => m.UserId == userId && m.Active))
                    .OrderBy(x => x.Name).ToListAsync();
                vm.ClasificationOptions = EnumHelper.GetSelectList<ClasificationTypeCR>();
                vm.PriorityOptions = EnumHelper.GetSelectList<PriorityCR>();
                vm.StatusOptions = EnumHelper.GetSelectList<StatusCR>();
                vm.ActionOptions = EnumHelper.GetSelectList<ActionCR>();
                vm.Elements = elements.Select(x => new SelectListItem { Text = $"{x.Name} - {x.Project.Name}", Value = x.Id.ToString() });
                return View(vm);
            }

            if (vm.Status.Value == StatusCR.Baselined && vm.Action != ActionCR.Approved)
            {
                ModelState.AddModelError(nameof(vm.Action), "Para baselinar, la accion debe ser Aprobado.");
                var elements = await _scmDocumentContext.Elements.AsNoTracking().Include(x => x.Project)
                    .Where(x => x.Project.UserId == userId || x.Project.Members.Any(m => m.UserId == userId && m.Active))
                    .OrderBy(x => x.Name).ToListAsync();
                vm.ClasificationOptions = EnumHelper.GetSelectList<ClasificationTypeCR>();
                vm.PriorityOptions = EnumHelper.GetSelectList<PriorityCR>();
                vm.StatusOptions = EnumHelper.GetSelectList<StatusCR>();
                vm.ActionOptions = EnumHelper.GetSelectList<ActionCR>();
                vm.Elements = elements.Select(x => new SelectListItem { Text = $"{x.Name} - {x.Project.Name}", Value = x.Id.ToString() });
                return View(vm);
            }

            if (vm.Status.Value == StatusCR.Baselined)
            {
                var baselineValidation = await ValidateBaselineEvidenceAsync(changeRequest.Id);
                if (!baselineValidation.IsValid)
                {
                    ModelState.AddModelError(nameof(vm.Status), baselineValidation.Message);
                    var elements = await _scmDocumentContext.Elements.AsNoTracking().Include(x => x.Project)
                        .Where(x => x.Project.UserId == userId || x.Project.Members.Any(m => m.UserId == userId && m.Active))
                        .OrderBy(x => x.Name).ToListAsync();
                    vm.ClasificationOptions = EnumHelper.GetSelectList<ClasificationTypeCR>();
                    vm.PriorityOptions = EnumHelper.GetSelectList<PriorityCR>();
                    vm.StatusOptions = EnumHelper.GetSelectList<StatusCR>();
                    vm.ActionOptions = EnumHelper.GetSelectList<ActionCR>();
                    vm.Elements = elements.Select(x => new SelectListItem { Text = $"{x.Name} - {x.Project.Name}", Value = x.Id.ToString() });
                    return View(vm);
                }
            }

            var summary = BuildUpdateSummary(changeRequest, vm);
            var previousStatus = changeRequest.Status;
            var previousAction = changeRequest.Action;

            changeRequest.ElementId = vm.ElementId.Value;
            changeRequest.ClasificationType = vm.ClasificationType.Value;
            changeRequest.Description = vm.Description;
            changeRequest.Priority = vm.Priority.Value;
            changeRequest.Status = vm.Status.Value;
            changeRequest.Remarks = vm.Remarks;
            changeRequest.Code = vm.Code;
            changeRequest.Action = vm.Action;

            await _scmDocumentContext.SaveChangesAsync();
            await _changeRequestLifecycleService.RegisterUpdatedAsync(
                changeRequest,
                userId,
                summary,
                previousStatus,
                previousAction);

            return RedirectToAction(nameof(Index), new { elementId = vm.ElementId });
        }


        public async Task<IActionResult> Edit(int id)
        {
            var userId = GetCurrentUserId();
            var changeRequest = await _scmDocumentContext.ChangeRequests
                .Include(x => x.Element)
                .FirstOrDefaultAsync(x => x.Id == id && (x.Element.Project.UserId == userId || x.Element.Project.Members.Any(m => m.UserId == userId && m.Active)));

            if (changeRequest is null)
            {
                return NotFound();
            }

            var elements = await _scmDocumentContext.Elements.AsNoTracking().Include(x => x.Project)
                .Where(x => x.Project.UserId == userId || x.Project.Members.Any(m => m.UserId == userId && m.Active))
                .OrderBy(x => x.Name).ToListAsync();

            var vm = new ChangeRequestCreateVM
            {
                Action = changeRequest.Action,
                ElementId = changeRequest.ElementId,
                Code = changeRequest.Code,
                Priority = changeRequest.Priority,
                Status = changeRequest.Status,
                Description = changeRequest.Description,
                Remarks = changeRequest.Remarks,
                ClasificationType = changeRequest.ClasificationType,


                ClasificationOptions = EnumHelper.GetSelectList<ClasificationTypeCR>(),
                PriorityOptions = EnumHelper.GetSelectList<PriorityCR>(),
                StatusOptions = EnumHelper.GetSelectList<StatusCR>(),
                ActionOptions = EnumHelper.GetSelectList<ActionCR>(),
                Elements = elements.Select(x => new SelectListItem { Text = $"{x.Name} - {x.Project.Name}", Value = x.Id.ToString() }),

            };

            return View(vm);

        }





        public async Task<IActionResult> CreateByElementAsync(int id)
        {
            var userId = GetCurrentUserId();
            var element = await _scmDocumentContext.Elements
                .FirstOrDefaultAsync(x => x.Id == id && (x.Project.UserId == userId || x.Project.Members.Any(m => m.UserId == userId && m.Active)));

            if (element == null)
            {
                return NotFound();
            }
                

            var vm = new ChangeRequestCreateByElementVM
            {
                ElementId = id,
                ElementName = element.Name,
                Code = await GenerateNextChangeRequestCodeAsync(),
                ClasificationOptions = EnumHelper.GetSelectList<ClasificationTypeCR>(),
                PriorityOptions = EnumHelper.GetSelectList<PriorityCR>(),
                StatusOptions = EnumHelper.GetSelectList<StatusCR>(),
                ActionOptions = EnumHelper.GetSelectList<ActionCR>()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateByElement(int id,ChangeRequestCreateByElementVM vm)
        {
            var userId = GetCurrentUserId();
            var element = await _scmDocumentContext.Elements
                .FirstOrDefaultAsync(x => x.Id == id && (x.Project.UserId == userId || x.Project.Members.Any(m => m.UserId == userId && m.Active)));

            if (element == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(vm.Code))
            {
                vm.Code = await GenerateNextChangeRequestCodeAsync();
            }
            vm.Code = vm.Code.Trim();

            if (!ModelState.IsValid)
            {
                vm.ClasificationOptions = EnumHelper.GetSelectList<ClasificationTypeCR>();
                vm.PriorityOptions = EnumHelper.GetSelectList<PriorityCR>();
                vm.StatusOptions = EnumHelper.GetSelectList<StatusCR>();
                vm.ActionOptions = EnumHelper.GetSelectList<ActionCR>();
                vm.ElementId = element.Id;
                vm.ElementName = element.Name;

                return View(vm);
            }

            var existsCode = await _scmDocumentContext.ChangeRequests
                .AnyAsync(x => x.Code == vm.Code);
            if (existsCode)
            {
                ModelState.AddModelError(nameof(vm.Code), "El codigo ya existe. Usa otro o acepta el sugerido.");
                vm.Code = await GenerateNextChangeRequestCodeAsync();
                vm.ClasificationOptions = EnumHelper.GetSelectList<ClasificationTypeCR>();
                vm.PriorityOptions = EnumHelper.GetSelectList<PriorityCR>();
                vm.StatusOptions = EnumHelper.GetSelectList<StatusCR>();
                vm.ActionOptions = EnumHelper.GetSelectList<ActionCR>();
                vm.ElementId = element.Id;
                vm.ElementName = element.Name;
                return View(vm);
            }

            if (vm.Status == StatusCR.Baselined && vm.Action != ActionCR.Approved)
            {
                ModelState.AddModelError(nameof(vm.Action), "Para baselinar, la accion debe ser Aprobado.");
            }

            if (vm.Status == StatusCR.Baselined)
            {
                ModelState.AddModelError(nameof(vm.Status), "No puedes baselinar una CR nueva sin evidencia Git. Vincula commit o PR primero.");
            }

            if (!ModelState.IsValid)
            {
                vm.ClasificationOptions = EnumHelper.GetSelectList<ClasificationTypeCR>();
                vm.PriorityOptions = EnumHelper.GetSelectList<PriorityCR>();
                vm.StatusOptions = EnumHelper.GetSelectList<StatusCR>();
                vm.ActionOptions = EnumHelper.GetSelectList<ActionCR>();
                vm.ElementId = element.Id;
                vm.ElementName = element.Name;
                return View(vm);
            }

            var changeRequest = new ChangeRequest
            {
                ElementId = element.Id,
                ClasificationType = vm.ClasificationType,
                Description = vm.Description,
                Priority = vm.Priority,
                Status = vm.Status,
                Remarks = vm.Remarks,
                Code = vm.Code,
                Action = vm.Action,
                CreatedAt = DateTime.UtcNow
            };

            _scmDocumentContext.ChangeRequests.Add(changeRequest);
            await _scmDocumentContext.SaveChangesAsync();
            await _changeRequestLifecycleService.RegisterCreatedAsync(
                changeRequest,
                userId,
                $"CR creada desde elemento {element.Name}.");

            return RedirectToAction(nameof(Index), new { elementId = element.Id });
        }

        public async Task<IActionResult> Details(int id)
        {
            var userId = GetCurrentUserId();
            var changeRequest = await _scmDocumentContext.ChangeRequests
                .Include(x => x.Element)
                .FirstOrDefaultAsync(x => x.Id == id && (x.Element.Project.UserId == userId || x.Element.Project.Members.Any(m => m.UserId == userId && m.Active)));
            if (changeRequest is null)
            {
                return NotFound();
            }

            var audits = await _scmDocumentContext.ChangeRequestAudits
                .AsNoTracking()
                .Where(x => x.ChangeRequestId == id)
                .OrderByDescending(x => x.ChangedAt)
                .Select(x => new ChangeRequestAuditVM
                {
                    ChangedAt = x.ChangedAt,
                    ChangedByUserId = x.ChangedByUserId,
                    EventType = x.EventType,
                    Summary = x.Summary
                }).ToListAsync();

            var statusHistory = audits
                .Where(x => x.EventType == "Historial de proceso")
                .ToList();

            var versions = await _scmDocumentContext.Versions
                .AsNoTracking()
                .Where(x => x.ChangeRequestId == id && (x.Element.Project.UserId == userId || x.Element.Project.Members.Any(m => m.UserId == userId && m.Active)))
                .OrderByDescending(x => x.UploadDate)
                .Select(x => new ChangeRequestVersionTraceVM
                {
                    VersionId = x.Id,
                    VersionCode = x.VersionCode,
                    State = x.State,
                    Phase = x.Phase,
                    Iteration = x.iteration
                })
                .ToListAsync();

            var gitTraceLinks = await _scmDocumentContext.GitTraceLinks
                .AsNoTracking()
                .Where(x => x.ChangeRequestId == id)
                .OrderByDescending(x => x.LinkedAt)
                .Select(x => new GitTraceLinkVM
                {
                    Id = x.Id,
                    Repository = x.Repository,
                    CommitSha = x.CommitSha,
                    PullRequestUrl = x.PullRequestUrl,
                    PullRequestNumber = x.PullRequestNumber,
                    LinkedByUserId = x.LinkedByUserId,
                    LinkedAt = x.LinkedAt,
                    VersionId = x.VersionId
                })
                .ToListAsync();

            var vm = new ChangeRequestDetailsVM
            {
                Id = changeRequest.Id,
                Code = changeRequest.Code,
                Description = changeRequest.Description,
                Priority = EnumHelper.GetDisplayName(changeRequest.Priority),
                Clasification = EnumHelper.GetDisplayName(changeRequest.ClasificationType),
                Status = EnumHelper.GetDisplayName(changeRequest.Status),
                Action = EnumHelper.GetDisplayName(changeRequest.Action ?? ActionCR.InWait),
                ApprovalStatus = changeRequest.ApprovalStatus.HasValue ? EnumHelper.GetDisplayName(changeRequest.ApprovalStatus.Value) : "No solicitado",
                ApprovalAssigneeUserId = changeRequest.ApprovalAssigneeUserId,
                ApprovalRequestedAt = changeRequest.ApprovalRequestedAt,
                ApprovalDueAt = changeRequest.ApprovalDueAt,
                ApprovalDecidedAt = changeRequest.ApprovalDecidedAt,
                IsPendingApproval = changeRequest.ApprovalStatus == Models.ApprovalStatus.Pending,
                IsCurrentUserApprover = !string.IsNullOrWhiteSpace(changeRequest.ApprovalAssigneeUserId) && changeRequest.ApprovalAssigneeUserId == userId,
                IsSlaExpired = changeRequest.ApprovalStatus == Models.ApprovalStatus.Pending && changeRequest.ApprovalDueAt.HasValue && changeRequest.ApprovalDueAt.Value < DateTime.UtcNow,
                Versions = versions,
                GitTraceLinks = gitTraceLinks,
                Audits = audits,
                StateHistory = statusHistory
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> TraceabilityReport(int id)
        {
            var userId = GetCurrentUserId();
            var changeRequest = await _scmDocumentContext.ChangeRequests
                .AsNoTracking()
                .Include(x => x.Element)
                .FirstOrDefaultAsync(x => x.Id == id && (x.Element.Project.UserId == userId || x.Element.Project.Members.Any(m => m.UserId == userId && m.Active)));
            if (changeRequest is null)
            {
                return NotFound();
            }

            var versions = await _scmDocumentContext.Versions
                .AsNoTracking()
                .Where(x => x.ChangeRequestId == id && (x.Element.Project.UserId == userId || x.Element.Project.Members.Any(m => m.UserId == userId && m.Active)))
                .OrderByDescending(x => x.UploadDate)
                .Select(x => new
                {
                    x.Id,
                    x.VersionCode,
                    x.State,
                    x.Phase,
                    Iteration = x.iteration,
                    IsBaselined = x.State == VersionController.VERSION_STATE_ACTIVE
                })
                .ToListAsync();

            var links = await _scmDocumentContext.GitTraceLinks
                .AsNoTracking()
                .Where(x => x.ChangeRequestId == id)
                .OrderByDescending(x => x.LinkedAt)
                .Select(x => new
                {
                    x.Id,
                    x.Repository,
                    x.CommitSha,
                    x.PullRequestUrl,
                    x.PullRequestNumber,
                    x.VersionId,
                    x.LinkedByUserId,
                    x.LinkedAt
                })
                .ToListAsync();

            var report = new
            {
                ChangeRequest = new
                {
                    changeRequest.Id,
                    changeRequest.Code,
                    Status = EnumHelper.GetDisplayName(changeRequest.Status),
                    Action = EnumHelper.GetDisplayName(changeRequest.Action ?? ActionCR.InWait)
                },
                Summary = new
                {
                    VersionsCount = versions.Count,
                    GitLinksCount = links.Count,
                    HasBaseline = versions.Any(x => x.IsBaselined)
                },
                Versions = versions,
                GitLinks = links
            };

            return Ok(report);
        }

        [HttpGet]
        public async Task<IActionResult> ExportTraceabilityCsv(int id)
        {
            var userId = GetCurrentUserId();
            var changeRequest = await _scmDocumentContext.ChangeRequests
                .AsNoTracking()
                .Include(x => x.Element)
                .FirstOrDefaultAsync(x => x.Id == id && (x.Element.Project.UserId == userId || x.Element.Project.Members.Any(m => m.UserId == userId && m.Active)));
            if (changeRequest is null)
            {
                return NotFound();
            }

            var links = await _scmDocumentContext.GitTraceLinks
                .AsNoTracking()
                .Where(x => x.ChangeRequestId == id)
                .OrderByDescending(x => x.LinkedAt)
                .ToListAsync();

            var versionMap = await _scmDocumentContext.Versions
                .AsNoTracking()
                .Where(x => x.ChangeRequestId == id && (x.Element.Project.UserId == userId || x.Element.Project.Members.Any(m => m.UserId == userId && m.Active)))
                .ToDictionaryAsync(x => x.Id, x => x);

            var builder = new StringBuilder();
            builder.AppendLine("CrId,CrCode,VersionId,VersionCode,VersionState,Phase,Iteration,Repository,CommitSha,PullRequestNumber,PullRequestUrl,LinkedBy,LinkedAtUtc");

            if (links.Count == 0)
            {
                builder.AppendLine($"{changeRequest.Id},{EscapeCsv(changeRequest.Code)},,,,,,,,,,,");
            }
            else
            {
                foreach (var link in links)
                {
                    versionMap.TryGetValue(link.VersionId ?? 0, out var version);
                    builder.AppendLine(string.Join(',',
                        changeRequest.Id,
                        EscapeCsv(changeRequest.Code),
                        link.VersionId?.ToString() ?? string.Empty,
                        EscapeCsv(version?.VersionCode ?? string.Empty),
                        EscapeCsv(version?.State ?? string.Empty),
                        version?.Phase.ToString() ?? string.Empty,
                        version?.iteration.ToString() ?? string.Empty,
                        EscapeCsv(link.Repository),
                        EscapeCsv(link.CommitSha ?? string.Empty),
                        link.PullRequestNumber?.ToString() ?? string.Empty,
                        EscapeCsv(link.PullRequestUrl ?? string.Empty),
                        EscapeCsv(link.LinkedByUserId),
                        EscapeCsv(link.LinkedAt.ToString("yyyy-MM-dd HH:mm:ss"))));
                }
            }

            var bytes = Encoding.UTF8.GetBytes(builder.ToString());
            return File(bytes, "text/csv; charset=utf-8", $"traceability-cr-{id}-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
        }

        [HttpGet]
        public async Task<IActionResult> ExportTraceabilityExcel(int id)
        {
            var userId = GetCurrentUserId();
            var changeRequest = await _scmDocumentContext.ChangeRequests
                .AsNoTracking()
                .Include(x => x.Element)
                .FirstOrDefaultAsync(x => x.Id == id && (x.Element.Project.UserId == userId || x.Element.Project.Members.Any(m => m.UserId == userId && m.Active)));
            if (changeRequest is null)
            {
                return NotFound();
            }

            var rows = await BuildTraceabilityRowsAsync(id, userId, changeRequest);
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Traceability");
            var headers = new[]
            {
                "CrId", "CrCode", "VersionId", "VersionCode", "VersionState", "Phase", "Iteration",
                "Repository", "CommitSha", "PullRequestNumber", "PullRequestUrl", "LinkedBy", "LinkedAtUtc"
            };
            for (var i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(1, i + 1).Value = headers[i];
                worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
            }

            var rowIndex = 2;
            foreach (var row in rows)
            {
                worksheet.Cell(rowIndex, 1).Value = row.CrId;
                worksheet.Cell(rowIndex, 2).Value = row.CrCode;
                worksheet.Cell(rowIndex, 3).Value = row.VersionId;
                worksheet.Cell(rowIndex, 4).Value = row.VersionCode;
                worksheet.Cell(rowIndex, 5).Value = row.VersionState;
                worksheet.Cell(rowIndex, 6).Value = row.Phase;
                worksheet.Cell(rowIndex, 7).Value = row.Iteration;
                worksheet.Cell(rowIndex, 8).Value = row.Repository;
                worksheet.Cell(rowIndex, 9).Value = row.CommitSha;
                worksheet.Cell(rowIndex, 10).Value = row.PullRequestNumber;
                worksheet.Cell(rowIndex, 11).Value = row.PullRequestUrl;
                worksheet.Cell(rowIndex, 12).Value = row.LinkedBy;
                worksheet.Cell(rowIndex, 13).Value = row.LinkedAtUtc;
                rowIndex++;
            }

            worksheet.Columns().AdjustToContents();
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var bytes = stream.ToArray();
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"traceability-cr-{id}-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
        }

        [HttpGet]
        public async Task<IActionResult> ExportTraceabilityPdf(int id)
        {
            var userId = GetCurrentUserId();
            var changeRequest = await _scmDocumentContext.ChangeRequests
                .AsNoTracking()
                .Include(x => x.Element)
                .FirstOrDefaultAsync(x => x.Id == id && (x.Element.Project.UserId == userId || x.Element.Project.Members.Any(m => m.UserId == userId && m.Active)));
            if (changeRequest is null)
            {
                return NotFound();
            }

            var rows = await BuildTraceabilityRowsAsync(id, userId, changeRequest);
            var bytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Size(PageSizes.A4);
                    page.Header().Text($"Traceability Report - CR {changeRequest.Code}").FontSize(16).Bold();
                    page.Content().Column(column =>
                    {
                        column.Spacing(6);
                        column.Item().Text($"CR ID: {changeRequest.Id}");
                        column.Item().Text($"Generated UTC: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
                        column.Item().LineHorizontal(1);
                        foreach (var row in rows)
                        {
                            column.Item().Text(
                                $"Version {row.VersionCode} ({row.VersionState}) | Repo: {row.Repository} | Commit: {row.CommitSha} | PR: {row.PullRequestNumber} | LinkedBy: {row.LinkedBy} | LinkedAt: {row.LinkedAtUtc}");
                        }
                    });
                });
            }).GeneratePdf();

            return File(bytes, "application/pdf", $"traceability-cr-{id}-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf");
        }

        [HttpPost]
        public async Task<IActionResult> RequestApproval(int id, string approverUserId, int slaHours = 48)
        {
            var userId = GetCurrentUserId();
            var changeRequest = await _scmDocumentContext.ChangeRequests
                .Include(x => x.Element)
                .FirstOrDefaultAsync(x => x.Id == id && (x.Element.Project.UserId == userId || x.Element.Project.Members.Any(m => m.UserId == userId && m.Active)));

            if (changeRequest is null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(approverUserId) || slaHours is < 1 or > 720)
            {
                TempData["Message"] = "Debe indicar responsable y un SLA entre 1 y 720 horas.";
                TempData["MessageType"] = "warning";
                return RedirectToAction(nameof(Details), new { id });
            }

            changeRequest.ApprovalAssigneeUserId = approverUserId.Trim();
            changeRequest.ApprovalRequestedAt = DateTime.UtcNow;
            changeRequest.ApprovalDueAt = DateTime.UtcNow.AddHours(slaHours);
            changeRequest.ApprovalDecidedAt = null;
            changeRequest.ApprovalStatus = Models.ApprovalStatus.Pending;

            _scmDocumentContext.ChangeRequestAudits.Add(new ChangeRequestAudit
            {
                ChangeRequestId = changeRequest.Id,
                ChangedAt = DateTime.UtcNow,
                ChangedByUserId = userId,
                EventType = "Aprobacion solicitada",
                Summary = $"Se solicito aprobacion formal a {changeRequest.ApprovalAssigneeUserId} con SLA de {slaHours} horas."
            });

            await _scmDocumentContext.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> AddGitTrace(int id, string repository, string? commitSha, string? pullRequestUrl, int? pullRequestNumber, int? versionId)
        {
            var userId = GetCurrentUserId();
            var changeRequest = await _scmDocumentContext.ChangeRequests
                .Include(x => x.Element)
                .FirstOrDefaultAsync(x => x.Id == id && (x.Element.Project.UserId == userId || x.Element.Project.Members.Any(m => m.UserId == userId && m.Active)));

            if (changeRequest is null)
            {
                return NotFound();
            }

            if (!TryNormalizeGitInputs(ref repository, ref commitSha, ref pullRequestUrl, ref pullRequestNumber))
            {
                TrySetTempMessage("Repositorio invalido. Usa owner/repo o pega una URL de GitHub.", "warning");
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(commitSha) && string.IsNullOrWhiteSpace(pullRequestUrl) && !pullRequestNumber.HasValue)
            {
                TrySetTempMessage("Debes vincular al menos commit o PR.", "warning");
                return RedirectToAction(nameof(Details), new { id });
            }

            if (!await ValidateGithubTraceAsync(repository, commitSha, pullRequestNumber))
            {
                return RedirectToAction(nameof(Details), new { id });
            }

            if (versionId.HasValue)
            {
                var validVersion = await _scmDocumentContext.Versions
                    .AnyAsync(x => x.Id == versionId.Value && x.ChangeRequestId == id && (x.Element.Project.UserId == userId || x.Element.Project.Members.Any(m => m.UserId == userId && m.Active)));
                if (!validVersion)
                {
                    TrySetTempMessage("La version indicada no pertenece a la CR.", "warning");
                    return RedirectToAction(nameof(Details), new { id });
                }
            }

            var link = new GitTraceLink
            {
                ChangeRequestId = id,
                VersionId = versionId,
                Repository = repository.Trim(),
                CommitSha = string.IsNullOrWhiteSpace(commitSha) ? null : commitSha.Trim(),
                PullRequestUrl = string.IsNullOrWhiteSpace(pullRequestUrl) ? null : pullRequestUrl.Trim(),
                PullRequestNumber = pullRequestNumber,
                LinkedByUserId = userId,
                LinkedAt = DateTime.UtcNow
            };

            _scmDocumentContext.GitTraceLinks.Add(link);
            _scmDocumentContext.ChangeRequestAudits.Add(new ChangeRequestAudit
            {
                ChangeRequestId = id,
                ChangedAt = DateTime.UtcNow,
                ChangedByUserId = userId,
                EventType = "Trazabilidad Git",
                Summary = $"Vinculado repo '{link.Repository}', commit '{link.CommitSha ?? "-"}', PR '{link.PullRequestNumber?.ToString() ?? "-"}'."
            });
            await _scmDocumentContext.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateGitTrace(int id, int traceId, string repository, string? commitSha, string? pullRequestUrl, int? pullRequestNumber, int? versionId)
        {
            var userId = GetCurrentUserId();
            var trace = await _scmDocumentContext.GitTraceLinks
                .Include(x => x.ChangeRequest)
                .ThenInclude(x => x.Element)
                .FirstOrDefaultAsync(x => x.Id == traceId && x.ChangeRequestId == id && (x.ChangeRequest.Element.Project.UserId == userId || x.ChangeRequest.Element.Project.Members.Any(m => m.UserId == userId && m.Active)));
            if (trace is null)
            {
                return NotFound();
            }

            if (!TryNormalizeGitInputs(ref repository, ref commitSha, ref pullRequestUrl, ref pullRequestNumber))
            {
                TrySetTempMessage("Repositorio invalido. Usa owner/repo o pega una URL de GitHub.", "warning");
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(commitSha) && string.IsNullOrWhiteSpace(pullRequestUrl) && !pullRequestNumber.HasValue)
            {
                TrySetTempMessage("Debes vincular al menos commit o PR.", "warning");
                return RedirectToAction(nameof(Details), new { id });
            }

            if (!await ValidateGithubTraceAsync(repository, commitSha, pullRequestNumber))
            {
                return RedirectToAction(nameof(Details), new { id });
            }

            if (versionId.HasValue)
            {
                var validVersion = await _scmDocumentContext.Versions
                    .AnyAsync(x => x.Id == versionId.Value && x.ChangeRequestId == id && (x.Element.Project.UserId == userId || x.Element.Project.Members.Any(m => m.UserId == userId && m.Active)));
                if (!validVersion)
                {
                    TrySetTempMessage("La version indicada no pertenece a la CR.", "warning");
                    return RedirectToAction(nameof(Details), new { id });
                }
            }

            trace.Repository = repository.Trim();
            trace.CommitSha = string.IsNullOrWhiteSpace(commitSha) ? null : commitSha.Trim();
            trace.PullRequestNumber = pullRequestNumber;
            trace.PullRequestUrl = string.IsNullOrWhiteSpace(pullRequestUrl) ? null : pullRequestUrl.Trim();
            trace.VersionId = versionId;
            trace.LinkedAt = DateTime.UtcNow;
            trace.LinkedByUserId = userId;

            _scmDocumentContext.ChangeRequestAudits.Add(new ChangeRequestAudit
            {
                ChangeRequestId = id,
                ChangedAt = DateTime.UtcNow,
                ChangedByUserId = userId,
                EventType = "Trazabilidad Git",
                Summary = $"Vinculo git actualizado {traceId}. Repo '{trace.Repository}', commit '{trace.CommitSha ?? "-"}', PR '{trace.PullRequestNumber?.ToString() ?? "-"}'."
            });
            await _scmDocumentContext.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveGitTrace(int id, int traceId)
        {
            var userId = GetCurrentUserId();
            var trace = await _scmDocumentContext.GitTraceLinks
                .Include(x => x.ChangeRequest)
                .ThenInclude(x => x.Element)
                .FirstOrDefaultAsync(x => x.Id == traceId && x.ChangeRequestId == id && (x.ChangeRequest.Element.Project.UserId == userId || x.ChangeRequest.Element.Project.Members.Any(m => m.UserId == userId && m.Active)));
            if (trace is null)
            {
                return NotFound();
            }

            _scmDocumentContext.GitTraceLinks.Remove(trace);
            _scmDocumentContext.ChangeRequestAudits.Add(new ChangeRequestAudit
            {
                ChangeRequestId = id,
                ChangedAt = DateTime.UtcNow,
                ChangedByUserId = userId,
                EventType = "Trazabilidad Git",
                Summary = $"Se elimino vinculo git {traceId}."
            });
            await _scmDocumentContext.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> Approve(int id)
        {
            var userId = GetCurrentUserId();
            var changeRequest = await _scmDocumentContext.ChangeRequests
                .Include(x => x.Element)
                .FirstOrDefaultAsync(x => x.Id == id && (x.Element.Project.UserId == userId || x.Element.Project.Members.Any(m => m.UserId == userId && m.Active)));

            if (changeRequest is null)
            {
                return NotFound();
            }

            if (changeRequest.ApprovalAssigneeUserId != userId)
            {
                return Forbid();
            }

            if (changeRequest.ApprovalStatus != Models.ApprovalStatus.Pending)
            {
                return BadRequest();
            }

            if (changeRequest.ApprovalDueAt.HasValue && changeRequest.ApprovalDueAt.Value < DateTime.UtcNow)
            {
                changeRequest.ApprovalStatus = Models.ApprovalStatus.Expired;
                changeRequest.ApprovalDecidedAt = DateTime.UtcNow;
                await _scmDocumentContext.SaveChangesAsync();
                return BadRequest();
            }

            var previousStatus = changeRequest.Status;
            var previousAction = changeRequest.Action;
            changeRequest.ApprovalStatus = Models.ApprovalStatus.Approved;
            changeRequest.ApprovalDecidedAt = DateTime.UtcNow;
            changeRequest.Action = ActionCR.Approved;
            await _scmDocumentContext.SaveChangesAsync();
            await _changeRequestLifecycleService.RegisterUpdatedAsync(changeRequest, userId, "Aprobacion formal completada.", previousStatus, previousAction);
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> Reject(int id)
        {
            var userId = GetCurrentUserId();
            var changeRequest = await _scmDocumentContext.ChangeRequests
                .Include(x => x.Element)
                .FirstOrDefaultAsync(x => x.Id == id && (x.Element.Project.UserId == userId || x.Element.Project.Members.Any(m => m.UserId == userId && m.Active)));

            if (changeRequest is null)
            {
                return NotFound();
            }

            if (changeRequest.ApprovalAssigneeUserId != userId)
            {
                return Forbid();
            }

            if (changeRequest.ApprovalStatus != Models.ApprovalStatus.Pending)
            {
                return BadRequest();
            }

            var previousStatus = changeRequest.Status;
            var previousAction = changeRequest.Action;
            changeRequest.ApprovalStatus = Models.ApprovalStatus.Rejected;
            changeRequest.ApprovalDecidedAt = DateTime.UtcNow;
            changeRequest.Action = ActionCR.Rejected;
            await _scmDocumentContext.SaveChangesAsync();
            await _changeRequestLifecycleService.RegisterUpdatedAsync(changeRequest, userId, "Aprobacion formal rechazada.", previousStatus, previousAction);
            return RedirectToAction(nameof(Details), new { id });
        }

        private string GetCurrentUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        }

        private async Task<string> GenerateNextChangeRequestCodeAsync()
        {
            var now = DateTime.Now;
            var year = now.Year;
            var dateHourStamp = now.ToString("ddHHmmss");
            var prefix = $"CR-{year}-{dateHourStamp}-";
            var regex = new Regex($@"^CR-{year}-{dateHourStamp}-(\d+)$", RegexOptions.Compiled);

            var codes = await _scmDocumentContext.ChangeRequests
                .AsNoTracking()
                .Where(x => x.Code.StartsWith(prefix))
                .Select(x => x.Code)
                .ToListAsync();

            var max = 0;
            foreach (var code in codes)
            {
                var match = regex.Match(code);
                if (!match.Success)
                {
                    continue;
                }

                if (int.TryParse(match.Groups[1].Value, out var number) && number > max)
                {
                    max = number;
                }
            }

            return $"{prefix}{(max + 1):000}";
        }

        private static bool IsValidStatusTransition(StatusCR current, StatusCR next)
        {
            if (current == next)
            {
                return true;
            }

            return _nextStatusByCurrent.TryGetValue(current, out var expectedNext) && expectedNext == next;
        }

        private static IQueryable<ChangeRequest> ApplySorting(IQueryable<ChangeRequest> query, string sortBy, string sortDir)
        {
            var isAsc = sortDir == "asc";

            return sortBy switch
            {
                "code" => isAsc ? query.OrderBy(c => c.Code) : query.OrderByDescending(c => c.Code),
                "status" => isAsc ? query.OrderBy(c => c.Status) : query.OrderByDescending(c => c.Status),
                "action" => isAsc ? query.OrderBy(c => c.Action) : query.OrderByDescending(c => c.Action),
                _ => isAsc ? query.OrderBy(c => c.CreatedAt) : query.OrderByDescending(c => c.CreatedAt)
            };
        }

        private static string NormalizeSortBy(string? sortBy)
        {
            return sortBy switch
            {
                "code" => "code",
                "status" => "status",
                "action" => "action",
                _ => "createdAt"
            };
        }

        private static string NormalizeSortDir(string? sortDir)
        {
            return sortDir == "asc" ? "asc" : "desc";
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }

        private string BuildUpdateSummary(ChangeRequest current, ChangeRequestCreateVM incoming)
        {
            var changes = new List<string>();
            if (!string.Equals(current.Code, incoming.Code, StringComparison.Ordinal))
            {
                changes.Add("codigo");
            }

            if (current.Status != incoming.Status!.Value)
            {
                changes.Add($"proceso ({EnumHelper.GetDisplayName(current.Status)} -> {EnumHelper.GetDisplayName(incoming.Status.Value)})");
            }

            if (current.Action != incoming.Action)
            {
                var currentAction = current.Action.HasValue ? EnumHelper.GetDisplayName(current.Action.Value) : "N/A";
                var nextAction = incoming.Action.HasValue ? EnumHelper.GetDisplayName(incoming.Action.Value) : "N/A";
                changes.Add($"estado ({currentAction} -> {nextAction})");
            }

            if (current.Priority != incoming.Priority!.Value)
            {
                changes.Add("prioridad");
            }

            if (current.ClasificationType != incoming.ClasificationType!.Value)
            {
                changes.Add("clasificacion");
            }

            if (current.ElementId != incoming.ElementId!.Value)
            {
                changes.Add("elemento");
            }

            if (!string.Equals(current.Description, incoming.Description, StringComparison.Ordinal))
            {
                changes.Add("descripcion");
            }

            if (!string.Equals(current.Remarks, incoming.Remarks, StringComparison.Ordinal))
            {
                changes.Add("observaciones");
            }

            return changes.Count == 0 ? "No se detectaron cambios de campos." : $"Campos actualizados: {string.Join(", ", changes)}.";
        }

        private static IEnumerable<SelectListItem> BuildPhaseOptions()
        {
            return new List<SelectListItem>
            {
                new() { Value = "1", Text = "Planificacion" },
                new() { Value = "2", Text = "Analisis" },
                new() { Value = "3", Text = "Diseno" },
                new() { Value = "4", Text = "Implementacion" },
                new() { Value = "5", Text = "Prueba" },
                new() { Value = "6", Text = "Mantenimiento" }
            };
        }

        private async Task<List<TraceabilityRow>> BuildTraceabilityRowsAsync(int id, string userId, ChangeRequest changeRequest)
        {
            var links = await _scmDocumentContext.GitTraceLinks
                .AsNoTracking()
                .Where(x => x.ChangeRequestId == id)
                .OrderByDescending(x => x.LinkedAt)
                .ToListAsync();

            var versionMap = await _scmDocumentContext.Versions
                .AsNoTracking()
                .Where(x => x.ChangeRequestId == id && (x.Element.Project.UserId == userId || x.Element.Project.Members.Any(m => m.UserId == userId && m.Active)))
                .ToDictionaryAsync(x => x.Id, x => x);

            var rows = new List<TraceabilityRow>();
            if (links.Count == 0)
            {
                rows.Add(new TraceabilityRow
                {
                    CrId = changeRequest.Id,
                    CrCode = changeRequest.Code
                });
                return rows;
            }

            foreach (var link in links)
            {
                versionMap.TryGetValue(link.VersionId ?? 0, out var version);
                rows.Add(new TraceabilityRow
                {
                    CrId = changeRequest.Id,
                    CrCode = changeRequest.Code,
                    VersionId = link.VersionId,
                    VersionCode = version?.VersionCode,
                    VersionState = version?.State,
                    Phase = version?.Phase,
                    Iteration = version?.iteration,
                    Repository = link.Repository,
                    CommitSha = link.CommitSha,
                    PullRequestNumber = link.PullRequestNumber,
                    PullRequestUrl = link.PullRequestUrl,
                    LinkedBy = link.LinkedByUserId,
                    LinkedAtUtc = link.LinkedAt.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }

            return rows;
        }

        private class TraceabilityRow
        {
            public int CrId { get; set; }
            public string CrCode { get; set; } = string.Empty;
            public int? VersionId { get; set; }
            public string? VersionCode { get; set; }
            public string? VersionState { get; set; }
            public int? Phase { get; set; }
            public int? Iteration { get; set; }
            public string? Repository { get; set; }
            public string? CommitSha { get; set; }
            public int? PullRequestNumber { get; set; }
            public string? PullRequestUrl { get; set; }
            public string? LinkedBy { get; set; }
            public string? LinkedAtUtc { get; set; }
        }

        private void TrySetTempMessage(string message, string type)
        {
            if (TempData is null)
            {
                return;
            }

            TempData["Message"] = message;
            TempData["MessageType"] = type;
        }

        private async Task<bool> ValidateGithubTraceAsync(string repository, string? commitSha, int? pullRequestNumber)
        {
            if (!_githubService.TryParseRepository(repository, out var owner, out var repo))
            {
                TrySetTempMessage("Formato de repositorio invalido. Usa owner/repo.", "warning");
                return false;
            }

            var repositoryCheck = await _githubService.CheckRepositoryAsync(owner, repo);
            if (repositoryCheck.Status == GitHubCheckStatus.Unavailable)
            {
                TrySetTempMessage("GitHub no esta disponible temporalmente (rate limit o error externo). Intenta nuevamente en unos minutos.", "warning");
                return false;
            }

            if (repositoryCheck.Status != GitHubCheckStatus.Valid)
            {
                TrySetTempMessage("El repositorio indicado no existe o no es accesible con el token configurado.", "warning");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(commitSha))
            {
                var commitCheck = await _githubService.CheckCommitAsync(owner, repo, commitSha.Trim());
                if (commitCheck.Status == GitHubCheckStatus.Unavailable)
                {
                    TrySetTempMessage("No se pudo validar el commit por indisponibilidad temporal de GitHub. Intenta nuevamente.", "warning");
                    return false;
                }

                if (commitCheck.Status != GitHubCheckStatus.Valid)
                {
                    TrySetTempMessage("El commit indicado no existe en el repositorio.", "warning");
                    return false;
                }
            }

            if (pullRequestNumber.HasValue)
            {
                var pullRequestCheck = await _githubService.CheckPullRequestAsync(owner, repo, pullRequestNumber.Value);
                if (pullRequestCheck.Status == GitHubCheckStatus.Unavailable)
                {
                    TrySetTempMessage("No se pudo validar el Pull Request por indisponibilidad temporal de GitHub. Intenta nuevamente.", "warning");
                    return false;
                }

                if (pullRequestCheck.Status != GitHubCheckStatus.Valid)
                {
                    TrySetTempMessage("El Pull Request indicado no existe en el repositorio.", "warning");
                    return false;
                }
            }

            return true;
        }

        private bool TryNormalizeGitInputs(ref string repository, ref string? commitSha, ref string? pullRequestUrl, ref int? pullRequestNumber)
        {
            if (string.IsNullOrWhiteSpace(repository))
            {
                if (!string.IsNullOrWhiteSpace(pullRequestUrl) && _githubService.TryNormalizeRepository(pullRequestUrl, out var fromPrUrl))
                {
                    repository = fromPrUrl;
                }
            }
            else if (_githubService.TryNormalizeRepository(repository, out var normalizedRepository))
            {
                repository = normalizedRepository;
            }

            if (string.IsNullOrWhiteSpace(repository))
            {
                return false;
            }

            if (!pullRequestNumber.HasValue &&
                !string.IsNullOrWhiteSpace(pullRequestUrl) &&
                _githubService.TryExtractPullRequestNumber(pullRequestUrl, out var extractedPr))
            {
                pullRequestNumber = extractedPr;
            }

            if (string.IsNullOrWhiteSpace(commitSha) &&
                !string.IsNullOrWhiteSpace(pullRequestUrl) &&
                _githubService.TryExtractCommitSha(pullRequestUrl, out var extractedSha))
            {
                commitSha = extractedSha;
            }

            return true;
        }

        private async Task<(bool IsValid, string Message)> ValidateBaselineEvidenceAsync(int changeRequestId)
        {
            var links = await _scmDocumentContext.GitTraceLinks
                .AsNoTracking()
                .Where(x => x.ChangeRequestId == changeRequestId)
                .ToListAsync();

            if (links.Count == 0)
            {
                return (false, "No puedes baselinar: la CR no tiene vinculos Git. Agrega un commit valido o un PR merged.");
            }

            var checkedItems = 0;

            foreach (var link in links)
            {
                if (!_githubService.TryParseRepository(link.Repository, out var owner, out var repo))
                {
                    continue;
                }

                checkedItems++;

                if (!string.IsNullOrWhiteSpace(link.CommitSha))
                {
                    var commitCheck = await _githubService.CheckCommitAsync(owner, repo, link.CommitSha.Trim());
                    if (commitCheck.Status == GitHubCheckStatus.Valid)
                    {
                        return (true, string.Empty);
                    }
                }

                if (link.PullRequestNumber.HasValue)
                {
                    var pullRequestCheck = await _githubService.CheckPullRequestMergedAsync(owner, repo, link.PullRequestNumber.Value);
                    if (pullRequestCheck.Status == GitHubCheckStatus.Valid)
                    {
                        return (true, string.Empty);
                    }

                    if (pullRequestCheck.Status == GitHubCheckStatus.Unavailable)
                    {
                        return (false, "No puedes baselinar ahora: GitHub no esta disponible temporalmente para validar PR merged.");
                    }
                }
            }

            if (checkedItems == 0)
            {
                return (false, "No puedes baselinar: los vinculos Git tienen formato de repositorio invalido. Usa owner/repo.");
            }

            return (false, "No puedes baselinar: no hay evidencia Git verificable (commit existente o PR merged).");
        }

    }
}
