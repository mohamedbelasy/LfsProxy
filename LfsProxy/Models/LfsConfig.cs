using System.ComponentModel.DataAnnotations;

namespace LfsProxy.Models;

public class LfsUser
{
    [Required(ErrorMessage = "User Uid is required.")]
    public string Uid { get; set; } = Guid.NewGuid().ToString();

    [Required(ErrorMessage = "User Username is required.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "User Password is required.")]
    public string Password { get; set; } = string.Empty;
}

public class LfsConfig
{
    [Required(ErrorMessage = "Lfs.StoragePath is required for file locking.")]
    public string StoragePath { get; set; } = string.Empty;

    public S3Config S3 { get; set; } = new();

    [Range(60, 86400, ErrorMessage = "PresignedUrlExpirySeconds must be between 60 and 86400.")]
    public int PresignedUrlExpirySeconds { get; set; } = 3600;

    public List<LfsUser> Users { get; set; } = [];
}

public class S3Config
{
    public string Endpoint { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;

    [Required(ErrorMessage = "S3.AccessKey is required.")]
    public string AccessKey { get; set; } = string.Empty;

    [Required(ErrorMessage = "S3.SecretKey is required.")]
    public string SecretKey { get; set; } = string.Empty;

    [Required(ErrorMessage = "S3.BucketName is required.")]
    public string BucketName { get; set; } = string.Empty;

    public bool UseSsl { get; set; } = true;
    public string? RootPathPrefix { get; set; }
    public bool PerRepoStorage { get; set; } = true;
    public bool S3ForcePathStyle { get; set; }
}