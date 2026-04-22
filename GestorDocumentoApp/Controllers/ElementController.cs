using GestorDocumentoApp.Data;
using GestorDocumentoApp.Extensions;
using GestorDocumentoApp.Models;
using GestorDocumentoApp.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;

namespace GestorDocumentoApp.Controllers
{
    public class ElementController : Controller
    {
        private ScmDocumentContext _scmDocumentContext;
        private ILogger<ElementController> _logger;

        public ElementController(ScmDocumentContext scmDocumentContext, ILogger<ElementController> logger)
        {
            _scmDocumentContext = scmDocumentContext;
            _logger = logger;
        }

        public async Task<IActionResult> Index(int? projectId, int pageNumber = 1, int pageSize = 10)
        {
            var userId = GetCurrentUserId();
            Project? project = null;

            if (projectId.HasValue)
            {
                project = await _scmDocumentContext.Projects.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == projectId && x.UserId == userId);

                if (project is null)
                {
                    return NotFound();
                }
            }

            var projects = await _scmDocumentContext.Projects.AsNoTracking()
                .Where(x => x.UserId == userId)
                .ToListAsync();

            IQueryable<Element> query = _scmDocumentContext.Elements
                .Where(x => x.Project.UserId == userId);

            if (projectId.HasValue)
            {
                query=query.Where(x => x.ProjectId == projectId);
            }

            var page = await query
                .Include(x => x.Project).Include(x => x.ElementType)
                .OrderByDescending(element => element.CreatedDate).ToPagedListAsync(pageNumber, pageSize);

            return View(
                new ElementIndexVM
                {
                    Projects=projects.Select(x =>new SelectListItem { Value=x.Id.ToString(),Text=x.Name.ToString()}),
                    HasNext=page.HasNext,
                    HasPrevious=page.HasPrevious,
                    Items=page.Items,
                    TotalCount=page.TotalCount,
                    PageNumber=pageNumber,
                    PageSize=pageSize,
                    ProjectId=projectId,
                    ProjectName=project?.Name
                }
                );
        }

        public async Task<IActionResult> Create()
        {
            var userId = GetCurrentUserId();

            var elementTypes = await _scmDocumentContext.ElementTypes.AsNoTracking().OrderBy(elementType => elementType.Name).ToListAsync();
            var projects = await _scmDocumentContext.Projects.AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderBy(x => x.Name).ToListAsync();


            return View(
                new ElementVM
                {
                    ElementTypes = elementTypes.Select(elementType => new SelectListItem { Text = elementType.Name, Value = elementType.Id.ToString() }),
                    Projects = projects.Select(x => new SelectListItem { Text = x.Name, Value = x.Id.ToString() }),
                }
            );
        }

