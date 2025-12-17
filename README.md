# 清风 (QingFeng)

一款基于.NET平台C#语言开发的个人家庭服务器主页，使用Blazor技术栈实现。

与AI（github copilot）协同实现

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

- **磁盘管理**：管理磁盘挂载和共享
  - 查看所有磁盘信息
  - 挂载/卸载磁盘（Linux）
  - 查看Samba共享配置

- **Docker管理**：管理Docker容器和镜像
  - 查看容器列表
  - 启动/停止/重启/删除容器
  - 查看镜像列表
  - 从 Docker 容器快速创建快捷方式

- **文件管理器**：浏览和管理文件系统
  - 浏览目录结构
  - 创建文件夹
  - 删除文件和文件夹

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

## 安全注意事项

1. 此应用设计用于个人家庭服务器环境
2. 建议在内网环境中使用
3. 如需公网访问，请配置适当的身份验证和加密
4. 磁盘挂载功能需要相应的系统权限

## 许可证

本项目采用 GPL-3.0 许可证。详见 [LICENSE](LICENSE) 文件。
