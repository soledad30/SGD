using GestorDocumentoApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GestorDocumentoApp.Data
{
    public class ScmDocumentContext : IdentityDbContext<IdentityUser>
    {
        public ScmDocumentContext(DbContextOptions<ScmDocumentContext> options) : base(options)
        {
        }

        public ScmDocumentContext()
        {

        }


        public DbSet<RequirementType> RequirementTypes { get; set; }
        public DbSet<Project> Projects { get;set; }
        public DbSet<ElementType> ElementTypes { get; set; }
        public DbSet<Element> Elements { get; set; }

        public DbSet<GestorDocumentoApp.Models.Version> Versions { get; set; }

        public DbSet<ChangeRequest> ChangeRequests { get; set; }
        public DbSet<ChangeRequestAudit> ChangeRequestAudits { get; set; }
        public DbSet<GitTraceLink> GitTraceLinks { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<ProjectMember> ProjectMembers { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Notification>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ProjectMember>()
                .HasIndex(x => new { x.ProjectId, x.UserId })
                .IsUnique();

            builder.Entity<ProjectMember>()
                .HasOne(x => x.Project)
                .WithMany(x => x.Members)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ProjectMember>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
