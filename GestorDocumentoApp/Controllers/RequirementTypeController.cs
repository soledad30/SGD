using GestorDocumentoApp.Data;
using GestorDocumentoApp.Extensions;
using GestorDocumentoApp.Models;
using GestorDocumentoApp.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorDocumentoApp.Controllers
{
    [Authorize(Roles = "Admin")]
    public class RequirementTypeController : Controller
    {
        private readonly ScmDocumentContext _scmDocumentContext;
        private readonly ILogger<ElementTypeController> _logger;

        public RequirementTypeController(ScmDocumentContext scmDocumentContext, ILogger<ElementTypeController> logger)
        {
            _scmDocumentContext = scmDocumentContext;
            _logger = logger;
        }


        public async Task<IActionResult> Index(int pageNumber = 1, int pageSize = 10)
        {

            var requirementTypes = await _scmDocumentContext.RequirementTypes.AsNoTracking().OrderBy(x => x.Name).ToPagedListAsync(pageNumber, pageSize);
            return View(requirementTypes);

        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(RequirementTypeVM requirementTypeVM)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return View(requirementTypeVM);
                }
                var requirementType = new RequirementType() { Name = requirementTypeVM.Name, Description = requirementTypeVM.Description };
                _scmDocumentContext.Add(requirementType);
                await _scmDocumentContext.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving requirement Type {requirementName}", requirementTypeVM.Name);

                return RedirectToAction(nameof(Index));
            }

        }


        public async Task<IActionResult> Edit([FromRoute] int id)
        {
            var requirementType = await _scmDocumentContext.RequirementTypes.FindAsync(id);

            if (requirementType is null)
            {
                return NotFound();
            }

            return View(new RequirementTypeVM { Id = requirementType.Id, Name = requirementType.Name, Description = requirementType.Description });

        }

        [HttpPost]
        public async Task<IActionResult> Edit([FromRoute] int id, RequirementTypeVM requirementTypeVM)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return View(requirementTypeVM);
                }

                var requirementType = await _scmDocumentContext.RequirementTypes.FindAsync(id);

                if (requirementType is null)
                {
                    return NotFound();
                }

                requirementType.Name = requirementTypeVM.Name;
                requirementType.Description = requirementTypeVM.Description;

                await _scmDocumentContext.SaveChangesAsync();

                return RedirectToAction(nameof(Index));

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating requirement Type {requirementName}", requirementTypeVM.Name);
                return RedirectToAction(nameof(Index));
            }

        }

        [HttpPost]
        public async Task<IActionResult> Delete([FromRoute] int id)
        {
            try
            {
                var requirementType = await _scmDocumentContext.RequirementTypes.FindAsync(id);

                if (requirementType is null)
                {
                    return NotFound();
                }
                _scmDocumentContext.RequirementTypes.Remove(requirementType);
                await _scmDocumentContext.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error delete requirement Type {requirementId}", id);
                return RedirectToAction(nameof(Index));
            }
        }

    }
}

