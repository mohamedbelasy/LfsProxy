# LfsProxy - 一个轻量级的自托管 Git LFS 服务器

`LfsProxy` 是一个使用 C# 和 ASP.NET Core 构建的轻量级、自托管 Git LFS (Large File Storage) 服务器。它旨在为需要管理大文件的团队或个人提供一个简单、高效且易于部署的解决方案，无需依赖庞大的 Git 托管平台。

## ✨ 核心功能

*   **完整的 LFS 协议支持**: 实现了 Git LFS 的核心 **Batch API** (用于文件上传/下载) 和 **File Locking API** (用于文件独占锁定)，与 Git LFS 客户端完全兼容。
*   **兼容 S3 的后端存储**: 使用 Minio、AWS S3 或任何其他兼容 S3 的对象存储作为大文件后端。通过预签名 URL (Presigned URL) 保证了上传下载的高效性和安全性。
*   **无数据库依赖**: 文件锁定状态通过本地 JSON 文件进行管理，无需配置和维护外部数据库，极大地简化了部署和备份流程。
*   **简单而安全的用户认证**: 内置基于配置文件的 HTTP Basic 认证。默认情况下，如果未配置任何用户，服务将拒绝所有请求，防止意外的未授权访问。
*   **灵活的存储结构**: 支持将不同仓库的文件隔离存储在各自的目录中 (`PerRepoStorage: true`)，也支持所有仓库的文件统一存放。
*   **启动时健康检查**: 服务器在启动时会自动检查与 S3 存储桶的连接，确保配置正确无误，便于快速排查问题。
*   **为反向代理优化**: 内置支持 `BasePath` 和 `ForwardedHeaders`，可以轻松地部署在 Nginx、Caddy 等反向代理之后，并正确处理 URL 路径和 HTTPS 协议。

## 🚀 快速开始

### 先决条件

