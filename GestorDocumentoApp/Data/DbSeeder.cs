using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using GestorDocumentoApp.Models;

namespace GestorDocumentoApp.Data
{
    public class DbSeeder
    {
        public static async Task SeedAsync(
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ScmDocumentContext context,
            IConfiguration configuration)
        {
            var adminEmail = configuration["AdminUser:Email"];
            var adminPassword = configuration["AdminUser:Password"];

            if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
            {
                return;
            }

            var roles = new[] {"Admin", "User", "Approver"};
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            var adminDefaul=await userManager.FindByEmailAsync(adminEmail);

            if (adminDefaul == null) {
                var admin = new IdentityUser
                {
                    UserName = adminEmail,
                    Email = adminEmail
                };
                var result = await userManager.CreateAsync(admin, adminPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, "Admin");
                }
            }

            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser is null)
            {
                return;
            }

            await SeedCatalogsAsync(context);
            await SeedSampleProjectDataAsync(context, adminUser.Id);
            await EnsureProjectOwnersAsMembersAsync(context);
            await SeedSampleNotificationsAsync(context, adminUser.Id);
        }

        private static async Task SeedCatalogsAsync(ScmDocumentContext context)
        {
            if (!await context.ElementTypes.AnyAsync())
            {
                context.ElementTypes.AddRange(
                    new ElementType { Name = "Documento", Description = "Especificaciones funcionales y tecnicas." },
                    new ElementType { Name = "Codigo fuente", Description = "Componentes y modulos de aplicacion." },
                    new ElementType { Name = "Base de datos", Description = "Scripts, migraciones y estructura de datos." },
                    new ElementType { Name = "Servicio externo", Description = "Integraciones con APIs de terceros." }
                );
            }

            if (!await context.RequirementTypes.AnyAsync())
            {
                context.RequirementTypes.AddRange(
                    new RequirementType { Name = "Funcional", Description = "Comportamiento esperado por el usuario." },
                    new RequirementType { Name = "No funcional", Description = "Rendimiento, seguridad y disponibilidad." },
                    new RequirementType { Name = "Regulatorio", Description = "Cumplimiento legal y normativo." }
                );
            }

            await context.SaveChangesAsync();
        }

        private static async Task SeedSampleProjectDataAsync(ScmDocumentContext context, string adminUserId)
        {
            var hasAdminProjects = await context.Projects.AnyAsync(x => x.UserId == adminUserId);
            if (hasAdminProjects)
            {
                return;
            }

            var today = DateTime.UtcNow;
            var functionalReqType = await context.RequirementTypes.AsNoTracking().FirstAsync();
            var elementTypes = await context.ElementTypes.AsNoTracking().ToListAsync();

            var project = new Project
            {
                Name = "Plataforma de Gestion Academica",
                Description = "Sistema para control de matriculas, notas y reportes.",
                CreationDate = today.AddDays(-45),
                UserId = adminUserId
            };
            context.Projects.Add(project);
            await context.SaveChangesAsync();

            var elementFrontend = new Element
            {
                Name = "Modulo Web de Matricula",
                Description = "Interfaz web para registro de estudiantes.",
                CreatedDate = today.AddDays(-40),
                ProjectId = project.Id,
                ElementTypeId = elementTypes.First().Id,
                ExternalUrlElement = "https://intranet.universidad.edu/matricula",
                ExternaCodeElement = "CI-WEB-MAT-01"
            };
            var elementApi = new Element
            {
                Name = "API de Notas",
                Description = "Servicio backend para consulta y registro de notas.",
                CreatedDate = today.AddDays(-38),
                ProjectId = project.Id,
                ElementTypeId = elementTypes.Skip(1).First().Id,
                ExternalUrlElement = "https://api.universidad.edu/notas",
                ExternaCodeElement = "CI-API-NOT-02"
            };

            context.Elements.AddRange(elementFrontend, elementApi);
            await context.SaveChangesAsync();

            var cr1 = new ChangeRequest
            {
                ElementId = elementFrontend.Id,
                ClasificationType = ClasificationTypeCR.Enhancement,
                Description = "Agregar validacion de cupos en tiempo real durante la matricula.",
                Priority = PriorityCR.Urgent,
                Status = StatusCR.Reviewed,
                Remarks = "Aprobado por coordinacion academica.",
                Code = "CR-2026-001",
                Action = ActionCR.Approved,
                ApprovalStatus = ApprovalStatus.Approved,
                ApprovalRequestedAt = today.AddDays(-20),
                ApprovalDecidedAt = today.AddDays(-18),
                CreatedAt = today.AddDays(-24)
            };
            var cr2 = new ChangeRequest
            {
                ElementId = elementApi.Id,
                ClasificationType = ClasificationTypeCR.BugFixing,
                Description = "Corregir inconsistencia en promedio final cuando existe recuperacion.",
                Priority = PriorityCR.Immediate,
                Status = StatusCR.Checkin,
                Remarks = "Validado en ambiente QA.",
                Code = "CR-2026-002",
                Action = ActionCR.Approved,
                ApprovalStatus = ApprovalStatus.Approved,
                ApprovalRequestedAt = today.AddDays(-12),
                ApprovalDecidedAt = today.AddDays(-11),
                CreatedAt = today.AddDays(-14)
            };
            var cr3 = new ChangeRequest
            {
                ElementId = elementApi.Id,
                ClasificationType = ClasificationTypeCR.Other,
                Description = "Preparar endpoint para exportacion de historial academico.",
                Priority = PriorityCR.AsSoonAsPossible,
                Status = StatusCR.Analyzed,
                Remarks = "Pendiente asignacion de sprint.",
                Code = "CR-2026-003",
                Action = ActionCR.InWait,
                ApprovalStatus = ApprovalStatus.Pending,
                ApprovalRequestedAt = today.AddDays(-4),
                CreatedAt = today.AddDays(-5)
            };
            context.ChangeRequests.AddRange(cr1, cr2, cr3);
            await context.SaveChangesAsync();

            var version1 = new GestorDocumentoApp.Models.Version
            {
                Name = "Validacion de cupos en matricula",
                ElementUrl = "https://repo.universidad.edu/matricula/releases/v1.2.0",
                UploadDate = today.AddDays(-16),
                State = "active",
                ToolUrl = "https://jira.universidad.edu/browse/CR-2026-001",
                VersionCode = "v1.2.0",
                Phase = 2,
                iteration = 3,
                ChangeRequestId = cr1.Id,
                ElementId = elementFrontend.Id,
                UserId = adminUserId,
                RequirementTypeId = functionalReqType.Id
            };
            var version2 = new GestorDocumentoApp.Models.Version
            {
                Name = "Fix promedio con recuperacion",
                ElementUrl = "https://repo.universidad.edu/notas/releases/v2.4.3",
                UploadDate = today.AddDays(-9),
                State = "active",
                ToolUrl = "https://jira.universidad.edu/browse/CR-2026-002",
                VersionCode = "v2.4.3",
                Phase = 3,
                iteration = 2,
                ChangeRequestId = cr2.Id,
                ElementId = elementApi.Id,
                UserId = adminUserId,
                RequirementTypeId = functionalReqType.Id
            };

            context.Versions.AddRange(version1, version2);
            context.ChangeRequestAudits.AddRange(
                new ChangeRequestAudit
                {
                    ChangeRequestId = cr1.Id,
                    ChangedAt = today.AddDays(-24),
                    ChangedByUserId = adminUserId,
                    EventType = "Creado",
                    Summary = "CR registrada y enviada para evaluacion."
                },
                new ChangeRequestAudit
                {
                    ChangeRequestId = cr2.Id,
                    ChangedAt = today.AddDays(-14),
                    ChangedByUserId = adminUserId,
                    EventType = "Creado",
                    Summary = "CR registrada por incidente en produccion."
                });

            await context.SaveChangesAsync();
        }

        private static async Task SeedSampleNotificationsAsync(ScmDocumentContext context, string adminUserId)
        {
            var hasNotifications = await context.Notifications.AnyAsync(x => x.UserId == adminUserId);
            if (hasNotifications)
            {
                return;
            }

            var now = DateTime.UtcNow;
            context.Notifications.AddRange(
                new Notification
                {
                    UserId = adminUserId,
                    Title = "CR aprobada",
                    Message = "La solicitud CR-2026-001 fue aprobada para implementacion.",
                    Link = "/ChangeRequest/Index",
                    IsRead = false,
                    CreatedAt = now.AddHours(-8)
                },
                new Notification
                {
                    UserId = adminUserId,
                    Title = "CR en revision",
                    Message = "La solicitud CR-2026-003 requiere aprobacion del comite.",
                    Link = "/ChangeRequest/Index",
                    IsRead = true,
                    CreatedAt = now.AddHours(-20)
                });

            await context.SaveChangesAsync();
        }

        private static async Task EnsureProjectOwnersAsMembersAsync(ScmDocumentContext context)
        {
            var projects = await context.Projects
                .AsNoTracking()
                .Select(x => new { x.Id, x.UserId })
                .ToListAsync();

            if (projects.Count == 0)
            {
                return;
            }

            var existingMemberships = await context.ProjectMembers
                .AsNoTracking()
                .Select(x => new { x.ProjectId, x.UserId })
                .ToListAsync();

            var existingLookup = new HashSet<string>(
                existingMemberships.Select(x => $"{x.ProjectId}|{x.UserId}"));

            var missingOwners = projects
                .Where(x => !string.IsNullOrWhiteSpace(x.UserId))
                .Where(x => !existingLookup.Contains($"{x.Id}|{x.UserId}"))
                .Select(x => new ProjectMember
                {
                    ProjectId = x.Id,
                    UserId = x.UserId,
                    Role = ProjectMemberRole.Owner,
                    CanEdit = true,
                    CanApprove = true,
                    JoinedAt = DateTime.UtcNow,
                    Active = true
                })
                .ToList();

            if (missingOwners.Count == 0)
            {
                return;
            }

            context.ProjectMembers.AddRange(missingOwners);
            await context.SaveChangesAsync();
        }
    }
}
