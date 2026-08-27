using Microsoft.EntityFrameworkCore;
using TaskManagementAPI.Common;
using TaskManagementAPI.Contracts;
using TaskManagementAPI.Domain;
using TaskManagementAPI.Repositories;
using TaskManagementAPI.Security;

namespace TaskManagementAPI.Services;

public interface ICommentService
{
    Task<IReadOnlyList<CommentDto>> GetForTaskAsync(Guid taskId, CancellationToken ct = default);
    Task<CommentDto> AddAsync(Guid taskId, CreateCommentRequest request, CancellationToken ct = default);
    Task<CommentDto> UpdateAsync(Guid commentId, UpdateCommentRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid commentId, CancellationToken ct = default);
}

public class CommentService : ICommentService
{
    private readonly IRepository<TaskComment> _comments;
    private readonly IRepository<TaskItem> _tasks;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public CommentService(
        IRepository<TaskComment> comments,
        IRepository<TaskItem> tasks,
        IUnitOfWork uow,
        ICurrentUser currentUser)
    {
        _comments = comments;
        _tasks = tasks;
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<CommentDto>> GetForTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        await EnsureTaskExistsAsync(taskId, ct);

        var comments = await _comments.Query()
            .Include(c => c.User)
            .Where(c => c.TaskId == taskId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

        return comments.Select(c => c.ToDto()).ToList();
    }

    public async Task<CommentDto> AddAsync(Guid taskId, CreateCommentRequest request, CancellationToken ct = default)
    {
        await EnsureTaskExistsAsync(taskId, ct);

        var comment = new TaskComment
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            UserId = _currentUser.Id,
            Content = request.Content.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        await _comments.AddAsync(comment, ct);
        await _uow.SaveChangesAsync(ct);

        return (await _comments.Query().Include(c => c.User).FirstAsync(c => c.Id == comment.Id, ct)).ToDto();
    }

    public async Task<CommentDto> UpdateAsync(Guid commentId, UpdateCommentRequest request, CancellationToken ct = default)
    {
        var comment = await _comments.Query(tracking: true)
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == commentId, ct)
            ?? throw new NotFoundException("Comment", commentId);

        EnsureOwnerOrManager(comment);

        comment.Content = request.Content.Trim();
        comment.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);
        return comment.ToDto();
    }

    public async Task DeleteAsync(Guid commentId, CancellationToken ct = default)
    {
        var comment = await _comments.Query(tracking: true).FirstOrDefaultAsync(c => c.Id == commentId, ct)
            ?? throw new NotFoundException("Comment", commentId);

        EnsureOwnerOrManager(comment);

        _comments.Remove(comment);
        await _uow.SaveChangesAsync(ct);
    }

    private void EnsureOwnerOrManager(TaskComment comment)
    {
        if (comment.UserId == _currentUser.Id || _currentUser.IsAdminOrManager) return;
        throw new ForbiddenException("You can only modify your own comments.");
    }

    private async Task EnsureTaskExistsAsync(Guid taskId, CancellationToken ct)
    {
        if (!await _tasks.AnyAsync(t => t.Id == taskId, ct))
            throw new NotFoundException("Task", taskId);
    }
}
