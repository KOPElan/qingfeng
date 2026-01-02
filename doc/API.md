# QingFeng API 文档

QingFeng 提供了完整的 RESTful API 接口，用于移动端和第三方应用调用。

## API 基础信息

- 基础 URL: `http://your-server:port/api`
- 响应格式: JSON
- 字符编码: UTF-8

## 系统监控 API (System Monitor)

### 获取系统资源信息
```
GET /api/system/resources
```
返回 CPU、内存、磁盘、网络的综合信息。

### 获取 CPU 信息
```
GET /api/system/cpu
```
响应示例:
```json
{
  "usagePercent": 25.5,
  "coreCount": 4,
  "processorName": "X64"
}
```

### 获取内存信息
```
GET /api/system/memory
```
响应示例:
```json
{
  "totalBytes": 16772579328,
  "usedBytes": 2697035776,
  "availableBytes": 14075543552,
  "usagePercent": 16.08,
  "totalGB": "15.62 GB",
  "usedGB": "2.51 GB",
  "availableGB": "13.11 GB"
}
```

### 获取磁盘信息
```
GET /api/system/disks
```

### 获取网络信息
```
GET /api/system/network
```

## Docker API

### 检查 Docker 可用性
```
GET /api/docker/available
```
响应示例:
```json
{
  "available": true
}
```

### 获取容器列表
```
GET /api/docker/containers?showAll=true
```
参数:
- `showAll` (可选): 是否显示所有容器，包括停止的容器。默认为 true。

### 获取镜像列表
```
GET /api/docker/images
```

### 启动容器
```
POST /api/docker/containers/{containerId}/start
```

### 停止容器
```
POST /api/docker/containers/{containerId}/stop
```

### 重启容器
```
POST /api/docker/containers/{containerId}/restart
```

### 删除容器
```
DELETE /api/docker/containers/{containerId}
```

### 获取容器日志
```
GET /api/docker/containers/{containerId}/logs?tailLines=1000
```
参数:
- `tailLines` (可选): 返回最后 N 行日志。默认为 1000。

## 文件管理 API (File Manager)

### 获取文件列表
```
GET /api/files?path=/home/user
```
参数:
- `path` (必需): 目录路径

### 获取驱动器列表
```
GET /api/files/drives
```

### 获取存储信息
```
GET /api/files/storage-info?path=/home/user
```

### 创建目录
```
POST /api/files/directory
Content-Type: application/json

{
  "path": "/home/user/newdir"
}
```

### 删除文件或目录
```
DELETE /api/files?path=/path/to/file&isDirectory=false
```
参数:
- `path` (必需): 文件或目录路径
- `isDirectory` (必需): 是否为目录

### 重命名文件或目录
```
POST /api/files/rename
Content-Type: application/json

{
  "oldPath": "/home/user/oldname",
  "newPath": "/home/user/newname"
}
```

### 复制文件或目录
```
POST /api/files/copy
Content-Type: application/json

{
  "sourcePath": "/home/user/source",
  "destinationPath": "/home/user/dest"
}
```

### 移动文件或目录
```
POST /api/files/move
Content-Type: application/json

{
  "sourcePath": "/home/user/source",
  "destinationPath": "/home/user/dest"
}
```

### 搜索文件
```
POST /api/files/search
Content-Type: application/json

{
  "path": "/home/user",
  "searchPattern": "*.txt",
  "maxResults": 1000,
  "maxDepth": 10
}
```

### 批量操作
```
POST /api/files/batch/copy
POST /api/files/batch/move
POST /api/files/batch/delete
```

### 收藏夹管理
```
GET /api/files/favorites
POST /api/files/favorites
PUT /api/files/favorites/{id}
DELETE /api/files/favorites/{id}
```

### 文件上传
```
POST /api/files/upload
Content-Type: multipart/form-data
```
支持大文件分片上传。

### 文件下载
```
GET /api/files/download?path=/path/to/file
```

## 磁盘管理 API (Disk Management)

### 获取所有磁盘
```
GET /api/disks
```

### 获取块设备列表
```
GET /api/disks/block-devices
```

### 获取磁盘信息
```
GET /api/disks/{devicePath}
```

### 挂载磁盘
```
POST /api/disks/mount
Content-Type: application/json

{
  "devicePath": "/dev/sdb1",
  "mountPoint": "/mnt/disk1",
  "fileSystem": "ext4",
  "options": null
}
```

### 永久挂载磁盘
```
POST /api/disks/mount-permanent
```

### 卸载磁盘
```
POST /api/disks/unmount
Content-Type: application/json

{
  "mountPoint": "/mnt/disk1"
}
```

### 网络磁盘管理
```
GET /api/disks/network
POST /api/disks/network/mount
POST /api/disks/network/mount-permanent
```

### 电源管理
```
POST /api/disks/{devicePath}/spindown
POST /api/disks/{devicePath}/apm
GET /api/disks/{devicePath}/power-status
GET /api/disks/{devicePath}/power-settings
```

### 功能检测
```
GET /api/disks/features
```

## 共享管理 API (Share Management)

### 获取所有共享
```
GET /api/shares
GET /api/shares/cifs
GET /api/shares/nfs
```

### 添加共享
```
POST /api/shares/cifs
POST /api/shares/nfs
```

### 更新共享
```
PUT /api/shares/cifs/{shareName}
PUT /api/shares/nfs/{exportPath}
```

### 删除共享
```
DELETE /api/shares/cifs/{shareName}
DELETE /api/shares/nfs/{exportPath}
```

### 服务管理
```
POST /api/shares/cifs/restart
POST /api/shares/nfs/restart
POST /api/shares/cifs/test-config
```

