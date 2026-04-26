using GestorDocumentoApp.Data;
using GestorDocumentoApp.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Security.Claims;

namespace GestorDocumentoApp.ViewComponents
{
    public class NotificationBellViewComponent : ViewComponent
    {
        private readonly ScmDocumentContext _context;

        public NotificationBellViewComponent(ScmDocumentContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var userId = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return View(new NotificationBellVM());
            }

            try
            {
                var unreadCount = await _context.Notifications
                    .AsNoTracking()
                    .CountAsync(x => x.UserId == userId && !x.IsRead);

                var latest = await _context.Notifications
                    .AsNoTracking()
                    .Where(x => x.UserId == userId && !x.IsRead)
                    .OrderByDescending(x => x.CreatedAt)
                    .Take(5)
                    .Select(x => new NotificationVM
                    {
                        Id = x.Id,
                        Title = x.Title,
                        Message = x.Message,
                        Link = x.Link,
                        IsRead = x.IsRead,
                        CreatedAt = x.CreatedAt
                    })
                    .ToListAsync();

                return View(new NotificationBellVM
                {
                    UnreadCount = unreadCount,
                    Latest = latest
                });
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                // If migrations are pending and the table does not exist yet,
                // keep the navbar usable instead of failing the whole request.
                return View(new NotificationBellVM());
            }
        }
    }
}
