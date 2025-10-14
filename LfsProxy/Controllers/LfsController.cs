using LfsProxy.Models;
using LfsProxy.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LfsProxy.Controllers;

[ApiController]
[Route("lfs/{user}/{repo}/objects")]
public class LfsController : ControllerBase
{
    private readonly LfsConfig _config;
    private readonly ILogger<LfsController> _logger;
    private readonly S3Service _s3Service;

    public LfsController(S3Service s3Service, IOptions<LfsConfig> options, ILogger<LfsController> logger)
    {
        _s3Service = s3Service;
        _config = options.Value;
        _logger = logger;
    }

    [HttpPost("batch")]
    [Produces("application/vnd.git-lfs+json")]
    public async Task<IActionResult> BatchApi([FromBody] LfsRequest request, [FromRoute] string user,
        [FromRoute] string repo)
    {
        _logger.LogInformation("收到仓库 '{User}/{Repo}' 的LFS批量请求，操作: '{Op}'，对象数量: {Count}。",
            user, repo, request.Operation, request.Objects.Count);

        var isUpload = request.Operation.Equals("upload", StringComparison.OrdinalIgnoreCase);


        string? verifyUrl = null;
        if (isUpload)
        {
            verifyUrl = Url.Action(nameof(VerifyApi), "Lfs", new { user, repo }, Request.Scheme);

            if (string.IsNullOrEmpty(verifyUrl))
            {
                _logger.LogError("为仓库 '{User}/{Repo}' 生成 verify URL 失败。", user, repo);
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to generate verification URL.");
            }
        }

        var processingTasks = request.Objects
            .Select(obj => ProcessLfsObjectAsync(obj, user, repo, isUpload, verifyUrl));


        var results = await Task.WhenAll(processingTasks);

        var response = new LfsResponse { Objects = results.ToList() };
        return Ok(response);
    }

    private async Task<LfsObjectResponse> ProcessLfsObjectAsync(LfsObject obj, string user, string repo, bool isUpload,
        string? verifyUrl)
    {
        try
        {
            var objectResponse = new LfsObjectResponse { Oid = obj.Oid, Size = obj.Size };
            var objectExists = await _s3Service.ObjectExistsAsync(user, repo, obj.Oid);

            if (isUpload)
            {
                if (objectExists)
                {
                    _logger.LogInformation("对象 {Oid} 已存在，跳过上传操作。", obj.Oid);
                }
                else
                {
                    // 生成上传和验证链接
                    var uploadUrl = await _s3Service.GeneratePresignedUploadUrlAsync(user, repo, obj.Oid);
                    objectResponse.Actions["upload"] = new LfsAction
                        { Href = uploadUrl, ExpiresIn = _config.PresignedUrlExpirySeconds };

                    if (!string.IsNullOrEmpty(verifyUrl))
                        objectResponse.Actions["verify"] = new LfsAction
                            { Href = verifyUrl, ExpiresIn = _config.PresignedUrlExpirySeconds };
                }
            }
            else
            {
                if (objectExists)
                {
                    // 生成下载链接
                    var downloadUrl = await _s3Service.GeneratePresignedDownloadUrlAsync(user, repo, obj.Oid);
                    objectResponse.Actions["download"] = new LfsAction
                        { Href = downloadUrl, ExpiresIn = _config.PresignedUrlExpirySeconds };
                }
                else
                {
                    _logger.LogWarning("请求下载的对象不存在, OID: {Oid}", obj.Oid);
                    objectResponse.Error = new LfsError { Code = 404, Message = "Object not found" };
                }
            }

            return objectResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理LFS对象时发生错误, OID: {Oid}", obj.Oid);
            return new LfsObjectResponse
            {
                Oid = obj.Oid,
                Size = obj.Size,
                Error = new LfsError { Code = 500, Message = "Internal server error while processing object." }
            };
        }
    }

    [HttpPost("verify")]
    public async Task<IActionResult> VerifyApi([FromBody] LfsVerifyRequest request, [FromRoute] string user,
        [FromRoute] string repo)
    {
        _logger.LogInformation("正在为仓库 '{User}/{Repo}' 验证对象 {Oid}。", user, repo, request.Oid);

        var (success, errorMessage) = await _s3Service.VerifyObjectAsync(user, repo, request.Oid, request.Size);

        if (success) return Ok();

        return errorMessage.Contains("not found")
            ? NotFound(new { message = errorMessage })
            : StatusCode(422, new { message = errorMessage });
    }
}