### Samba 用户管理
```
GET /api/shares/samba-users
POST /api/shares/samba-users
PUT /api/shares/samba-users/{username}
DELETE /api/shares/samba-users/{username}
```

## 认证 API (Authentication)

### 用户登录
```
POST /api/auth/login
Content-Type: application/json

{
  "username": "admin",
  "password": "password"
}
```

### 用户登出
```
POST /api/auth/logout
```

### 获取当前用户
```
GET /api/auth/current
```

### 用户管理 (需要管理员权限)
```
GET /api/auth/users
POST /api/auth/users
DELETE /api/auth/users/{userId}
```

## 终端 API (Terminal)

### 创建终端会话
```
POST /api/terminal/sessions
```
响应:
```json
{
  "sessionId": "abc123",
  "message": "终端会话已创建"
}
```

### 写入终端输入
```
POST /api/terminal/sessions/{sessionId}/input
Content-Type: application/json

{
  "input": "ls -la\n"
}
```

### 读取终端输出
```
GET /api/terminal/sessions/{sessionId}/output
```

### 调整终端大小
```
POST /api/terminal/sessions/{sessionId}/resize
Content-Type: application/json

{
  "rows": 24,
  "cols": 80
}
```

### 关闭终端会话
```
DELETE /api/terminal/sessions/{sessionId}
```

## Anydrop API

### 获取消息列表
```
GET /api/anydrop/messages?pageSize=20&beforeMessageId=100
```

### 获取消息详情
```
GET /api/anydrop/messages/{messageId}
```

### 创建消息
```
POST /api/anydrop/messages
Content-Type: application/json

{
  "content": "Hello World",
  "messageType": "Text"
}
```

### 添加附件
```
POST /api/anydrop/messages/{messageId}/attachments
Content-Type: multipart/form-data
```

### 搜索消息
```
GET /api/anydrop/messages/search?searchTerm=keyword
```

### 删除消息
```
DELETE /api/anydrop/messages/{messageId}
```

### 获取消息总数
```
GET /api/anydrop/messages/count
```

### 下载附件
```
GET /api/anydrop/attachment/{attachmentId}/download
GET /api/anydrop/attachment/{attachmentId}/preview
```

## 应用管理 API (Applications)

### 获取所有应用
```
GET /api/applications
```

### 获取应用详情
```
GET /api/applications/{appId}
```

### 保存应用
```
POST /api/applications
```

### 删除应用
```
DELETE /api/applications/{appId}
```

### 切换固定到 Dock
```
POST /api/applications/{appId}/toggle-pin
```

## Dock 项管理 API

### 获取所有 Dock 项
```
GET /api/dock
```

### 获取 Dock 项详情
```
GET /api/dock/{itemId}
```

### 保存 Dock 项
```
POST /api/dock
```

### 删除 Dock 项
```
DELETE /api/dock/{itemId}
```

## 计划任务 API (Scheduled Tasks)

### 获取所有任务
```
GET /api/tasks
```

### 获取任务详情
```
GET /api/tasks/{id}
```

### 创建任务
```
POST /api/tasks
```

### 更新任务
```
PUT /api/tasks/{id}
```

### 删除任务
```
DELETE /api/tasks/{id}
```

### 启用/禁用任务
```
POST /api/tasks/{id}/enable
Content-Type: application/json

{
  "enabled": true
}
```

### 立即运行任务
```
POST /api/tasks/{id}/run
```

## 系统设置 API (System Settings)

### 获取所有设置
```
GET /api/settings
```

### 获取设置值
```
GET /api/settings/{key}
```

### 按分类获取设置
```
GET /api/settings/category/{category}
```

### 设置值
```
POST /api/settings
Content-Type: application/json

{
  "key": "theme",
  "value": "dark",
  "category": "appearance",
  "description": "应用主题"
}
```

## 错误处理

所有 API 在发生错误时会返回适当的 HTTP 状态码和错误信息：

- `200 OK`: 请求成功
- `400 Bad Request`: 请求参数错误
- `401 Unauthorized`: 未授权，需要登录
- `403 Forbidden`: 禁止访问，权限不足
- `404 Not Found`: 资源不存在
- `500 Internal Server Error`: 服务器内部错误

错误响应格式:
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "错误标题",
  "status": 400,
  "detail": "详细错误信息"
}
```

## 注意事项

1. **安全性**: 
   - 文件上传和下载接口当前已禁用反 CSRF 保护以支持外部 API 客户端。
   - 在生产环境中，强烈建议实现以下安全措施：
     - 使用 API 密钥或 JWT 令牌进行身份验证
     - 启用 HTTPS 加密传输
     - 实施速率限制防止滥用
     - 添加文件类型白名单和大小限制
2. **权限管理**: 
   - 部分接口需要管理员权限才能访问（如用户管理、系统设置等）。
   - 所有路径参数都经过服务层验证，防止路径遍历攻击。
3. **参数编码**: 
   - 所有路径参数和查询参数应进行 URL 编码。
   - 特殊字符需要正确转义。
4. **文件上传**: 
   - 大文件上传支持分片上传，使用 Dropzone.js 的分片格式。
   - 单个文件最大 2GB。
   - 建议在生产环境中添加病毒扫描。
5. **响应格式**: 
   - 所有响应均为 JSON 格式，除非另有说明（如文件下载）。
   - 时间格式使用 ISO 8601 标准。

## 最佳实践

1. **错误处理**: 始终检查 HTTP 状态码和响应内容。
2. **连接池**: 使用 HTTP 连接池复用连接以提高性能。
3. **超时设置**: 为长时间运行的操作（如文件上传、Docker 操作）设置适当的超时。
4. **重试机制**: 对于临时性失败，实施指数退避重试策略。
5. **日志记录**: 记录所有 API 调用以便调试和审计。
