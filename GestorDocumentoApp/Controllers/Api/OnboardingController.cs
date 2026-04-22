using GestorDocumentoApp.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GestorDocumentoApp.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OnboardingController : ControllerBase
    {
        private readonly ScmDocumentContext _context;

        public OnboardingController(ScmDocumentContext context)
        {
            _context = context;
        }

        [HttpGet("progress")]
        public async Task<IActionResult> Progress()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var projectCount = await _context.Projects.CountAsync(x => x.UserId == userId);
            var elementCount = await _context.Elements.CountAsync(x => x.Project.UserId == userId);
            var changeRequestCount = await _context.ChangeRequests.CountAsync(x => x.Element.Project.UserId == userId);
            var versionCount = await _context.Versions.CountAsync(x => x.Element.Project.UserId == userId);
            var gitTraceCount = await _context.GitTraceLinks.CountAsync(x => x.ChangeRequest.Element.Project.UserId == userId);

            return Ok(new
            {
                projectCount,
                elementCount,
                changeRequestCount,
                versionCount,
                gitTraceCount
            });
        }
    }
}
