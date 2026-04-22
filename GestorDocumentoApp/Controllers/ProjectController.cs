using GestorDocumentoApp.Data;
using GestorDocumentoApp.Models;
using GestorDocumentoApp.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;

namespace GestorDocumentoApp.Controllers
{
    public class ProjectController : Controller
    {
        private readonly ScmDocumentContext _scmDocumentContext;
        private readonly ILogger<ElementTypeController> _logger;

        public ProjectController(ScmDocumentContext scmDocumentContext, ILogger<ElementTypeController> logger)
        {
            _scmDocumentContext = scmDocumentContext;
            _logger = logger;
        }


        public async Task<IActionResult> Index()
        {
            var useId = GetCurrentUserId();
            var projects = await _scmDocumentContext.Projects.Where(x => x.UserId == useId).OrderBy(project => project.Name).ToListAsync();
            return View(projects);

        }

        public IActionResult Create()
        {
            return View(new ProjectVM());
        }

        [HttpPost]
        public async Task<IActionResult> Create(ProjectVM projectVM)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return View(projectVM);
                }
                var project = new Project()
                {
                    Name = projectVM.Name,
                    Description = projectVM.Description,
                    CreationDate = DateTime.SpecifyKind(projectVM.CreationDate, DateTimeKind.Utc),
                    UserId = GetCurrentUserId()
                };
                _scmDocumentContext.Add(project);
                await _scmDocumentContext.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving Project {ProjectName}", projectVM.Name);

                return RedirectToAction(nameof(Index));
            }

        }


        public async Task<IActionResult> Edit([FromRoute] int id)
        {
            var userId = GetCurrentUserId();
            var project = await _scmDocumentContext.Projects.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

            if (project is null)
            {
                return NotFound();
            }

            return View(new ProjectVM
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                CreationDate = project.CreationDate
            });

        }

        [HttpPost]
        public async Task<IActionResult> Edit([FromRoute] int id, ProjectVM projectVM)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return View(projectVM);
                }

                var userId = GetCurrentUserId();
                var project = await _scmDocumentContext.Projects.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

                if (project is null)
                {
                    return NotFound();
                }

                project.Name = projectVM.Name;
                project.Description = projectVM.Description;
                project.CreationDate = DateTime.SpecifyKind(projectVM.CreationDate, DateTimeKind.Utc);

                await _scmDocumentContext.SaveChangesAsync();

                return RedirectToAction(nameof(Index));

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Project {ProjectName}", projectVM.Name);
                return RedirectToAction(nameof(Index));
            }

        }

        [HttpPost]
        public async Task<IActionResult> Delete([FromRoute] int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var project = await _scmDocumentContext.Projects.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

                if (project is null)
                {
                    return NotFound();
                }
                _scmDocumentContext.Projects.Remove(project);
                await _scmDocumentContext.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error delete Project {ProjectId}", id);
                return RedirectToAction(nameof(Index));
            }
        }

        public async Task<IActionResult> Show(int id)
        {
            var userId = GetCurrentUserId();
            var project = await _scmDocumentContext.Projects.AsNoTracking().Include(x => x.Elements.OrderByDescending(x=>x.CreatedDate)).ThenInclude(x=>x.Versions).Where(x => x.Id == id && x.UserId == userId)
                .FirstOrDefaultAsync(x=>x.Id==id);
            if (project is null)
            {
                return NotFound();
            }
            return View(project);
        }

        public async Task<IActionResult> AsignElement(int id)
        {
            var userId = GetCurrentUserId();
            var project = await _scmDocumentContext.Projects.AsNoTracking().Include(x => x.Elements).FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
            if (project is null)
            {
                return NotFound();
            }

            var elementTypes = await _scmDocumentContext.ElementTypes.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
            return View(new ProjectElementVM
            {
                Id = project.Id,
                ProjectName = project.Name,
                ElementTypes = elementTypes.Select(x => new SelectListItem { Value = x.Id.ToString(), Text = x.Name.ToString() })

            });
        }

        [HttpPost]
        public async Task<IActionResult> AsignElement(int id, ProjectElementVM projectElementVM)
        {
            var userId = GetCurrentUserId();
            var project = await _scmDocumentContext.Projects.AsNoTracking().Include(x => x.Elements).FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
            if (project is null)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                var elementTypes = await _scmDocumentContext.ElementTypes.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
                return View(new ProjectElementVM
                {
                    Id = project.Id,
                    ProjectName = project.Name,
                    ElementTypes = elementTypes.Select(x => new SelectListItem { Value = x.Id.ToString(), Text = x.Name.ToString() })

                });
            }

            var element = new Element
            {
                Name = projectElementVM.ElementName,
                Description = projectElementVM.Description,
                CreatedDate = DateTime.SpecifyKind(projectElementVM.CreatedDate, DateTimeKind.Utc),
                ElementTypeId = projectElementVM.ElementTypeId,
                ProjectId = project.Id,
                ExternaCodeElement = projectElementVM.ExternaCodeElement,
                ExternalUrlElement = projectElementVM.ExternalUrlElement,
                
            };

            _scmDocumentContext.Add(element);
            await _scmDocumentContext.SaveChangesAsync();

            return RedirectToAction(nameof(Show), new { id = project.Id });
        }

        private string GetCurrentUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        }
    }
}
