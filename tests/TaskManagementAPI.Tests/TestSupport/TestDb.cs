using Microsoft.EntityFrameworkCore;
using TaskManagementAPI.Data;
using TaskManagementAPI.Domain;
using TaskManagementAPI.Repositories;
using TaskManagementAPI.Security;

namespace TaskManagementAPI.Tests.TestSupport;

/// <summary>Spins up an isolated EF-InMemory context + real Repository&lt;T&gt; per test.</summary>
public sealed class TestDb : IDisposable
{
    public AppDbContext Context { get; }

    public TestDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tm-tests-{Guid.NewGuid()}")
            .EnableSensitiveDataLogging()
            .Options;
        Context = new AppDbContext(options);
    }

    public IRepository<T> Repo<T>() where T : class => new Repository<T>(Context);
    public IUnitOfWork Uow() => new UnitOfWork(Context);

    public User AddUser(UserRole role = UserRole.Developer, string? email = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email ?? $"{Guid.NewGuid():N}@test.local",
            PasswordHash = "x",
            FirstName = "Test",
            LastName = role.ToString(),
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        Context.Users.Add(user);
        Context.SaveChanges();
        return user;
    }

    public Project AddProject(Guid createdBy, ProjectStatus status = ProjectStatus.Active)
    {
        var p = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Project " + Guid.NewGuid().ToString("N")[..6],
            Status = status,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };
        Context.Projects.Add(p);
        Context.SaveChanges();
        return p;
    }

    public TaskItem AddTask(Guid projectId, Guid createdBy, Guid? assignedTo = null,
        WorkItemStatus status = WorkItemStatus.ToDo, TaskPriority priority = TaskPriority.Medium,
        DateTime? dueDate = null)
    {
        var t = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = "Task " + Guid.NewGuid().ToString("N")[..6],
            Status = status,
            Priority = priority,
            ProjectId = projectId,
            CreatedBy = createdBy,
            AssignedTo = assignedTo,
            CreatedAt = DateTime.UtcNow,
            DueDate = dueDate
        };
        Context.Tasks.Add(t);
        Context.SaveChanges();
        return t;
    }

    public void Dispose() => Context.Dispose();
}

public sealed class FakeCurrentUser : ICurrentUser
{
    public FakeCurrentUser(Guid id, UserRole role, string email = "test@test.local")
    {
        Id = id;
        Role = role;
        Email = email;
    }

    public bool IsAuthenticated => true;
    public Guid Id { get; }
    public string Email { get; }
    public UserRole Role { get; }
    public bool HasPermission(string permission) => RolePermissions.Has(Role, permission);
    public bool IsAdminOrManager => Role is UserRole.Admin or UserRole.Manager;
}
