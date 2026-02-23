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
    public DbSet<SharedResourceGroup> SharedResourceGroups { get; set; }
    public DbSet<SharedResourceGroupItem> SharedResourceGroupItems { get; set; }

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

        builder.Entity<SharedResourceGroup>()
            .HasIndex(sg => sg.ShareToken)
            .IsUnique();

        builder.Entity<SharedResourceGroup>()
            .HasOne(sg => sg.User)
            .WithMany()
            .HasForeignKey(sg => sg.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<SharedResourceGroupItem>()
            .HasKey(sgi => new { sgi.SharedResourceGroupId, sgi.LearningResourceId });

        builder.Entity<SharedResourceGroupItem>()
            .HasOne(sgi => sgi.SharedResourceGroup)
            .WithMany(sg => sg.Items)
            .HasForeignKey(sgi => sgi.SharedResourceGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<SharedResourceGroupItem>()
            .HasOne(sgi => sgi.LearningResource)
            .WithMany()
            .HasForeignKey(sgi => sgi.LearningResourceId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}