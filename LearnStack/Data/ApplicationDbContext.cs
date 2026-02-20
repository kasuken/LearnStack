using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using LearnStack.Data.Models;

namespace LearnStack.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<LearningResource> LearningResources { get; set; }
    public DbSet<ContentIdea> ContentIdeas { get; set; }
    public DbSet<ContentIdeaResource> ContentIdeaResources { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ContentIdeaResource>()
            .HasKey(cir => new { cir.ContentIdeaId, cir.LearningResourceId });

        builder.Entity<ContentIdeaResource>()
            .HasOne(cir => cir.ContentIdea)
            .WithMany(ci => ci.SourceResources)
            .HasForeignKey(cir => cir.ContentIdeaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ContentIdeaResource>()
            .HasOne(cir => cir.LearningResource)
            .WithMany()
            .HasForeignKey(cir => cir.LearningResourceId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<LearningResource>()
            .HasOne(lr => lr.User)
            .WithMany()
            .HasForeignKey(lr => lr.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ContentIdea>()
            .HasOne(ci => ci.User)
            .WithMany()
            .HasForeignKey(ci => ci.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}