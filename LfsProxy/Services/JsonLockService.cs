using System.Collections.Concurrent;
using System.Text.Json;
using LfsProxy.Models;
using Microsoft.Extensions.Options;

namespace LfsProxy.Services;

public class JsonLockService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> FileSemaphores = new();
    private readonly ILogger<JsonLockService> _logger;
    private readonly string _storagePath;

    public JsonLockService(IOptions<LfsConfig> options, ILogger<JsonLockService> logger)
    {
        _storagePath = options.Value.StoragePath;
        _logger = logger;
        if (!Directory.Exists(_storagePath)) Directory.CreateDirectory(_storagePath);
    }

    private string GetLockFilePath(string repositoryPath)
    {
        var safeFileName = $"{repositoryPath.Replace('/', '_')}.json";
        return Path.Combine(_storagePath, safeFileName);
    }

    private SemaphoreSlim GetSemaphoreForFile(string filePath)
    {
        return FileSemaphores.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));
    }

    public async Task<List<FileLock>> GetLocksByRepositoryAsync(string repositoryPath)
    {
        var lockFilePath = GetLockFilePath(repositoryPath);
        var semaphore = GetSemaphoreForFile(lockFilePath);

        await semaphore.WaitAsync();
        try
        {
            return await ReadLocksFromFileAsync(lockFilePath);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<(FileLock? newLock, FileLock? existingLock)> CreateLockAsync(string repositoryPath, string path,
        string ownerId, string ownerName)
    {
        var lockFilePath = GetLockFilePath(repositoryPath);
        var semaphore = GetSemaphoreForFile(lockFilePath);

        await semaphore.WaitAsync();
        try
        {
            var locks = await ReadLocksFromFileAsync(lockFilePath);
            var existingLock = locks.FirstOrDefault(l => l.Path == path);

            if (existingLock != null) return (null, existingLock);

            var newLock = new FileLock
            {
                Id = Guid.NewGuid().ToString(),
                Path = path,
                LockedAt = DateTime.UtcNow,
                OwnerId = ownerId,
                OwnerName = ownerName
            };

            locks.Add(newLock);
            await WriteLocksToFileAsync(lockFilePath, locks);

            return (newLock, null);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<(FileLock? unlockedLock, bool isOwner)> UnlockAsync(string repositoryPath, string lockId,
        string ownerId)
    {
        var lockFilePath = GetLockFilePath(repositoryPath);
        var semaphore = GetSemaphoreForFile(lockFilePath);

        await semaphore.WaitAsync();
        try
        {
            var locks = await ReadLocksFromFileAsync(lockFilePath);
            var lockToUnlock = locks.FirstOrDefault(l => l.Id == lockId);

            if (lockToUnlock == null) return (null, false);

            if (lockToUnlock.OwnerId != ownerId) return (lockToUnlock, false);

            locks.Remove(lockToUnlock);
            await WriteLocksToFileAsync(lockFilePath, locks);

            return (lockToUnlock, true);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<List<FileLock>> ReadLocksFromFileAsync(string filePath)
    {
        if (!File.Exists(filePath)) return [];

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<List<FileLock>>(json) ?? new List<FileLock>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "无法读取或反序列化锁文件: {FilePath}", filePath);
            return [];
        }
    }

    private static async Task WriteLocksToFileAsync(string filePath, List<FileLock> locks)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(locks, options);
        await File.WriteAllTextAsync(filePath, json);
    }
}