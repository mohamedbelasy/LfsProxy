using System.Security.Claims;
using LfsProxy.Models;
using LfsProxy.Services;
using Microsoft.AspNetCore.Mvc;

namespace LfsProxy.Controllers;

[ApiController]
[Route("lfs/{user}/{repo}/locks")]
public class LfsLocksController : ControllerBase
{
    private readonly JsonLockService _lockService;
    private readonly ILogger<LfsLocksController> _logger;

    public LfsLocksController(JsonLockService lockService, ILogger<LfsLocksController> logger)
    {
        _lockService = lockService;
        _logger = logger;
    }

    private (string UserId, string UserName) GetCurrentUser()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = User.Identity?.Name;

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userName))
            throw new InvalidOperationException("User is not properly authenticated.");
        return (userId, userName);
    }

    [HttpGet("")]
    public async Task<IActionResult> ListLocks([FromRoute] string user, [FromRoute] string repo,
        [FromQuery] string? path, [FromQuery] string? id)
    {
        var repositoryPath = $"{user}/{repo}";
        _logger.LogInformation("为仓库 '{RepoPath}' 列出锁, Path: '{Path}', ID: '{Id}'", repositoryPath, path, id);

        var allLocks = await _lockService.GetLocksByRepositoryAsync(repositoryPath);

        if (!string.IsNullOrEmpty(path)) allLocks = allLocks.Where(l => l.Path == path).ToList();
        if (!string.IsNullOrEmpty(id)) allLocks = allLocks.Where(l => l.Id == id).ToList();

        return Ok(new { locks = allLocks.Select(MapToLockResponse).ToList() });
    }

    [HttpPost("")]
    public async Task<IActionResult> CreateLock([FromRoute] string user, [FromRoute] string repo,
        [FromBody] CreateLockRequest request)
    {
        var repositoryPath = $"{user}/{repo}";
        var (userId, userName) = GetCurrentUser();
        _logger.LogInformation("用户 '{UserName}' 为仓库 '{RepoPath}' 的路径 '{Path}' 创建锁请求", userName, repositoryPath,
            request.Path);

        var (newLock, existingLock) =
            await _lockService.CreateLockAsync(repositoryPath, request.Path, userId, userName);

        if (existingLock != null)
        {
            _logger.LogWarning("文件 '{Path}' 已被 '{Owner}' 锁定，无法创建新锁", request.Path, existingLock.OwnerName);
            return Conflict(new { message = "the file is already locked", @lock = MapToLockResponse(existingLock) });
        }

        _logger.LogInformation("成功为 '{Path}' 创建锁，ID: {LockId}", request.Path, newLock!.Id);
        return StatusCode(201, new { @lock = MapToLockResponse(newLock) });
    }

    [HttpPost("verify")]
    public async Task<IActionResult> VerifyLocks([FromRoute] string user, [FromRoute] string repo)
    {
        var repositoryPath = $"{user}/{repo}";
        var (userId, _) = GetCurrentUser();
        _logger.LogInformation("验证仓库 '{RepoPath}' 的所有锁", repositoryPath);

        var allLocks = await _lockService.GetLocksByRepositoryAsync(repositoryPath);

        var response = new VerifyLocksResponse
        {
            Ours = allLocks.Where(l => l.OwnerId == userId).Select(MapToLockResponse).ToList(),
            Theirs = allLocks.Where(l => l.OwnerId != userId).Select(MapToLockResponse).ToList()
        };

        return Ok(response);
    }

    [HttpPost("{id}/unlock")]
    public async Task<IActionResult> Unlock([FromRoute] string user, [FromRoute] string repo, [FromRoute] string id)
    {
        var repositoryPath = $"{user}/{repo}";
        var (userId, userName) = GetCurrentUser();
        _logger.LogInformation("用户 '{UserName}' 尝试为仓库 '{RepoPath}' 解锁ID为 '{LockId}' 的文件", userName, repositoryPath, id);

        var (unlockedLock, isOwner) = await _lockService.UnlockAsync(repositoryPath, id, userId);

        if (unlockedLock == null) return NotFound(new { message = "lock not found" });

        if (!isOwner)
        {
            _logger.LogWarning("用户 '{UserName}' 尝试解锁不属于自己的锁 (ID: {LockId}, Owner: {OwnerName})", userName, id,
                unlockedLock.OwnerName);
            return Forbid();
        }

        _logger.LogInformation("成功解锁文件 '{Path}' (ID: {LockId})", unlockedLock.Path, id);
        return Ok(new { @lock = MapToLockResponse(unlockedLock) });
    }

    private LockResponse MapToLockResponse(FileLock l)
    {
        return new LockResponse
        {
            Id = l.Id,
            Path = l.Path,
            LockedAt = l.LockedAt,
            Owner = new LockOwner { Name = l.OwnerName }
        };
    }
}