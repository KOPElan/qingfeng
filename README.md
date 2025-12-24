# 清风 (QingFeng)

一款基于.NET平台C#语言开发的个人家庭服务器主页，使用Blazor技术栈实现。

与AI（github copilot）协同实现

# 警告：这是一个实验性的项目，并且处于活跃的开发阶段

## 功能特性

- **个性化主页**：可定制的主页配置
  - 添加、编辑、删除快捷方式
  - 固定常用应用到 Dock
  - 自定义布局配置
  - SQLite 数据库持久化存储
  - 自动从浏览器 localStorage 迁移数据

- **系统监控**：实时监控系统资源信息
  - CPU使用率和核心数
  - 内存使用情况
  - 磁盘空间使用
  - 网络接口状态和流量统计

- **磁盘管理**：管理本地磁盘和网络磁盘
  - 查看所有磁盘信息（已挂载和未挂载）
  - 挂载/卸载本地磁盘（Linux）
  - 磁盘挂载向导（支持临时和永久挂载）
  - 网络磁盘管理（CIFS/SMB 和 NFS）
  - 网络磁盘挂载向导（支持Windows共享和NFS共享）
  - 磁盘电源管理（休眠/APM设置）
  - 查看Samba共享配置

- **Docker管理**：管理Docker容器和镜像
  - 查看容器列表
  - 启动/停止/重启/删除容器
  - 查看镜像列表
  - 从 Docker 容器快速创建快捷方式

- **文件管理器**：完整的Linux宿主机文件管理功能
  - 浏览目录结构（网格视图和列表视图）
  - 创建、删除、重命名文件和文件夹
  - 文件上传（支持多文件，最大100MB）
  - 文件下载
  - 文件复制和移动
  - 文件搜索（支持通配符模式）
  - 文件预览（图片、文本文件、PDF和Office文档）
    - 图片预览（JPG、PNG、GIF、BMP、SVG、WebP）
    - 文本预览（TXT、JSON、XML、CSV、HTML、CSS、JS等）
    - PDF预览（使用PDF.js）
    - Word文档预览（DOC、DOCX，使用Mammoth.js）
    - Excel表格预览（XLS、XLSX，使用SheetJS，支持多工作表切换）
  - 文件类型图标识别
  - 操作通知和错误提示
  - 自动处理文件名冲突

## 技术栈

- .NET 10.0
- Blazor Server
- MudBlazor UI 框架
- Entity Framework Core
- SQLite 数据库
- Docker.DotNet
- System.Diagnostics.PerformanceCounter

## 快速开始

### 前置要求

- .NET SDK 10.0 或更高版本
- （可选）Docker（用于容器管理功能）
- （Linux）磁盘管理功能需要以下系统工具：
  - `lsblk` - 列出块设备信息
  - `mount` / `umount` - 挂载和卸载文件系统
  - `hdparm` - 磁盘电源管理和性能调优（可选）
  - `cifs-utils` - CIFS/SMB网络磁盘支持（可选）
  - `nfs-common` - NFS网络磁盘支持（可选）

**安装网络磁盘工具**（Ubuntu/Debian）：
```bash
sudo apt-get update
sudo apt-get install cifs-utils nfs-common
```

**注意**：磁盘挂载和电源管理功能需要 sudo 权限。建议使用以下方式之一运行：

1. 使用 sudo 运行应用程序：
   ```bash
   sudo dotnet run --urls "http://0.0.0.0:5000"
   ```

2. 或者配置 sudoers 文件允许特定用户无密码执行 mount/umount/hdparm 命令：
   ```bash
   # 编辑 sudoers 文件
   sudo visudo
   
   # 添加以下行（将 username 替换为实际用户名）
   username ALL=(ALL) NOPASSWD: /bin/mount, /bin/umount, /sbin/hdparm
   ```

### 运行应用

```bash
# 克隆仓库
git clone https://github.com/KOPElan/qingfeng.git
cd qingfeng

# 还原依赖
dotnet restore

# 运行应用
dotnet run

# 或指定端口
dotnet run --urls "http://0.0.0.0:5000"
```

应用启动后，访问 `http://localhost:5000` 查看主页。

### 构建

```bash
dotnet build
```

### 发布

```bash
dotnet publish -c Release -o ./publish
```

## 项目结构

```
qingfeng/
├── Components/           # Blazor组件
│   ├── Layout/          # 布局组件
│   └── Pages/           # 页面组件
├── Models/              # 数据模型
├── Services/            # 业务服务
│   ├── SystemMonitorService.cs      # 系统监控服务
│   ├── DiskManagementService.cs     # 磁盘管理服务
│   ├── DockerService.cs             # Docker管理服务
│   └── FileManagerService.cs        # 文件管理服务
├── wwwroot/             # 静态资源
└── Program.cs           # 应用入口
```

## 配置说明

### 数据库配置

应用使用 SQLite 数据库存储主页配置和快捷方式。数据库文件默认位于应用根目录下的 `qingfeng.db`。

可以通过在 `appsettings.json` 中配置连接字符串来自定义数据库位置：

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=/path/to/your/qingfeng.db"
  }
}
```

首次启动时，应用会自动创建数据库并初始化默认快捷方式。如果之前使用了浏览器 localStorage 存储的数据，应用会自动迁移到数据库中。

### Docker连接

默认情况下，应用会尝试连接到以下Docker socket：
- Linux: `unix:///var/run/docker.sock`
- Windows: `npipe://./pipe/docker_engine`

可以通过设置环境变量 `DOCKER_HOST` 来指定自定义Docker连接地址。

### 文件管理器权限

出于安全考虑，文件管理器默认只允许访问：
- Windows: 用户目录及其子目录
- Linux: 根目录及其子目录

### 磁盘管理功能说明

磁盘管理功能仅在 Linux 系统上可用，提供以下功能：

1. **查看所有磁盘**：使用 `lsblk` 命令列出所有块设备，包括未挂载的磁盘和分区
2. **临时挂载**：将磁盘挂载到指定挂载点，重启后失效
3. **永久挂载**：将磁盘挂载并自动写入 `/etc/fstab` 文件，重启后自动挂载
4. **网络磁盘管理**：
   - **CIFS/SMB**：挂载Windows网络共享，支持用户名密码认证
   - **NFS**：挂载Linux/Unix网络共享
   - 支持临时和永久挂载
   - 自动检测已挂载的网络磁盘
5. **磁盘电源管理**：
   - **休眠超时**：设置磁盘自动休眠时间（0-240分钟），通过更新 `/etc/hdparm.conf` 实现永久生效
   - **APM 级别**：设置高级电源管理级别（1=最省电，255=最高性能），通过更新 `/etc/hdparm.conf` 实现永久生效
   - **电源状态查询**：使用 `hdparm -C` 查看磁盘当前电源状态
   - 设置会立即应用并写入配置文件，重启后自动生效

**安装必需工具**（Ubuntu/Debian）：
```bash
sudo apt-get update
sudo apt-get install util-linux hdparm cifs-utils nfs-common
```

**安装必需工具**（CentOS/RHEL/Fedora）：
```bash
sudo yum install util-linux hdparm cifs-utils nfs-utils
# 或
sudo dnf install util-linux hdparm cifs-utils nfs-utils
```

## 安全注意事项

1. 此应用设计用于个人家庭服务器环境
2. 建议在内网环境中使用
3. 如需公网访问，请配置适当的身份验证和加密
4. 磁盘挂载功能需要相应的系统权限

## 许可证

本项目采用 GPL-3.0 许可证。详见 [LICENSE](LICENSE) 文件。
