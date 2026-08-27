using Microsoft.EntityFrameworkCore;
using TaskManagementAPI.Domain;
using TaskManagementAPI.Security;

namespace TaskManagementAPI.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DataSeeder");

        if (db.Database.IsRelational())
            await db.Database.MigrateAsync(ct);
        else
            await db.Database.EnsureCreatedAsync(ct);

        if (await db.Users.AnyAsync(ct))
        {
            logger.LogInformation("Database already seeded; skipping.");
            return;
        }

        logger.LogInformation("Seeding initial data...");

        var users = new List<User>
        {
            NewUser(hasher, "admin@company.com", "Admin", "User", UserRole.Admin, "Admin@123"),
            NewUser(hasher, "manager@company.com", "John", "Manager", UserRole.Manager, "Manager@123"),
            NewUser(hasher, "dev1@company.com", "Sarah", "Developer", UserRole.Developer, "Dev@123"),
            NewUser(hasher, "dev2@company.com", "Mike", "Developer", UserRole.Developer, "Dev@123"),
            NewUser(hasher, "viewer@company.com", "Lisa", "Viewer", UserRole.Viewer, "Viewer@123"),
        };
        await db.Users.AddRangeAsync(users, ct);

        var admin = users[0];
        var manager = users[1];
        var dev1 = users[2];
        var dev2 = users[3];

        var ecommerce = NewProject("E-Commerce Platform", "Modern e-commerce solution with microservices", admin.Id);
        var banking = NewProject("Mobile Banking App", "Secure mobile banking application", manager.Id);
        var analytics = NewProject("Data Analytics Dashboard", "Real-time business intelligence dashboard", admin.Id);
        analytics.Status = ProjectStatus.Completed;
        analytics.CompletedAt = DateTime.UtcNow.AddDays(-3);

        await db.Projects.AddRangeAsync(new[] { ecommerce, banking, analytics }, ct);

        var tasks = new[]
        {
            NewTask("Setup Database Schema", "Design and implement database structure",
                WorkItemStatus.Done, TaskPriority.Critical, ecommerce.Id, admin.Id, dev1.Id, dueInDays: -10, completed: true),
            NewTask("Implement Authentication", "JWT authentication with refresh tokens",
                WorkItemStatus.InProgress, TaskPriority.High, ecommerce.Id, manager.Id, dev1.Id, dueInDays: 5),
            NewTask("Design UI Components", "Create reusable React components",
                WorkItemStatus.ToDo, TaskPriority.Medium, banking.Id, manager.Id, dev2.Id, dueInDays: 12),
            NewTask("Integration Testing", "Write comprehensive integration tests",
                WorkItemStatus.InReview, TaskPriority.Medium, analytics.Id, admin.Id, dev2.Id, dueInDays: 2),
            NewTask("Payment Gateway Integration", "Integrate Stripe for checkout",
                WorkItemStatus.ToDo, TaskPriority.High, ecommerce.Id, manager.Id, null, dueInDays: -2),
        };
        await db.Tasks.AddRangeAsync(tasks, ct);

        await db.TaskComments.AddRangeAsync(new[]
        {
            new TaskComment
            {
                Id = Guid.NewGuid(), TaskId = tasks[1].Id, UserId = manager.Id,
                Content = "Please prioritise the refresh-token rotation flow.", CreatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new TaskComment
            {
                Id = Guid.NewGuid(), TaskId = tasks[1].Id, UserId = dev1.Id,
                Content = "On it — access token TTL set to 15 minutes.", CreatedAt = DateTime.UtcNow.AddHours(-5)
            },
        }, ct);

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seed complete: {Users} users, {Projects} projects, {Tasks} tasks.",
            users.Count, 3, tasks.Length);
    }

    private static User NewUser(IPasswordHasher hasher, string email, string first, string last, UserRole role, string password)
        => new()
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = hasher.Hash(password),
            FirstName = first,
            LastName = last,
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

    private static Project NewProject(string name, string description, Guid createdBy)
        => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            Status = ProjectStatus.Active,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow.AddDays(-20)
        };

    private static TaskItem NewTask(
        string title, string description, WorkItemStatus status, TaskPriority priority,
        Guid projectId, Guid createdBy, Guid? assignedTo, int dueInDays, bool completed = false)
        => new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description,
            Status = status,
            Priority = priority,
            ProjectId = projectId,
            CreatedBy = createdBy,
            AssignedTo = assignedTo,
            CreatedAt = DateTime.UtcNow.AddDays(-15),
            DueDate = DateTime.UtcNow.Date.AddDays(dueInDays),
            CompletedAt = completed ? DateTime.UtcNow.AddDays(-9) : null,
            EstimatedHours = 16,
            ActualHours = completed ? 18 : 4
        };
}
