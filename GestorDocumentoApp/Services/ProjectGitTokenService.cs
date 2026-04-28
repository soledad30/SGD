using GestorDocumentoApp.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace GestorDocumentoApp.Services
{
    public class ProjectGitTokenService
    {
        private readonly IDataProtector _protector;
        private readonly ScmDocumentContext _context;

        public ProjectGitTokenService(IDataProtectionProvider dataProtectionProvider, ScmDocumentContext context)
        {
            _protector = dataProtectionProvider.CreateProtector("GestorDocumentoApp.ProjectGitToken.v1");
            _context = context;
        }

        public string? Protect(string? plainToken)
        {
            if (string.IsNullOrWhiteSpace(plainToken))
            {
                return null;
            }

            return _protector.Protect(plainToken.Trim());
        }

        public string? Unprotect(string? cipherToken)
        {
            if (string.IsNullOrWhiteSpace(cipherToken))
            {
                return null;
            }

            try
            {
                return _protector.Unprotect(cipherToken);
            }
            catch
            {
                return null;
            }
        }

        public async Task<string?> GetTokenByChangeRequestIdAsync(int changeRequestId)
        {
            var cipherToken = await _context.ChangeRequests
                .Where(x => x.Id == changeRequestId)
                .Select(x => x.Element.Project.GitHubTokenCipherText)
                .FirstOrDefaultAsync();
            return Unprotect(cipherToken);
        }
    }
}
