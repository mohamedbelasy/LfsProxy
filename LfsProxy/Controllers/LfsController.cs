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
    private readonly MinioService _minioService;

    public LfsController(MinioService minioService, IOptions<LfsConfig> options, ILogger<LfsController> logger)
    {
        _minioService = minioService;
        _config = options.Value;
        _logger = logger;
    }

    [HttpPost("batch")]
    [Produces("application/vnd.git-lfs+json")]
    public async Task<IActionResult> BatchApi([FromBody] LfsRequest request, [FromRoute] string user,
        [FromRoute] string repo)
    {
        _logger.LogInformation("收到仓库 '{User}/{Repo}' 的LFS批量请求，操作: '{Op}'。", user, repo, request.Operation);

        var response = new LfsResponse();
        var isUpload = request.Operation.Equals("upload", StringComparison.OrdinalIgnoreCase);

        foreach (var obj in request.Objects)
            try
            {
                var objectResponse = new LfsObjectResponse { Oid = obj.Oid, Size = obj.Size };
                var objectExists = await _minioService.ObjectExistsAsync(user, repo, obj.Oid);
                if (isUpload)
                {
                    if (objectExists)
                    {
                        _logger.LogInformation("对象 {Oid} 已存在，跳过上传操作。", obj.Oid);
                    }
                    else
                    {
                        var uploadUrl = await _minioService.GeneratePresignedUploadUrlAsync(user, repo, obj.Oid);
                        objectResponse.Actions["upload"] = new LfsAction
                            { Href = uploadUrl, ExpiresIn = _config.PresignedUrlExpirySeconds };

                        var verifyUrl = Url.Action("VerifyApi", "Lfs", new { user, repo }, Request.Scheme)!;
                        objectResponse.Actions["verify"] = new LfsAction
                            { Href = verifyUrl, ExpiresIn = _config.PresignedUrlExpirySeconds };
                    }
                }
                else
                {
                    if (objectExists)
                    {
                        var downloadUrl = await _minioService.GeneratePresignedDownloadUrlAsync(user, repo, obj.Oid);
                        objectResponse.Actions["download"] = new LfsAction
                            { Href = downloadUrl, ExpiresIn = _config.PresignedUrlExpirySeconds };
                    }
                    else
                    {
                        _logger.LogWarning("请求下载的对象不存在, OID: {Oid}", obj.Oid);
                        objectResponse.Error = new LfsError { Code = 404, Message = "Object not found" };
                    }
                }

                response.Objects.Add(objectResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理LFS对象时发生错误, OID: {Oid}", obj.Oid);
                response.Objects.Add(new LfsObjectResponse
                {
                    Oid = obj.Oid,
                    Size = obj.Size,
                    Error = new LfsError { Code = 500, Message = "Internal server error while processing object." }
                });
            }

        return Ok(response);
    }


    [HttpPost("verify")]
    [ActionName("VerifyApi")]
    public async Task<IActionResult> VerifyApi([FromBody] LfsVerifyRequest request, [FromRoute] string user,
        [FromRoute] string repo)
    {
        _logger.LogInformation("正在为仓库 '{User}/{Repo}' 验证对象 {Oid}。", user, repo, request.Oid);

        var (success, errorMessage) = await _minioService.VerifyObjectAsync(user, repo, request.Oid, request.Size);

        if (success) return Ok();

        if (errorMessage.Contains("not found")) return NotFound(new { message = errorMessage });

        return StatusCode(422, new { message = errorMessage });
    }
}