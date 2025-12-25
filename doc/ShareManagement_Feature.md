# 共享目录管理功能说明

## 功能概述

新增了一个宿主机共享目录管理功能，可以在网页界面中查看和管理当前主机提供给其他设备的共享目录（CIFS/Samba 和 NFS）。

## 主要功能

### 1. 查看共享

- **CIFS/Samba 共享**：查看从 `/etc/samba/smb.conf` 读取的所有 Samba 共享配置
- **NFS 导出**：查看从 `/etc/exports` 读取的所有 NFS 导出配置
- 显示详细信息包括：
  - 共享名称/路径
  - 本地目录路径
  - 访问权限（只读/读写）
  - 允许的用户/主机
  - 其他选项

### 2. 添加共享

#### CIFS/Samba 共享
- 设置共享名称（网络上显示的名称）
- 选择要共享的本地目录
- 配置选项：
  - 可浏览性
  - 只读/读写权限
  - 允许访客访问
  - 有效用户列表（可指定用户或组）
  - 写入列表（在只读共享中允许写入的用户）
  - 说明文字

#### NFS 导出
- 选择要导出的本地目录
- 配置选项：
  - 允许访问的主机（支持通配符、IP 地址、子网）
  - NFS 选项（如 rw, ro, sync, async, no_subtree_check 等）

### 3. 编辑共享

- 修改现有共享的权限和配置
- 更新用户访问列表
- 调整共享选项

### 4. 删除共享

- 从配置文件中移除共享
- 提供确认对话框防止误删除

### 5. 服务管理

- 重启 Samba 服务以应用配置更改
- 重启 NFS 服务以应用配置更改
- 自动测试 Samba 配置语法

## 技术实现

### 模型 (Models)
- `ShareInfo.cs`: 共享信息模型
  - ShareType 枚举：CIFS 或 NFS
  - ShareInfo 类：包含共享的所有配置信息
  - ShareRequest 类：用于创建/更新共享的请求模型

### 服务 (Services)
- `IShareManagementService.cs`: 服务接口
- `ShareManagementService.cs`: 服务实现
  - 读取和解析 Samba 配置文件
  - 读取和解析 NFS 导出配置
  - 添加/更新/删除共享配置
  - 重启相关服务
  - 配置文件的原子性写入

### 用户界面 (UI)
- `ShareManagement.razor`: 共享管理页面
  - 响应式表格显示共享列表
  - 添加/编辑共享的对话框
  - 删除确认对话框
  - 实时状态反馈

### 导航
- 在 Dock 中添加"共享管理"图标
- 使用 `folder_shared` Material Icons 图标
- 紫色渐变背景色

## 使用前提

### 系统要求
- Linux 操作系统（仅在 Linux 上支持）
- Root 权限（修改配置文件需要）

### CIFS/Samba 支持
需要安装 Samba：
```bash
# Ubuntu/Debian
sudo apt-get install samba samba-common-bin

# CentOS/RHEL/Fedora
sudo yum install samba samba-common
# 或
sudo dnf install samba samba-common
```

### NFS 支持
需要安装 NFS 服务器：
```bash
# Ubuntu/Debian
sudo apt-get install nfs-kernel-server

# CentOS/RHEL/Fedora
sudo yum install nfs-utils
# 或
sudo dnf install nfs-utils
```

## 运行权限

应用需要以 root 权限运行才能修改系统配置文件：

```bash
sudo dotnet run --urls "http://0.0.0.0:5000"
```

或者配置 sudoers 允许特定用户修改配置文件（不推荐，安全性较低）。

## 安全考虑

1. **配置文件备份**：在修改前建议备份配置文件
   ```bash
   sudo cp /etc/samba/smb.conf /etc/samba/smb.conf.backup
   sudo cp /etc/exports /etc/exports.backup
   ```

2. **原子性写入**：所有配置更改使用临时文件和原子移动操作，防止写入时的损坏

3. **输入验证**：
   - 检查路径是否为绝对路径
   - 过滤危险字符
   - 验证用户输入

4. **权限控制**：需要管理员登录才能访问共享管理页面

## 使用示例

### 添加 CIFS 共享
1. 点击 Dock 中的"共享管理"图标
2. 在 CIFS 部分点击"添加 CIFS 共享"
3. 填写信息：
   - 共享名：`documents`
   - 本地路径：`/srv/shares/documents`
   - 说明：`文档共享文件夹`
   - 勾选"可浏览"
   - 取消"只读"（允许写入）
   - 有效用户：`john, mary, @staff`（用户 john、mary 和 staff 组成员）
4. 点击"添加"
5. 点击"重启服务"使配置生效

### 添加 NFS 导出
1. 在 NFS 部分点击"添加 NFS 导出"
2. 填写信息：
   - 本地路径：`/srv/nfs/public`
   - 允许主机：`192.168.1.0/24`
   - NFS 选项：`rw,sync,no_subtree_check`
3. 点击"添加"（自动重新加载导出）

## 故障排除

### Samba 配置语法错误
如果添加共享后出现语法错误，可以手动使用 `testparm` 检查：
```bash
sudo testparm -s
```

### NFS 导出未生效
手动重新加载 NFS 导出：
```bash
sudo exportfs -ra
```

查看当前导出：
```bash
sudo exportfs -v
```

### 权限被拒绝
确保应用以足够权限运行：
```bash
sudo dotnet run --urls "http://0.0.0.0:5000"
```

## 注意事项

1. **生产环境使用**：此功能会直接修改系统配置文件，请在生产环境中谨慎使用
2. **配置备份**：建议定期备份配置文件
3. **服务重启**：修改 CIFS 配置后需要手动重启 Samba 服务
4. **NFS 即时生效**：NFS 导出修改会自动调用 `exportfs -ra` 重新加载
5. **防火墙**：确保防火墙允许 SMB（445）和 NFS（2049）端口
