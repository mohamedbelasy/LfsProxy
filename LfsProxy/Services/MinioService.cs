using LfsProxy.Models;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace LfsProxy.Services;

public class MinioService
{
    private readonly LfsConfig _config;
    private readonly ILogger<MinioService> _logger;
    private readonly IMinioClient _minioClient;

    public MinioService(IMinioClient minioClient, IOptions<LfsConfig> options, ILogger<MinioService> logger)
    {
        _minioClient = minioClient;
        _config = options.Value;
        _logger = logger;
    }

    public async Task<string> GeneratePresignedUploadUrlAsync(string user, string repo, string oid)
    {
        var objectKey = GetObjectKey(user, repo, oid);
        var args = new PresignedPutObjectArgs()
            .WithBucket(_config.S3.BucketName)
            .WithObject(objectKey)
            .WithExpiry(_config.PresignedUrlExpirySeconds);

        return await _minioClient.PresignedPutObjectAsync(args);
    }

    public async Task<bool> ObjectExistsAsync(string user, string repo, string oid)
    {
        var objectKey = GetObjectKey(user, repo, oid);
        try
        {
            var args = new StatObjectArgs()
                .WithBucket(_config.S3.BucketName)
                .WithObject(objectKey);

            var stats = await _minioClient.StatObjectAsync(args);
            return stats != null && stats.Size > 0;
        }
        catch (ObjectNotFoundException)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查对象 {Oid} 是否存在时发生意外错误，键值：{Key}", oid, objectKey);
            throw;
        }
    }

    public Task<ObjectStat> StatObjectAsync(string user, string repo, string oid)
    {
        var objectKey = GetObjectKey(user, repo, oid);
        var args = new StatObjectArgs()
            .WithBucket(_config.S3.BucketName)
            .WithObject(objectKey);
        return _minioClient.StatObjectAsync(args);
    }

    public async Task<string> GeneratePresignedDownloadUrlAsync(string user, string repo, string oid)
    {
        var objectKey = GetObjectKey(user, repo, oid);
        var args = new PresignedGetObjectArgs()
            .WithBucket(_config.S3.BucketName)
            .WithObject(objectKey)
            .WithExpiry(_config.PresignedUrlExpirySeconds);

        return await _minioClient.PresignedGetObjectAsync(args);
    }

    public async Task<(bool Success, string ErrorMessage)> VerifyObjectAsync(string user, string repo, string oid,
        long expectedSize)
    {
        var objectKey = GetObjectKey(user, repo, oid);
        try
        {
            var args = new StatObjectArgs()
                .WithBucket(_config.S3.BucketName)
                .WithObject(objectKey);
            var stats = await _minioClient.StatObjectAsync(args);

            if (stats.Size == expectedSize) return (true, string.Empty);

            var errorMessage = $"Size mismatch. Expected: {expectedSize}, Actual: {stats.Size}";
            _logger.LogError("对象 {Oid} 验证失败：{Message}", oid, errorMessage);
            return (false, errorMessage);
        }
        catch (ObjectNotFoundException)
        {
            var errorMessage = "Object not found during verification.";
            _logger.LogError("对象 {Oid} 验证失败：{Message}", oid, errorMessage);
            return (false, errorMessage);
        }
    }

    public string GetObjectKey(string user, string repo, string oid)
    {
        var objectSegment = $"{oid.Substring(0, 2)}/{oid.Substring(2, 2)}/{oid}";
        string finalPath;
        if (_config.S3.PerRepoStorage)
        {
            var repoPath = $"{user}/{repo}";
            finalPath = Path.Combine(repoPath, objectSegment).Replace("\\", "/");
        }
        else
        {
            finalPath = objectSegment;
        }

        return !string.IsNullOrEmpty(_config.S3.RootPathPrefix)
            ? Path.Combine(_config.S3.RootPathPrefix, finalPath).Replace("\\", "/")
            : finalPath;
    }
}