        [HttpPost]
        public async Task<IActionResult> Create(ElementVM elementVM)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!ModelState.IsValid)
                {

                    var elementTypes = await _scmDocumentContext.ElementTypes.AsNoTracking().OrderBy(elementType => elementType.Name).ToListAsync();
                    var projects = await _scmDocumentContext.Projects.AsNoTracking()
                        .Where(x => x.UserId == userId)
                        .OrderBy(x => x.Name).ToListAsync();

                    elementVM.ElementTypes = elementTypes.Select(elementType => new SelectListItem { Text = elementType.Name, Value = elementType.Id.ToString() });
                    elementVM.Projects = projects.Select(x => new SelectListItem { Text = x.Name, Value = x.Id.ToString() });

                    return View(elementVM);
                }
                if (!elementVM.ProjectId.HasValue)
                {
                    ModelState.AddModelError(nameof(elementVM.ProjectId), "Proyecto es requerido.");
                    var elementTypes = await _scmDocumentContext.ElementTypes.AsNoTracking().OrderBy(elementType => elementType.Name).ToListAsync();
                    var projects = await _scmDocumentContext.Projects.AsNoTracking()
                        .Where(x => x.UserId == userId)
                        .OrderBy(x => x.Name).ToListAsync();
                    elementVM.ElementTypes = elementTypes.Select(elementType => new SelectListItem { Text = elementType.Name, Value = elementType.Id.ToString() });
                    elementVM.Projects = projects.Select(x => new SelectListItem { Text = x.Name, Value = x.Id.ToString() });
                    return View(elementVM);
                }

                var selectedProject = await _scmDocumentContext.Projects.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == elementVM.ProjectId && x.UserId == userId);
                if (selectedProject is null)
                {
                    return Forbid();
                }

                var element = new Element
                {
                    Name = elementVM.Name,
                    Description = elementVM.Description,
                    CreatedDate = DateTime.SpecifyKind(elementVM.CreatedDate, DateTimeKind.Utc),

                    ElementTypeId = elementVM.ElementTypeId,
                    ProjectId = elementVM.ProjectId ?? 0,
                };

                _scmDocumentContext.Add(element);
                await _scmDocumentContext.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving Element Type {ElementName}", elementVM.Name);

                return RedirectToAction(nameof(Index));
            }

        }


        public async Task<IActionResult> Edit([FromRoute] int id)
        {
            var userId = GetCurrentUserId();
            var element = await _scmDocumentContext.Elements
                .FirstOrDefaultAsync(x => x.Id == id && x.Project.UserId == userId);

            if (element is null)
            {
                return NotFound();
            }

            var elementTypes = await _scmDocumentContext.ElementTypes.AsNoTracking().OrderBy(element => element.Name).ToListAsync();

            var projects = await _scmDocumentContext.Projects.AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderBy(x => x.Name).ToListAsync();

            return View(
                new ElementVM
                {
                    Id = element.Id,
                    Name = element.Name,
                    Description = element.Description,
                    CreatedDate = element.CreatedDate,
                    ElementTypeId = element.ElementTypeId,

                    ProjectId = element.ProjectId,
                    ElementTypes = elementTypes.Select(elementType => new SelectListItem { Text = elementType.Name, Value = elementType.Id.ToString() }),
                    Projects = projects.Select(x => new SelectListItem { Text = x.Name, Value = x.Id.ToString() }),
                }

                );

        }

        [HttpPost]
        public async Task<IActionResult> Edit([FromRoute] int id, ElementVM elementVM)
        {
            try
            {
                var userId = GetCurrentUserId();

                var element = await _scmDocumentContext.Elements
                    .FirstOrDefaultAsync(x => x.Id == id && x.Project.UserId == userId);

                if (element is null)
                {
                    return NotFound();
                }

                if (!ModelState.IsValid)
                {
                    var elementTypes = await _scmDocumentContext.ElementTypes.OrderBy(elementType => elementType.Name).ToListAsync();
                    var projects = await _scmDocumentContext.Projects.AsNoTracking()
                        .Where(x => x.UserId == userId)
                        .OrderBy(x => x.Name).ToListAsync();

                    elementVM.ElementTypes = elementTypes.Select(elementType => new SelectListItem { Text = elementType.Name, Value = elementType.Id.ToString() });
                    elementVM.Projects = projects.Select(x => new SelectListItem { Text = x.Name, Value = x.Id.ToString() });

                    return View(elementVM);
                }
                if (!elementVM.ProjectId.HasValue)
                {
                    ModelState.AddModelError(nameof(elementVM.ProjectId), "Proyecto es requerido.");
                    var elementTypes = await _scmDocumentContext.ElementTypes.OrderBy(elementType => elementType.Name).ToListAsync();
                    var projects = await _scmDocumentContext.Projects.AsNoTracking()
                        .Where(x => x.UserId == userId)
                        .OrderBy(x => x.Name).ToListAsync();
                    elementVM.ElementTypes = elementTypes.Select(elementType => new SelectListItem { Text = elementType.Name, Value = elementType.Id.ToString() });
                    elementVM.Projects = projects.Select(x => new SelectListItem { Text = x.Name, Value = x.Id.ToString() });
                    return View(elementVM);
                }

                var selectedProject = await _scmDocumentContext.Projects.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == elementVM.ProjectId && x.UserId == userId);
                if (selectedProject is null)
                {
                    return Forbid();
                }

                element.Name = elementVM.Name;
                element.Description = elementVM.Description;
                element.CreatedDate = DateTime.SpecifyKind(elementVM.CreatedDate, DateTimeKind.Utc);
                element.ElementTypeId = elementVM.ElementTypeId;

                element.ProjectId = elementVM.ProjectId ?? 0;

                await _scmDocumentContext.SaveChangesAsync();

                return RedirectToAction(nameof(Index));

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Element {ElementName}", elementVM.Name);
                return RedirectToAction(nameof(Index));
            }

        }

        [HttpPost]
        public async Task<IActionResult> Delete([FromRoute] int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var element = await _scmDocumentContext.Elements
                    .FirstOrDefaultAsync(x => x.Id == id && x.Project.UserId == userId);

                if (element is null)
                {
                    return NotFound();
                }
                _scmDocumentContext.Elements.Remove(element);
                await _scmDocumentContext.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error delete Element {ElementId}", id);
                return RedirectToAction(nameof(Index));
            }
        }

        private string GetCurrentUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        }
    }
}
