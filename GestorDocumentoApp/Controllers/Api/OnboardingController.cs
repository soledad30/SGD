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
            var projectCount = await _context.Projects.CountAsync(x => x.UserId == userId || x.Members.Any(m => m.UserId == userId && m.Active));
            var elementCount = await _context.Elements.CountAsync(x => x.Project.UserId == userId || x.Project.Members.Any(m => m.UserId == userId && m.Active));
            var changeRequestCount = await _context.ChangeRequests.CountAsync(x => x.Element.Project.UserId == userId || x.Element.Project.Members.Any(m => m.UserId == userId && m.Active));
            var versionCount = await _context.Versions.CountAsync(x => x.Element.Project.UserId == userId || x.Element.Project.Members.Any(m => m.UserId == userId && m.Active));
            var gitTraceCount = await _context.GitTraceLinks.CountAsync(x => x.ChangeRequest.Element.Project.UserId == userId || x.ChangeRequest.Element.Project.Members.Any(m => m.UserId == userId && m.Active));

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
