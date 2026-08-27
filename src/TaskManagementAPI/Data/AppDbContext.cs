using Microsoft.EntityFrameworkCore;
using TaskManagementAPI.Domain;

namespace TaskManagementAPI.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).IsRequired().HasMaxLength(256);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.PasswordHash).IsRequired();
            e.Property(x => x.FirstName).IsRequired().HasMaxLength(100);
            e.Property(x => x.LastName).IsRequired().HasMaxLength(100);
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.RefreshToken).HasMaxLength(256);
            e.HasIndex(x => x.RefreshToken);
        });

        b.Entity<Project>(e =>
        {
            e.ToTable("Projects");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(150);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Status);

            e.HasOne(x => x.Creator)
                .WithMany(u => u.CreatedProjects)
                .HasForeignKey(x => x.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<TaskItem>(e =>
        {
            e.ToTable("Tasks");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).IsRequired().HasMaxLength(100);
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Priority).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.EstimatedHours).HasPrecision(6, 2);
            e.Property(x => x.ActualHours).HasPrecision(6, 2);

            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.Priority);
            e.HasIndex(x => x.DueDate);
            e.HasIndex(x => x.ProjectId);
            e.HasIndex(x => x.AssignedTo);
            e.HasIndex(x => x.IsDeleted);

            e.HasOne(x => x.Project)
                .WithMany(p => p.Tasks)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Assignee)
                .WithMany(u => u.AssignedTasks)
                .HasForeignKey(x => x.AssignedTo)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(x => x.Creator)
                .WithMany(u => u.CreatedTasks)
                .HasForeignKey(x => x.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            // Soft-delete filter
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        b.Entity<TaskComment>(e =>
        {
            e.ToTable("TaskComments");
            e.HasKey(x => x.Id);
            e.Property(x => x.Content).IsRequired().HasMaxLength(1000);
            e.HasIndex(x => x.TaskId);

            e.HasOne(x => x.Task)
                .WithMany(t => t.Comments)
                .HasForeignKey(x => x.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.User)
                .WithMany(u => u.Comments)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Mirror the TaskItem soft-delete filter so comments of deleted tasks are hidden too.
            e.HasQueryFilter(c => !c.Task!.IsDeleted);
        });
    }
}
