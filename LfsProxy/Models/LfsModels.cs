using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace LfsProxy.Models;

public class LfsRequest
{
    [JsonPropertyName("operation")] public string Operation { get; set; } = string.Empty;

    [JsonPropertyName("objects")] public List<LfsObject> Objects { get; set; } = [];
}

public class LfsObject
{
    [JsonPropertyName("oid")] public string Oid { get; set; } = string.Empty;

    [JsonPropertyName("size")] public long Size { get; set; }
}

public class LfsResponse
{
    [JsonPropertyName("objects")] public List<LfsObjectResponse> Objects { get; set; } = [];
}

public class LfsObjectResponse
{
    [JsonPropertyName("oid")] public string Oid { get; set; } = string.Empty;

    [JsonPropertyName("size")] public long Size { get; set; }

    [JsonPropertyName("actions")] public Dictionary<string, LfsAction> Actions { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("error")]
    public LfsError? Error { get; set; }
}

public class LfsAction
{
    [JsonPropertyName("href")] public string Href { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
}

public class LfsError
{
    [JsonPropertyName("code")] public int Code { get; set; }

    [JsonPropertyName("message")] public string Message { get; set; } = string.Empty;
}

public class LfsVerifyRequest
{
    [JsonPropertyName("oid")] public string Oid { get; set; } = string.Empty;

    [JsonPropertyName("size")] public long Size { get; set; }
}

public class FileLock
{
    [Key] public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Path { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public DateTime LockedAt { get; set; } = DateTime.UtcNow;
}

public class CreateLockRequest
{
    [JsonPropertyName("path")] public string Path { get; set; } = string.Empty;
}

public class LockResponse
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("path")] public string Path { get; set; } = string.Empty;
    [JsonPropertyName("locked_at")] public DateTime LockedAt { get; set; }
    [JsonPropertyName("owner")] public LockOwner Owner { get; set; } = new();
}

public class LockOwner
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
}

public class VerifyLocksResponse
{
    [JsonPropertyName("ours")] public List<LockResponse> Ours { get; set; } = [];

    [JsonPropertyName("theirs")] public List<LockResponse> Theirs { get; set; } = [];
}