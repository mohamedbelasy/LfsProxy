namespace LfsProxy.Models;

public class LfsUser
{
    public string Uid { get; set; } = Guid.NewGuid().ToString();
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LfsConfig
{
    public string StoragePath { get; set; } = string.Empty;
    public S3Config S3 { get; set; } = new();
    public int PresignedUrlExpirySeconds { get; set; } = 3600;

    public List<LfsUser> Users { get; set; } = [];
}

public class S3Config
{
    public string Endpoint { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public bool UseSsl { get; set; }
    public string? RootPathPrefix { get; set; }
    public bool PerRepoStorage { get; set; } = true;
}