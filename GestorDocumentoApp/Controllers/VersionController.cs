using GestorDocumentoApp.Data;
using GestorDocumentoApp.Models;
using GestorDocumentoApp.Services;
using GestorDocumentoApp.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace GestorDocumentoApp.Controllers
{
    public class VersionController : Controller
    {
        private readonly ScmDocumentContext _scmDocumentContext;
        private readonly ILogger<VersionController> _logger;
        private readonly ChangeRequestLifecycleService _changeRequestLifecycleService;

        public const string VERSION_STATE_ACTIVE = "active";
        public const string VERSION_STATE_INACTIVE = "inactive";

        public VersionController(ScmDocumentContext scmDocumentContext, ILogger<VersionController> logger, ChangeRequestLifecycleService changeRequestLifecycleService)
        {
            _scmDocumentContext = scmDocumentContext;
            _logger = logger;
            _changeRequestLifecycleService = changeRequestLifecycleService;
        }

        public async Task<IActionResult> Index(int elementId)
        {
            var userId = GetCurrentUserId();
            var element = await _scmDocumentContext.Elements.Include(x => x.Versions.OrderByDescending(x => x.UploadDate))
                .ThenInclude(x => x.ParentVersion!)
                .ThenInclude(x => x.RequirementType).FirstOrDefaultAsync(x => x.Id == elementId && x.Project.UserId == userId);

            if (element == null)
            {
                return NotFound();
            }

            return View(element);


        }

        [HttpGet]
        public async Task<IActionResult> Compare(int elementId, int sourceVersionId, int targetVersionId)
        {
            var userId = GetCurrentUserId();
            if (sourceVersionId == targetVersionId)
            {
                TempData["Message"] = "Selecciona dos versiones diferentes para comparar.";
                TempData["MessageType"] = "warning";
                return RedirectToAction(nameof(Index), new { elementId });
            }

            var versions = await _scmDocumentContext.Versions
                .AsNoTracking()
                .Include(x => x.RequirementType)
                .Include(x => x.ChangeRequest)
                .Where(x => x.ElementId == elementId && x.Element.Project.UserId == userId)
                .Where(x => x.Id == sourceVersionId || x.Id == targetVersionId)
                .ToListAsync();

            var source = versions.FirstOrDefault(x => x.Id == sourceVersionId);
            var target = versions.FirstOrDefault(x => x.Id == targetVersionId);
            if (source is null || target is null)
            {
                return NotFound();
            }

            var elementName = await _scmDocumentContext.Elements
                .AsNoTracking()
                .Where(x => x.Id == elementId && x.Project.UserId == userId)
                .Select(x => x.Name)
                .FirstOrDefaultAsync();
            if (elementName is null)
            {
                return NotFound();
            }

            var vm = new VersionCompareVM
            {
                ElementId = elementId,
                ElementName = elementName,
                Source = ToSnapshot(source),
                Target = ToSnapshot(target),
                ChangeLog = BuildChangeLog(source, target)
            };

            return View(vm);
        }

        public async Task<IActionResult> Create(int elementId)
        {
            var userId = GetCurrentUserId();
            var element = await _scmDocumentContext.Elements
                .FirstOrDefaultAsync(x => x.Id == elementId && x.Project.UserId == userId);

            if (element is null)
            {
                return NotFound();
            }

            var requirementTypes = await _scmDocumentContext.RequirementTypes.AsNoTracking()
                .OrderBy(r => r.Name).ToListAsync();

            var versions = await _scmDocumentContext.Versions.AsNoTracking().Where(x => x.ElementId == element.Id)
                .OrderBy(v => v.VersionCode).ToListAsync();

            var changeRequests = await _scmDocumentContext.ChangeRequests
                .AsNoTracking()
                .Where(x => x.ElementId == element.Id)
                .Where(x => x.Action == ActionCR.Approved)
                .Where(x=>x.Status==StatusCR.Action).ToListAsync();

            return View(new VersionVM
            {
                ElementName = element.Name,
                ElementId = element.Id,
                RequirementTypes = requirementTypes.Select(r => new SelectListItem { Text = r.Name, Value = r.Id.ToString() }),
                PreviousVersions = versions.Select(v => new SelectListItem { Text = v.VersionCode + " | " + v.Id, Value = v.Id.ToString() }),
                ChangeRequests = changeRequests.Select(x => new SelectListItem { Text = x.Code, Value = x.Id.ToString() }),
                Phases = new SelectListItem[] {
                    new SelectListItem { Value="1",Text="Planificación"},
                    new SelectListItem { Value="2",Text="Análisis"},
                    new SelectListItem { Value="3",Text="Diseño"},
                    new SelectListItem { Value="4",Text="Implementación"},
                    new SelectListItem { Value="5",Text="Prueba"},
                    new SelectListItem { Value="6",Text="Mantenimiento"},
                }

            });
        }

        [HttpPost]
        public async Task<IActionResult> Create(int elementId, VersionVM versionVM)
        {
            try
            {
                var userId = GetCurrentUserId();
                var element = await _scmDocumentContext.Elements
                    .FirstOrDefaultAsync(x => x.Id == elementId && x.Project.UserId == userId);

                if (element is null)
                {
                    return NotFound();
                }

                if (!ModelState.IsValid)
                {
                    await LoadDropDowns(versionVM);
                    return View(versionVM);
                }

                if (versionVM.ChangeRequestId is null || versionVM.Phase is null || versionVM.Iteration is null || versionVM.RequirementTypeId is null)
                {
                    ModelState.AddModelError(string.Empty, "Debe completar todos los campos obligatorios.");
                    await LoadDropDowns(versionVM);
                    return View(versionVM);
                }

                var version = new Models.Version
                {
                    Name = versionVM.Name,
                    ElementUrl = versionVM.ElementUrl,
                    UploadDate = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc),
                    State = VERSION_STATE_INACTIVE,
                    ToolUrl = versionVM.ToolUrl,
                    VersionCode = versionVM.VersionCode,
                    ElementId = element.Id,
                    ChangeRequestId = versionVM.ChangeRequestId.Value,
                    Phase = versionVM.Phase.Value,
                    iteration = versionVM.Iteration.Value,
                    UserId = userId,
                    RequirementTypeId = versionVM.RequirementTypeId.Value,
                    ParentVersionId = versionVM.ParentVersionId
                };

                _scmDocumentContext.Add(version);
                await _scmDocumentContext.SaveChangesAsync();

                return RedirectToAction(nameof(Index), new { elementId = element.Id });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error saving Version {VersionName}", versionVM.Name);

                TempData["Message"] = "El CR ya esta asociado a una version.";
                TempData["MessageType"] = "warning";

                return RedirectToAction(nameof(Index), new { elementId = elementId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving Version {VersionName}", versionVM.Name);
                return RedirectToAction(nameof(Index), new { elementId = elementId });
            }
        }

        public async Task<IActionResult> Edit([FromRoute] int id)
        {
            var userId = GetCurrentUserId();
            var version = await _scmDocumentContext.Versions
                .FirstOrDefaultAsync(x => x.Id == id && x.Element.Project.UserId == userId);

            if (version is null)
            {
                return NotFound();
            }

            var vm = new VersionVM
            {
                Id = version.Id,
                Name = version.Name,
                ElementId = version.ElementId,
                ElementName = version.Name,
                ElementUrl = version.ElementUrl,
                UploadDate = version.UploadDate,
                ChangeRequestId=version.ChangeRequestId,
                State = version.State,
                Phase = version.Phase,
                Iteration = version.iteration,
                ToolUrl = version.ToolUrl,
                VersionCode = version.VersionCode,

                RequirementTypeId = version.RequirementTypeId,
                ParentVersionId = version.ParentVersionId

            };

            await LoadDropDowns(vm);

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> Edit([FromRoute] int id, VersionVM versionVM)
        {
            try
            {
                var userId = GetCurrentUserId();
                var version = await _scmDocumentContext.Versions
                    .FirstOrDefaultAsync(x => x.Id == id && x.Element.Project.UserId == userId);

                if (version is null)
                {
                    return NotFound();
                }

                if (!ModelState.IsValid)
                {
                    await LoadDropDowns(versionVM);
                    return View(versionVM);
                }

                if (versionVM.ChangeRequestId is null || versionVM.Phase is null || versionVM.Iteration is null || versionVM.RequirementTypeId is null)
                {
                    ModelState.AddModelError(string.Empty, "Debe completar todos los campos obligatorios.");
                    await LoadDropDowns(versionVM);
                    return View(versionVM);
                }

                version.Name = versionVM.Name;
                version.ElementUrl = versionVM.ElementUrl;
                version.UploadDate = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc);
                version.ToolUrl = versionVM.ToolUrl;
                version.iteration = versionVM.Iteration.Value;
                version.Phase = versionVM.Phase.Value;
                version.ChangeRequestId = versionVM.ChangeRequestId.Value;
                version.VersionCode = versionVM.VersionCode;
                version.RequirementTypeId = versionVM.RequirementTypeId.Value;
                version.ParentVersionId = versionVM.ParentVersionId;

                await _scmDocumentContext.SaveChangesAsync();

                return RedirectToAction(nameof(Index), new { elementId = version.ElementId });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error updating Version {VersionName}", versionVM.Name);

                TempData["Message"] = "El CR ya esta asociado a una version.";
                TempData["MessageType"] = "warning";

                return RedirectToAction(nameof(Index), new { elementId = versionVM.ElementId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving Version {VersionName}", versionVM.Name);
                return RedirectToAction(nameof(Index), new { elementId = versionVM.ElementId });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete([FromRoute] int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var version = await _scmDocumentContext.Versions
                    .FirstOrDefaultAsync(x => x.Id == id && x.Element.Project.UserId == userId);

                if (version is null)
                {
                    return NotFound();
                }

                _scmDocumentContext.Versions.Remove(version);
                await _scmDocumentContext.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting Version {VersionId}", id);
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        public async Task<IActionResult> SetVersion(int id)
        {
            var userId = GetCurrentUserId();
            var version = await _scmDocumentContext.Versions.Include(x=>x.ChangeRequest)
                .Where(x=>x.Id==id && x.Element.Project.UserId == userId).FirstOrDefaultAsync();
            if (version is null)
            {
                TempData["Message"] = "No existe la version.";
                TempData["MessageType"] = "warning";
                return NotFound();
            }


            try
            {
                var activeVersion = await _scmDocumentContext.Versions
                    .Where(x => x.ElementId == version.ElementId).AnyAsync(x => x.State == VERSION_STATE_ACTIVE);

                if (activeVersion)
                {
                    TempData["Message"] = "Ya existe una version.";
                    TempData["MessageType"] = "warning";

                    return RedirectToAction(nameof(Index), new { elementId = version.ElementId });
                }

                TempData["Message"] = "Version activada correctamente.";
                TempData["MessageType"] = "success";

                version.State = VERSION_STATE_ACTIVE;
                var previousStatus = version.ChangeRequest.Status;
                version.ChangeRequest.Status = StatusCR.Baselined;

                await _scmDocumentContext.SaveChangesAsync();
                await _changeRequestLifecycleService.RegisterStatusSetByVersionAsync(
                    version.ChangeRequest,
                    userId,
                    previousStatus,
                    "Cambio a linea base por activacion de version.");

                return RedirectToAction(nameof(Index), new { elementId = version.ElementId });
            }
            catch (Exception ex)
            {
                {
                    TempData["Message"] = "Error al establecer la nueva version.";
                    TempData["MessageType"] = "error";
                    _logger.LogError(ex, "Error set new version.");
                    return RedirectToAction(nameof(Index), new { elementId = version.ElementId });
                }
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpVersion(int id)
        {
            var userId = GetCurrentUserId();
            var version = await _scmDocumentContext.Versions
                .Where(x => x.Id == id && x.Element.Project.UserId == userId).FirstOrDefaultAsync();

            if (version is null)
            {
                return NotFound();
            }

            if (version.State == VERSION_STATE_INACTIVE)
            {

                TempData["Message"] = "La version es inactiva.";
                TempData["MessageType"] = "warning";

                return RedirectToAction(nameof(Index), new { elementId = version.ElementId });
            }

            try
            {
                var versionLevelUp = await _scmDocumentContext.Versions
                    .Include(x => x.ParentVersion)
                    .Include(x=>x.ChangeRequest)
                    .FirstOrDefaultAsync(x => x.ParentVersionId == version.Id);

                if (versionLevelUp is null)
                {
                    TempData["Message"] = "No existe una version padre.";
                    TempData["MessageType"] = "warning";
                    return RedirectToAction(nameof(Index), new { elementId = version.ElementId });
                }

                version.State = VERSION_STATE_INACTIVE;

                versionLevelUp.State = VERSION_STATE_ACTIVE;
                var previousStatus = versionLevelUp.ChangeRequest.Status;
                versionLevelUp.ChangeRequest.Status = StatusCR.Baselined;

                await _scmDocumentContext.SaveChangesAsync();
                await _changeRequestLifecycleService.RegisterStatusSetByVersionAsync(
                    versionLevelUp.ChangeRequest,
                    userId,
                    previousStatus,
                    "Cambio a linea base por promocion de version.");

                TempData["Message"] = "Version subida de nivel.";
                TempData["MessageType"] = "success";

                return RedirectToAction(nameof(Index), new { elementId = version.ElementId });

            }catch(Exception ex)
            {
                _logger.LogError(ex, "Erro to up version");
                return RedirectToAction(nameof(Index), new { elementId = version.ElementId });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DownVersion(int id)
        {
            var userId = GetCurrentUserId();

            var version = await _scmDocumentContext.Versions.Include(x => x.ParentVersion)
                .Where(x => x.Id == id && x.Element.Project.UserId == userId).FirstOrDefaultAsync();
            if (version is null)
            {
                return NotFound();
            }

            if (version.State == VERSION_STATE_INACTIVE)
            {

                TempData["Message"] = "La version es inactiva.";
                TempData["MessageType"] = "warning";
                return RedirectToAction(nameof(Index), new { elementId = version.ElementId });
            }

            try
            {
                if (version.ParentVersion is null)
                {
                    TempData["Message"] = "No existe una version padre.";
                    TempData["MessageType"] = "warning";
                    return RedirectToAction(nameof(Index), new { elementId = version.ElementId });
                }

                var versionParent = await _scmDocumentContext.Versions.FindAsync(version.ParentVersionId);
                if (versionParent is null)
                {
                    TempData["Message"] = "No existe una version padre.";
                    TempData["MessageType"] = "warning";
                    return RedirectToAction(nameof(Index), new { elementId = version.ElementId });
                }


                version.State = VERSION_STATE_INACTIVE;

                versionParent.State = VERSION_STATE_ACTIVE;

                await _scmDocumentContext.SaveChangesAsync();

                TempData["Message"] = "Version bajada de nivel.";
                TempData["MessageType"] = "success";

                return RedirectToAction(nameof(Index), new { elementId = version.ElementId });

            }catch(Exception ex)
            {
                _logger.LogError(ex, "Error to down version");
                return RedirectToAction(nameof(Index), new { elementId = version.ElementId });
            }
        }

        private async Task LoadDropDowns(VersionVM vm)
        {
            var userId = GetCurrentUserId();
            var requirementTypes = await _scmDocumentContext.RequirementTypes.OrderBy(r => r.Name).ToListAsync();
            var versions = await _scmDocumentContext.Versions.Where(x => x.ElementId == vm.ElementId).OrderBy(v => v.VersionCode).ToListAsync();
            var changeRequests = await _scmDocumentContext.ChangeRequests.
                Where(x => x.ElementId == vm.ElementId).
                Where(x => x.Action == ActionCR.Approved).
                Where(x => x.Element.Project.UserId == userId).ToListAsync();

            var elementBelongsToUser = await _scmDocumentContext.Elements
                .AnyAsync(x => x.Id == vm.ElementId && x.Project.UserId == userId);
            if (!elementBelongsToUser)
            {
                vm.RequirementTypes = [];
                vm.PreviousVersions = [];
                vm.ChangeRequests = [];
                vm.Phases = [];
                return;
            }

            vm.RequirementTypes = requirementTypes.Select(r => new SelectListItem { Text = r.Name, Value = r.Id.ToString() });
            vm.PreviousVersions = versions.Select(v => new SelectListItem { Text = v.VersionCode + " | " + v.Id, Value = v.Id.ToString() });
            vm.ChangeRequests = changeRequests.Select(x => new SelectListItem { Text = x.Code, Value = x.Id.ToString() });
            vm.Phases = new SelectListItem[] {
                    new SelectListItem { Value="1",Text="Planificación"},
                    new SelectListItem { Value="2",Text="Análisis"},
                    new SelectListItem { Value="3",Text="Diseño"},
                    new SelectListItem { Value="4",Text="Implementación"},
                    new SelectListItem { Value="5",Text="Prueba"},
                    new SelectListItem { Value="6",Text="Mantenimiento"},
                };
        }

        private string GetCurrentUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        }

        private static VersionSnapshotVM ToSnapshot(Models.Version version)
        {
            return new VersionSnapshotVM
            {
                Id = version.Id,
                Name = version.Name,
                VersionCode = version.VersionCode,
                State = version.State,
                ElementUrl = version.ElementUrl,
                ToolUrl = version.ToolUrl,
                Phase = version.Phase,
                Iteration = version.iteration,
                RequirementTypeName = version.RequirementType?.Name ?? "-",
                ChangeRequestCode = version.ChangeRequest?.Code ?? "-",
                UploadDate = version.UploadDate,
                ParentVersionId = version.ParentVersionId
            };
        }

        private static IEnumerable<string> BuildChangeLog(Models.Version source, Models.Version target)
        {
            var changes = new List<string>();
            if (!string.Equals(source.Name, target.Name, StringComparison.Ordinal))
            {
                changes.Add($"Nombre: '{source.Name}' -> '{target.Name}'");
            }
            if (!string.Equals(source.VersionCode, target.VersionCode, StringComparison.Ordinal))
            {
                changes.Add($"Codigo version: '{source.VersionCode}' -> '{target.VersionCode}'");
            }
            if (!string.Equals(source.State, target.State, StringComparison.Ordinal))
            {
                changes.Add($"Estado: '{source.State}' -> '{target.State}'");
            }
            if (source.Phase != target.Phase)
            {
                changes.Add($"Fase: {PhaseHelper.GetPhaseName(source.Phase)} -> {PhaseHelper.GetPhaseName(target.Phase)}");
            }
            if (source.iteration != target.iteration)
            {
                changes.Add($"Iteracion: {source.iteration} -> {target.iteration}");
            }
            if (!string.Equals(source.ElementUrl, target.ElementUrl, StringComparison.Ordinal))
            {
                changes.Add("Cambio de enlace de documento.");
            }
            if (!string.Equals(source.ToolUrl, target.ToolUrl, StringComparison.Ordinal))
            {
                changes.Add("Cambio de enlace de herramienta.");
            }
            if (source.RequirementTypeId != target.RequirementTypeId)
            {
                changes.Add($"Tipo de requerimiento: '{source.RequirementType?.Name ?? "-"}' -> '{target.RequirementType?.Name ?? "-"}'");
            }
            if (source.ChangeRequestId != target.ChangeRequestId)
            {
                changes.Add($"Solicitud de cambio: '{source.ChangeRequest?.Code ?? "-"}' -> '{target.ChangeRequest?.Code ?? "-"}'");
            }
            if (source.ParentVersionId != target.ParentVersionId)
            {
                changes.Add($"Version padre: {source.ParentVersionId?.ToString() ?? "-"} -> {target.ParentVersionId?.ToString() ?? "-"}");
            }

            return changes.Count == 0 ? ["No se detectaron diferencias entre versiones."] : changes;
        }
    }


}