1.  [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) 或更高版本。
2.  一个兼容 S3 的对象存储服务，例如 [Minio](https://min.io/)。
3.  本地已安装 [Git](https://git-scm.com/) 和 [Git LFS](https://git-lfs.github.com/) 扩展。

### 步骤 1: 准备 S3 (Minio)

1.  启动一个 Minio 实例。
2.  创建一个 Bucket (存储桶)，例如 `git-lfs-test`。

### 步骤 2: 克隆并配置项目

1.  克隆本仓库到本地：
    ```bash
    git clone https://github.com/zedoCN/LfsProxy.git
    cd LfsProxy
    ```

2.  打开 `appsettings.json` 文件，根据你的环境修改配置（详细配置见下文）。

### 步骤 3: 运行 LFS 服务器

在项目根目录下，执行以下命令：

```bash
dotnet run
```

服务启动后，将首先验证 S3 连接。成功后，默认将监听在 `http://localhost:5074`。

### 步骤 4: 配置你的 Git 仓库

进入你的本地 Git 仓库，并执行以下命令：

1.  **设置 LFS 服务器地址**：
    ```bash
    # 假设你的服务部署在 https://your-domain.com/lfs-server
    git config -f .lfsconfig lfs.url "https://your-domain.com/lfs-server"
    ```
    这会在仓库根目录创建一个 `.lfsconfig` 文件，方便团队共享配置。

2.  **开始追踪大文件**：
    ```bash
    git lfs track "*.zip" "*.psd"
    ```
    确保 `.gitattributes` 文件被添加到 Git 中。

3.  **提交和推送**：
    ```bash
    git add .
    git commit -m "Add large files"
    git push origin main
    ```
    此时，终端会提示你输入在 `appsettings.json` 中配置的用户名和密码。

## ⚙️ 配置详解

所有配置均在 `appsettings.json` 文件中完成。

| 配置项 (路径)                         | 说明                                                                                                                                                             | 示例值                                     |
| ------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------ |
| `BasePath`                            | **可选**，应用的 URL 路径前缀。如果你希望通过 `https://domain.com/lfs-server` 访问服务，请将其设置为 `lfs-server`。这对于反向代理部署非常有用。                       | `"lfs-server"`                             |
| `Lfs.StoragePath`                     | 文件锁 JSON 文件的本地存储路径。应用需要对此目录有读写权限。                                                                                                       | `"./locks"`                                  |
| `Lfs.PresignedUrlExpirySeconds`       | S3 预签名 URL 的有效时间（秒）。Git LFS 客户端必须在此时间内完成上传或下载。                                                                                       | `3600`                                     |
| `Lfs.S3.Endpoint`                     | S3 服务的完整 URL 地址。                                                                                                                                         | `"http://127.0.0.1:9000"`                  |
| `Lfs.S3.Region`                       | S3 区域。对于 AWS S3 是必需的，对于 Minio 等自托管服务通常可以留空或设置为 `us-east-1`。                                                                           | `"us-east-1"`                              |
| `Lfs.S3.AccessKey`                    | S3 服务的 Access Key。                                                                                                                                           | `"lfs-user"`                               |
| `Lfs.S3.SecretKey`                    | S3 服务的 Secret Key。                                                                                                                                           | `"lfs-password"`                           |
| `Lfs.S3.BucketName`                   | 用于存储 LFS 对象的 S3 存储桶名称。                                                                                                                              | `"git-lfs-test"`                           |
| `Lfs.S3.UseSsl`                       | 是否通过 HTTPS 连接 S3 服务。                                                                                                                                    | `false`                                    |
| `Lfs.S3.S3ForcePathStyle`             | **重要**：设置为 `true` 以使用**路径风格**的 URL (`http://endpoint/bucket/key`)，这对于 Minio 等自托管 S3 服务通常是必需的。设置为 `false` 以使用**虚拟主机风格**的 URL (`http://bucket.endpoint/key`)，这是 AWS S3 的默认设置。 | `true`                                     |
| `Lfs.S3.RootPathPrefix`               | **可选**，在 S3 存储桶内为所有 LFS 对象添加一个统一的路径前缀，便于组织和隔离。                                                                                  | `"lfs/objects"`                            |
| `Lfs.S3.PerRepoStorage`               | 是否为每个 Git 仓库在 S3 中创建独立的存储路径。详见下文的“存储结构”。                                                                                            | `true`                                     |
| `Lfs.Users`                           | 用户认证列表。这是一个数组，可以配置多个用户。                                                                                                                   | `[...]`                                    |
| `Lfs.Users[].Uid`                      | **必需**，用户的唯一标识符 (GUID)。此 ID 用于文件锁定功能，以识别锁的所有者。                                                                                      | `"0123...1920"`                            |
| `Lfs.Users[].Username`                | Git LFS 客户端进行认证时使用的用户名。                                                                                                                           | `"user"`                                   |
| `Lfs.Users[].Password`                | Git LFS 客户端进行认证时使用的密码。                                                                                                                             | `"pswd"`                                   |

## 🌍 部署与反向代理

`LfsProxy` 被设计为在反向代理（如 Nginx, Caddy, Traefik）之后运行，由反向代理处理 HTTPS 终止、域名路由等。

### 关键配置

1.  **`BasePath`**: 如上所述，如果你的反向代理将一个子路径（例如 `/lfs-server`）转发给 `LfsProxy`，你必须在 `appsettings.json` 中设置 `BasePath`。这使得应用能够正确地生成内部路由和 URL，无需在代理层进行复杂的 `path_rewrite`。

2.  **`ForwardedHeaders`**: 项目已默认配置为信任来自反向代理的 `X-Forwarded-For` 和 `X-Forwarded-Proto` 头。这使得应用即使在 `http://` 上运行，也能知道客户端是通过 `https` 连接的，从而生成正确的 `https://` 链接（例如 LFS Verify URL）。

### Nginx 配置示例

假设 `LfsProxy` 运行在 `http://127.0.0.1:5074`，你希望通过 `https://your-domain.com/lfs-server` 访问它。

`appsettings.json` 中应设置:
```json
"BasePath": "lfs-server"
```

你的 Nginx 配置文件中应包含类似以下的 `location` 块：

```nginx
location /lfs-server/ {
    # 将请求转发到 LfsProxy 应用
    proxy_pass http://127.0.0.1:5074/;

    # 设置必要的头信息，以便应用能正确识别客户端信息
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    
    # 关键：告诉应用客户端是通过 HTTPS 连接的
    proxy_set_header X-Forwarded-Proto $scheme; 
}
```

## 🗂️ 存储结构

通过 `Lfs.S3.PerRepoStorage` 和 `Lfs.S3.RootPathPrefix`，可以灵活控制对象在 S3 中的存储路径。

*   **当 `PerRepoStorage` 为 `true` (默认)**:
    对象路径为: `<RootPathPrefix>/<user>/<repo>/<oid_prefix_1>/<oid_prefix_2>/<oid>`
    *示例*: `lfs/objects/my-user/my-project/aa/bb/aabbcc...`

*   **当 `PerRepoStorage` 为 `false`**:
    对象路径为: `<RootPathPrefix>/<oid_prefix_1>/<oid_prefix_2>/<oid>`
    *示例*: `lfs/objects/aa/bb/aabbcc...`

## 💡 设计与实现亮点

*   **S3 启动健康检查**: 为避免在运行时出现难以诊断的连接问题，`LfsProxy` 在启动时会立即尝试连接 S3 并获取存储桶信息。如果失败，它会记录一条关键错误并退出，强制要求管理员修复配置。
*   **安全的认证默认设置**: 认证中间件被设计为“默认拒绝”。如果在配置中没有提供任何用户，服务器会返回 `500 Internal Server Error` 并记录错误，而不是允许匿名访问。这是一种确保安全的纵深防御策略。
*   **并发安全的文件锁**: `JsonLockService` 使用 `SemaphoreSlim` 来确保对每个锁文件的读写操作都是线程安全的，即使在高并发场景下也能保证锁状态的一致性。
*   **用户身份与文件锁绑定**: 用户的 `Uid` 属性是文件锁定功能的核心。它被用作所有者的唯一标识，确保只有锁的创建者才能解锁文件，提供了可靠的访问控制。