# LfsProxy - 一个轻量级的自托管 Git LFS 服务器

`LfsProxy` 是一个使用 C# 和 ASP.NET Core 构建的轻量级、自托管 Git LFS (Large File Storage) 服务器。它旨在为需要管理大文件的团队或个人提供一个简单、高效且易于部署的解决方案，无需依赖庞大的 Git 托管平台。

## ✨ 核心功能

*   **完整的 LFS 协议支持**: 实现了 Git LFS 的核心 **Batch API** (用于文件上传/下载) 和 **File Locking API** (用于文件独占锁定)。
*   **兼容 S3 的后端存储**: 使用 Minio 或任何其他兼容 S3 的对象存储服务作为大文件的存储后端，通过预签名 URL (Presigned URL) 保证了上传下载的高效性和安全性。
*   **无数据库依赖**: 文件锁定状态通过本地 JSON 文件进行管理，无需配置和维护外部数据库，极大地简化了部署流程。
*   **简单的用户认证**: 内置基于配置文件的 HTTP Basic 认证，可以轻松地在 `appsettings.json` 中添加和管理用户。
*   **灵活的存储结构**: 支持将不同仓库的文件隔离存储在各自的目录中 (`PerRepoStorage: true`)，也支持所有文件统一存放。

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
    git clone <your-repository-url>
    cd LfsProxy
    ```

2.  打开 `appsettings.json` 文件，根据你的环境修改配置：
    ```json
    {
      // ... 其他配置 ...
      "BasePath": "lfs-server", // URL路径前缀, 可选
      "Lfs": {
        "StoragePath": "./locks", // 文件锁的本地存储路径
        "S3": {
          "Endpoint": "http://127.0.0.1:9000", // 你的S3/Minio地址
          "AccessKey": "lfs-user",             // 你的AccessKey
          "SecretKey": "lfs-password",         // 你的SecretKey
          "BucketName": "git-lfs-test",        // 你创建的Bucket名称
          "RootPathPrefix": "lfs/objects",     // S3中对象的统一前缀, 可选
          "PerRepoStorage": true               // 是否为每个仓库创建独立的存储路径
        },
        "PresignedUrlExpirySeconds": 3600, // 预签名URL的有效时间(秒)
        "Users": [
          {
            "Uid": "01234567-8910-1112-1314-151617181920", // 用户唯一ID
            "Username": "user",  // Git LFS 客户端使用的用户名
            "Password": "pswd"   // Git LFS 客户端使用的密码
          }
        ]
      },
      // ... 其他配置 ...
    }
    ```

### 步骤 3: 运行 LFS 服务器

在项目根目录下，执行以下命令：

```bash
dotnet run
```

服务启动后，默认将监听在 `http://localhost:5074`。完整的 LFS 服务地址是 `http://localhost:5074/lfs-server` (基于 `BasePath` 配置)。

### 步骤 4: 配置你的 Git 仓库

现在，进入你希望使用此 LFS 服务的本地 Git 仓库，并执行以下命令：

1.  **设置 LFS 服务器地址**：
    ```bash
    git config -f .lfsconfig lfs.url "http://localhost:5074/lfs-server"
    ```
    这会在仓库根目录创建一个 `.lfsconfig` 文件，方便团队共享配置。

2.  **开始追踪大文件**：
    ```bash
    # 追踪所有 .zip 文件
    git lfs track "*.zip"
    
    # 追踪所有 .psd 文件
    git lfs track "*.psd"
    ```
    确保 ` .gitattributes` 文件被添加到 Git 中。

3.  **提交和推送**：
    当你添加、提交并推送文件时，Git LFS 会自动拦截大文件，向你的 `LfsProxy` 服务器请求上传许可，然后将文件上传到 Minio。
    ```bash
    git add .
    git commit -m "Add large files"
    git push origin main
    ```
    此时，终端会提示你输入在 `appsettings.json` 中配置的用户名 (`user`) 和密码 (`pswd`)。

## 🔧 API 端点

`LfsProxy` 实现了 Git LFS 规范定义的标准 API 端点。

*   **Batch API**: `POST /{user}/{repo}/objects/batch`
    *   处理大文件的上传和下载请求。
*   **Locking API**:
    *   `GET /{user}/{repo}/locks` - 列出文件锁。
    *   `POST /{user}/{repo}/locks` - 创建一个文件锁。
    *   `POST /{user}/{repo}/locks/verify` - 验证用户持有的锁。
    *   `POST /{user}/{repo}/locks/{id}/unlock` - 解锁一个文件。

## 🔒 存储结构

通过 `S3` 配置中的 `PerRepoStorage` 和 `RootPathPrefix`，可以灵活控制对象在 S3 中的存储路径。

*   **当 `PerRepoStorage` 为 `true` (默认)**:
    对象路径为: `<RootPathPrefix>/<user>/<repo>/<oid_prefix_1>/<oid_prefix_2>/<oid>`
    *示例*: `lfs/objects/my-user/my-project/aa/bb/aabbcc...`

*   **当 `PerRepoStorage` 为 `false`**:
    对象路径为: `<RootPathPrefix>/<oid_prefix_1>/<oid_prefix_2>/<oid>`
    *示例*: `lfs/objects/aa/bb/aabbcc...`

## 部署建议

*   **生产环境**建议使用 Docker 容器化部署。
*   使用**环境变量**或**Secrets Manager**等工具来管理敏感配置（如 S3 密钥和用户密码），而不是直接写在 `appsettings.json` 中。
*   在应用前部署一个反向代理（如 Nginx 或 Caddy）来处理 HTTPS 加密和请求路由。
