using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using LfsProxy.Models;
using Microsoft.Extensions.Options;

namespace LfsProxy.Services;

public class JsonLockService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _fileSemaphores = new();
    private readonly string _locksDirectory;
    private readonly ILogger<JsonLockService> _logger;

    public JsonLockService(IOptions<LfsConfig> options, ILogger<JsonLockService> logger)
    {
        _logger = logger;
        var configuredPath = options.Value.StoragePath;
        if (string.IsNullOrEmpty(configuredPath))
        {
            var processModule = Process.GetCurrentProcess().MainModule;
            if (processModule != null)
            {
                var executablePath = Path.GetDirectoryName(processModule.FileName);
                _locksDirectory =
                    Path.Combine(!string.IsNullOrEmpty(executablePath) ? executablePath : AppContext.BaseDirectory,
                        "lfs-locks");
            }
            else
            {
                _locksDirectory = Path.Combine(AppContext.BaseDirectory, "lfs-locks");
            }
        }
        else
        {
            _locksDirectory = Path.GetFullPath(configuredPath);
        }

        Directory.CreateDirectory(_locksDirectory);
        _logger.LogInformation("JSON 锁存储目录: {Path}", _locksDirectory);
    }

    private string GetLockFilePath(string repository)
    {
        if (!Regex.IsMatch(repository, @"^[a-zA-Z0-9_\-/]+$"))
            throw new ArgumentException("Invalid repository name format.", nameof(repository));
        var safeRepoFilename = repository.Replace("/", "_") + ".json";
        return Path.Combine(_locksDirectory, safeRepoFilename);
    }

    private SemaphoreSlim GetSemaphoreForFile(string filePath)
    {
        return _fileSemaphores.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));
    }

    private async Task<Dictionary<string, FileLock>> ReadLocksAsync(string filePath)
    {
        if (!File.Exists(filePath)) return new Dictionary<string, FileLock>();
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<Dictionary<string, FileLock>>(json)
                   ?? new Dictionary<string, FileLock>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取锁文件失败: {FilePath}", filePath);
            return new Dictionary<string, FileLock>();
        }
    }

    private async Task WriteLocksAsync(string filePath, Dictionary<string, FileLock> locks)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(locks, options);
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task<(FileLock? newLock, FileLock? existingLock)> CreateLockAsync(string repository, string path,
        string ownerId, string ownerName)
    {
        var filePath = GetLockFilePath(repository);
        var semaphore = GetSemaphoreForFile(filePath);
        await semaphore.WaitAsync();
        try
        {
            var repoLocks = await ReadLocksAsync(filePath);
            var existingLock = repoLocks.Values.FirstOrDefault(l => l.Path == path);

            if (existingLock != null) return (null, existingLock);

            var newLock = new FileLock
            {
                Id = Guid.NewGuid().ToString(),
                Repository = repository,
                Path = path,
                OwnerId = ownerId,
                OwnerName = ownerName,
                LockedAt = DateTime.UtcNow
            };

            repoLocks[newLock.Id] = newLock;
            await WriteLocksAsync(filePath, repoLocks);

            return (newLock, null);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<List<FileLock>> GetLocksByRepositoryAsync(string repository)
    {
        var filePath = GetLockFilePath(repository);
        var semaphore = GetSemaphoreForFile(filePath);
        await semaphore.WaitAsync();
        try
        {
            var repoLocks = await ReadLocksAsync(filePath);
            return repoLocks.Values.ToList();
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<(FileLock? unlockedLock, bool isOwner)> UnlockAsync(string repository, string id, string ownerId)
    {
        var filePath = GetLockFilePath(repository);
        var semaphore = GetSemaphoreForFile(filePath);
        await semaphore.WaitAsync();
        try
        {
            var repoLocks = await ReadLocksAsync(filePath);
            if (repoLocks.TryGetValue(id, out var fileLock))
            {
                if (fileLock.OwnerId != ownerId) return (fileLock, false); // 不是所有者

                repoLocks.Remove(id);
                await WriteLocksAsync(filePath, repoLocks);
                return (fileLock, true); // 成功解锁
            }

            return (null, false); // 找不到锁
        }
        finally
        {
            semaphore.Release();
        }
    }
}