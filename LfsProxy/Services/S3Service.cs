using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using LfsProxy.Models;
using Microsoft.Extensions.Options;

namespace LfsProxy.Services;

public class S3Service
{
    private readonly LfsConfig _config;
    private readonly ILogger<S3Service> _logger;
    private readonly IAmazonS3 _s3Client;

    public S3Service(IAmazonS3 s3Client, IOptions<LfsConfig> options, ILogger<S3Service> logger)
    {
        _s3Client = s3Client;
        _config = options.Value;
        _logger = logger;
    }

    public Task<string> GeneratePresignedUploadUrlAsync(string user, string repo, string oid)
    {
        var objectKey = GetObjectKey(user, repo, oid);
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _config.S3.BucketName,
            Key = objectKey,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.AddSeconds(_config.PresignedUrlExpirySeconds)
        };

        var url = _s3Client.GetPreSignedURL(request);
        return Task.FromResult(url);
    }

    public async Task<bool> ObjectExistsAsync(string user, string repo, string oid)
    {
        var objectKey = GetObjectKey(user, repo, oid);
        try
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = _config.S3.BucketName,
                Key = objectKey
            };
            var metadata = await _s3Client.GetObjectMetadataAsync(request);
            return metadata != null && metadata.ContentLength > 0;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查对象 {Oid} 是否存在时发生意外错误，键值：{Key}", oid, objectKey);
            throw;
        }
    }

    public Task<GetObjectMetadataResponse> GetObjectMetadataAsync(string user, string repo, string oid)
    {
        var objectKey = GetObjectKey(user, repo, oid);
        var request = new GetObjectMetadataRequest
        {
            BucketName = _config.S3.BucketName,
            Key = objectKey
        };
        return _s3Client.GetObjectMetadataAsync(request);
    }

    public Task<string> GeneratePresignedDownloadUrlAsync(string user, string repo, string oid)
    {
        var objectKey = GetObjectKey(user, repo, oid);
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _config.S3.BucketName,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddSeconds(_config.PresignedUrlExpirySeconds)
        };

        var url = _s3Client.GetPreSignedURL(request);
        return Task.FromResult(url);
    }

    public async Task<(bool Success, string ErrorMessage)> VerifyObjectAsync(string user, string repo, string oid,
        long expectedSize)
    {
        var objectKey = GetObjectKey(user, repo, oid);
        try
        {
            var metadata = await GetObjectMetadataAsync(user, repo, oid);

            if (metadata.ContentLength == expectedSize) return (true, string.Empty);

            var errorMessage = $"Size mismatch. Expected: {expectedSize}, Actual: {metadata.ContentLength}";
            _logger.LogError("对象 {Oid} 验证失败：{Message}", oid, errorMessage);
            return (false, errorMessage);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
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