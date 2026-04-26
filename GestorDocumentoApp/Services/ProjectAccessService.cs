using GestorDocumentoApp.Data;
using GestorDocumentoApp.Models;
using Microsoft.EntityFrameworkCore;

namespace GestorDocumentoApp.Services
{
    public class ProjectAccessService
    {
        private readonly ScmDocumentContext _context;

        public ProjectAccessService(ScmDocumentContext context)
        {
            _context = context;
        }

        public IQueryable<Project> AccessibleProjectsQuery(string userId)
        {
            return _context.Projects.Where(x =>
                x.UserId == userId ||
                x.Members.Any(m => m.UserId == userId && m.Active));
        }

        public IQueryable<int> AccessibleProjectIdsQuery(string userId)
        {
            return AccessibleProjectsQuery(userId).Select(x => x.Id);
        }

        public async Task<bool> CanViewProjectAsync(int projectId, string userId)
        {
            return await AccessibleProjectsQuery(userId).AnyAsync(x => x.Id == projectId);
        }

        public async Task<bool> CanEditProjectAsync(int projectId, string userId)
        {
            return await _context.Projects.AnyAsync(x =>
                x.Id == projectId &&
                (
                    x.UserId == userId ||
                    x.Members.Any(m =>
                        m.UserId == userId &&
                        m.Active &&
                        (m.CanEdit || m.Role == ProjectMemberRole.Owner || m.Role == ProjectMemberRole.Maintainer || m.Role == ProjectMemberRole.Developer))
                ));
        }

        public async Task<bool> CanApproveChangeRequestAsync(int changeRequestId, string userId)
        {
            return await _context.ChangeRequests
                .Include(x => x.Element)
                .AnyAsync(x =>
                    x.Id == changeRequestId &&
                    (
                        x.Element.Project.UserId == userId ||
                        x.Element.Project.Members.Any(m =>
                            m.UserId == userId &&
                            m.Active &&
                            (m.CanApprove || m.Role == ProjectMemberRole.Owner || m.Role == ProjectMemberRole.Maintainer || m.Role == ProjectMemberRole.Reviewer))
                    ));
        }
    }
}
