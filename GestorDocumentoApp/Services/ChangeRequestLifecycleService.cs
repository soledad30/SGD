using GestorDocumentoApp.Data;
using GestorDocumentoApp.Models;
using GestorDocumentoApp.Utils;

namespace GestorDocumentoApp.Services
{
    public class ChangeRequestLifecycleService
    {
        private readonly ScmDocumentContext _context;

        public ChangeRequestLifecycleService(ScmDocumentContext context)
        {
            _context = context;
        }

        public async Task RegisterCreatedAsync(ChangeRequest changeRequest, string userId, string summary)
        {
            AddAudit(changeRequest.Id, userId, "Creado", summary);
            AddStatusHistory(changeRequest.Id, userId, null, changeRequest.Status, "Estado inicial de la CR.");
            AddCriticalNotifications(changeRequest, userId, false, statusChanged: true, actionChanged: false);
            await _context.SaveChangesAsync();
        }

        public async Task RegisterUpdatedAsync(
            ChangeRequest changeRequest,
            string userId,
            string summary,
            StatusCR previousStatus,
            ActionCR? previousAction)
        {
            AddAudit(changeRequest.Id, userId, "Actualizado", summary);

            var statusChanged = previousStatus != changeRequest.Status;
            var actionChanged = previousAction != changeRequest.Action;

            if (statusChanged)
            {
                AddStatusHistory(
                    changeRequest.Id,
                    userId,
                    previousStatus,
                    changeRequest.Status,
                    $"Cambio de proceso: {EnumHelper.GetDisplayName(previousStatus)} -> {EnumHelper.GetDisplayName(changeRequest.Status)}.");
            }

            AddCriticalNotifications(changeRequest, userId, true, statusChanged, actionChanged);
            await _context.SaveChangesAsync();
        }

        public async Task RegisterStatusSetByVersionAsync(ChangeRequest changeRequest, string userId, StatusCR previousStatus, string reason)
        {
            if (previousStatus == changeRequest.Status)
            {
                return;
            }

            AddAudit(
                changeRequest.Id,
                userId,
                "Proceso actualizado por version",
                $"Cambio de proceso por version: {EnumHelper.GetDisplayName(previousStatus)} -> {EnumHelper.GetDisplayName(changeRequest.Status)}. Motivo: {reason}");

            AddStatusHistory(
                changeRequest.Id,
                userId,
                previousStatus,
                changeRequest.Status,
                reason);

            AddCriticalNotifications(changeRequest, userId, true, statusChanged: true, actionChanged: false);
            await _context.SaveChangesAsync();
        }

        private void AddAudit(int changeRequestId, string userId, string eventType, string summary)
        {
            _context.ChangeRequestAudits.Add(new ChangeRequestAudit
            {
                ChangeRequestId = changeRequestId,
                ChangedAt = DateTime.UtcNow,
                ChangedByUserId = userId,
                EventType = eventType,
                Summary = summary
            });
        }

        private void AddStatusHistory(int changeRequestId, string userId, StatusCR? fromStatus, StatusCR toStatus, string reason)
        {
            var fromLabel = fromStatus.HasValue ? EnumHelper.GetDisplayName(fromStatus.Value) : "N/A";
            var toLabel = EnumHelper.GetDisplayName(toStatus);

            _context.ChangeRequestAudits.Add(new ChangeRequestAudit
            {
                ChangeRequestId = changeRequestId,
                ChangedAt = DateTime.UtcNow,
                ChangedByUserId = userId,
                EventType = "Historial de proceso",
                Summary = $"{fromLabel} -> {toLabel}. {reason}"
            });
        }

        private void AddCriticalNotifications(ChangeRequest changeRequest, string userId, bool isUpdate, bool statusChanged, bool actionChanged)
        {
            var events = new List<(string title, string message)>();
            var action = changeRequest.Action ?? ActionCR.InWait;

            if (statusChanged)
            {
                events.Add((
                    "Proceso CR actualizado",
                    $"La CR {changeRequest.Code} ahora esta en {EnumHelper.GetDisplayName(changeRequest.Status)}."
                ));
            }

            if (changeRequest.Status == StatusCR.Baselined)
            {
                events.Add((
                    "CR en linea base",
                    $"La CR {changeRequest.Code} alcanzo estado Linea base."
                ));
            }

            if (actionChanged && action == ActionCR.Approved)
            {
                events.Add((
                    "CR aprobada",
                    $"La CR {changeRequest.Code} fue marcada como Aprobada."
                ));
            }
            else if (actionChanged && action == ActionCR.Rejected)
            {
                events.Add((
                    "CR rechazada",
                    $"La CR {changeRequest.Code} fue marcada como Rechazada."
                ));
            }

            foreach (var evt in events)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = userId,
                    Title = isUpdate ? $"{evt.title} (actualizacion)" : evt.title,
                    Message = evt.message,
                    Link = $"/ChangeRequest/Details/{changeRequest.Id}",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
    }
}
