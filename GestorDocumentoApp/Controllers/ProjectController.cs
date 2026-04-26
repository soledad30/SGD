using GestorDocumentoApp.Data;
using GestorDocumentoApp.Models;
using GestorDocumentoApp.Services;
using GestorDocumentoApp.ViewModels;
using Microsoft.AspNetCore.Identity;
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
        private readonly ProjectAccessService _projectAccessService;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<ElementTypeController> _logger;

        public ProjectController(
            ScmDocumentContext scmDocumentContext,
            ProjectAccessService projectAccessService,
            UserManager<IdentityUser> userManager,
            ILogger<ElementTypeController> logger)
        {
            _scmDocumentContext = scmDocumentContext;
            _projectAccessService = projectAccessService;
            _userManager = userManager;
            _logger = logger;
        }


        public async Task<IActionResult> Index()
        {
            var useId = GetCurrentUserId();
            var projects = await _projectAccessService.AccessibleProjectsQuery(useId)
                .AsNoTracking()
                .OrderBy(project => project.Name)
                .ToListAsync();
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

                _scmDocumentContext.ProjectMembers.Add(new ProjectMember
                {
                    ProjectId = project.Id,
                    UserId = project.UserId,
                    Role = ProjectMemberRole.Owner,
                    CanEdit = true,
                    CanApprove = true,
                    JoinedAt = DateTime.UtcNow,
                    Active = true
                });
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
            if (!await _projectAccessService.CanEditProjectAsync(id, userId))
            {
                return Forbid();
            }
            var project = await _scmDocumentContext.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);

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
                if (!await _projectAccessService.CanEditProjectAsync(id, userId))
                {
                    return Forbid();
                }
                var project = await _scmDocumentContext.Projects.FirstOrDefaultAsync(x => x.Id == id);

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
                if (!await _projectAccessService.CanEditProjectAsync(id, userId))
                {
                    return Forbid();
                }
                var project = await _scmDocumentContext.Projects.FirstOrDefaultAsync(x => x.Id == id);

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
            var project = await _projectAccessService.AccessibleProjectsQuery(userId)
                .AsNoTracking().Include(x => x.Elements.OrderByDescending(x=>x.CreatedDate)).ThenInclude(x=>x.Versions)
                .FirstOrDefaultAsync(x=>x.Id==id);
            if (project is null)
            {
                return NotFound();
            }
            return View(project);
        }

        public async Task<IActionResult> Members(int id)
        {
            var userId = GetCurrentUserId();
            if (!await _projectAccessService.CanViewProjectAsync(id, userId))
            {
                return Forbid();
            }

            var project = await _scmDocumentContext.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
            if (project is null)
            {
                return NotFound();
            }

            var members = await _scmDocumentContext.ProjectMembers
                .AsNoTracking()
                .Where(x => x.ProjectId == id)
                .Join(_userManager.Users,
                    member => member.UserId,
                    user => user.Id,
                    (member, user) => new ProjectMemberRowVM
                    {
                        Id = member.Id,
                        UserId = member.UserId,
                        Email = user.Email ?? user.UserName ?? member.UserId,
                        Role = member.Role,
                        CanEdit = member.CanEdit,
                        CanApprove = member.CanApprove,
                        Active = member.Active
                    })
                .OrderBy(x => x.Email)
                .ToListAsync();

            var availableUserEmails = await _userManager.Users
                .AsNoTracking()
                .OrderBy(x => x.Email)
                .Select(x => x.Email ?? x.UserName ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToListAsync();

            return View(new ProjectMembersVM
            {
                ProjectId = project.Id,
                ProjectName = project.Name,
                CurrentUserId = userId,
                Members = members,
                AvailableUserEmails = availableUserEmails
            });
        }

        [HttpPost]
        public async Task<IActionResult> AddMember(ProjectMemberCreateVM vm)
        {
            var userId = GetCurrentUserId();
            if (!await _projectAccessService.CanEditProjectAsync(vm.ProjectId, userId))
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                TempData["Message"] = "Datos de miembro invalidos.";
                TempData["MessageType"] = "warning";
                return RedirectToAction(nameof(Members), new { id = vm.ProjectId });
            }

            var user = await _userManager.FindByEmailAsync(vm.Email.Trim());
            if (user is null)
            {
                TempData["Message"] = "No existe un usuario con ese email.";
                TempData["MessageType"] = "warning";
                return RedirectToAction(nameof(Members), new { id = vm.ProjectId });
            }

            var existing = await _scmDocumentContext.ProjectMembers
                .FirstOrDefaultAsync(x => x.ProjectId == vm.ProjectId && x.UserId == user.Id);
            if (existing is not null)
            {
                existing.Role = vm.Role;
                existing.CanEdit = vm.CanEdit;
                existing.CanApprove = vm.CanApprove;
                existing.Active = true;
            }
            else
            {
                _scmDocumentContext.ProjectMembers.Add(new ProjectMember
                {
                    ProjectId = vm.ProjectId,
                    UserId = user.Id,
                    Role = vm.Role,
                    CanEdit = vm.CanEdit,
                    CanApprove = vm.CanApprove,
                    JoinedAt = DateTime.UtcNow,
                    Active = true
                });
            }

            await _scmDocumentContext.SaveChangesAsync();
            TempData["Message"] = "Miembro guardado correctamente.";
            TempData["MessageType"] = "success";
            return RedirectToAction(nameof(Members), new { id = vm.ProjectId });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateMember(ProjectMemberUpdateVM vm)
        {
            var userId = GetCurrentUserId();
            if (!await _projectAccessService.CanEditProjectAsync(vm.ProjectId, userId))
            {
                return Forbid();
            }

            var member = await _scmDocumentContext.ProjectMembers
                .FirstOrDefaultAsync(x => x.Id == vm.MemberId && x.ProjectId == vm.ProjectId);
            if (member is null)
            {
                return NotFound();
            }

            member.Role = vm.Role;
            member.CanEdit = vm.CanEdit;
            member.CanApprove = vm.CanApprove;
            member.Active = vm.Active;

            if (member.Role == ProjectMemberRole.Owner && !member.Active)
            {
                var ownerCount = await _scmDocumentContext.ProjectMembers
                    .CountAsync(x => x.ProjectId == vm.ProjectId && x.Role == ProjectMemberRole.Owner && x.Active);
                if (ownerCount <= 1)
                {
                    TempData["Message"] = "No se puede desactivar el ultimo owner del proyecto.";
                    TempData["MessageType"] = "warning";
                    return RedirectToAction(nameof(Members), new { id = vm.ProjectId });
                }
            }

            await _scmDocumentContext.SaveChangesAsync();
            TempData["Message"] = "Miembro actualizado correctamente.";
            TempData["MessageType"] = "success";
            return RedirectToAction(nameof(Members), new { id = vm.ProjectId });
        }

        public async Task<IActionResult> AsignElement(int id)
        {
            var userId = GetCurrentUserId();
            if (!await _projectAccessService.CanEditProjectAsync(id, userId))
            {
                return Forbid();
            }
            var project = await _scmDocumentContext.Projects.AsNoTracking().Include(x => x.Elements).FirstOrDefaultAsync(x => x.Id == id);
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
            if (!await _projectAccessService.CanEditProjectAsync(id, userId))
            {
                return Forbid();
            }
            var project = await _scmDocumentContext.Projects.AsNoTracking().Include(x => x.Elements).FirstOrDefaultAsync(x => x.Id == id);
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
