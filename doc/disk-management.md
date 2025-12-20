# 磁盘管理功能说明

## 概述

磁盘管理功能提供了完整的Linux磁盘管理解决方案，允许您查看、挂载、卸载本地磁盘和网络磁盘，并管理磁盘电源设置。

## 功能特性

### 1. 查看所有磁盘设备

- **显示所有块设备**：包括已挂载和未挂载的磁盘和分区
- **层次结构显示**：清晰展示磁盘及其分区的关系
- **详细信息**：
  - 设备路径（如 /dev/sda, /dev/sdb1）
  - 设备类型（disk, part, loop等）
  - 磁盘大小
  - 文件系统类型（ext4, ntfs, vfat等）
  - 挂载点
  - 使用率（已挂载磁盘）
  - UUID（用于永久挂载）
  - 标签

### 2. 磁盘挂载向导

#### 临时挂载
- 将磁盘挂载到指定挂载点
- 重启后自动失效
- 适用于临时访问外部存储

#### 永久挂载
- 挂载磁盘并自动写入 `/etc/fstab`
- 系统重启后自动挂载
- 使用 UUID 确保磁盘识别的准确性

#### 挂载选项
- **文件系统类型**：支持 ext4, ext3, ext2, btrfs, xfs, ntfs, vfat, exfat 等
- **挂载选项**：可自定义挂载选项，如：
  - `defaults`：默认选项
  - `rw`：读写模式
  - `ro`：只读模式
  - `noatime`：不更新访问时间（提高性能）
  - `user`：允许普通用户挂载

### 3. 磁盘卸载

- 一键卸载已挂载的磁盘
- 自动验证卸载是否成功
- 提供详细的错误信息

### 4. 网络磁盘管理

#### 网络磁盘类型支持
- **CIFS/SMB**：Windows网络共享
  - 支持用户名/密码认证
  - 支持域认证
  - 自动创建安全的凭据文件（永久挂载）
- **NFS**：Linux/Unix网络共享
  - 支持NFS v3和v4
  - 支持自定义挂载选项

#### 查看网络磁盘
- 自动检测已挂载的网络磁盘
- 显示服务器地址和共享路径
- 显示磁盘使用率
- 区分CIFS和NFS类型

#### 网络磁盘挂载向导
- **CIFS/SMB 挂载**：
  - 服务器地址（IP或主机名）
  - 共享名称
  - 用户名和密码（可选）
  - 域名（可选）
  - 自定义挂载选项
- **NFS 挂载**：
  - 服务器地址
  - 导出路径
  - 自定义挂载选项

#### 临时和永久挂载
- **临时挂载**：仅当前会话有效，重启后失效
- **永久挂载**：
  - CIFS：自动创建凭据文件并写入 `/etc/fstab`
  - NFS：直接写入 `/etc/fstab`
  - 系统重启后自动挂载

### 5. 磁盘电源管理

#### 休眠超时设置
- 设置磁盘自动休眠时间（0-240分钟）
- 0 表示禁用自动休眠
- 使用 `hdparm -S` 命令实现

#### APM（高级电源管理）级别
- 设置磁盘电源管理级别（1-255）
- 1 = 最省电（更频繁休眠）
- 255 = 最高性能（不休眠）
- 使用 `hdparm -B` 命令实现

#### 电源状态查询
- 查看磁盘当前电源状态
- 显示磁盘是否处于活动、待机或休眠状态
- 使用 `hdparm -C` 命令实现

## 系统要求

### 必需工具

1. **lsblk** (util-linux 包)
   - 用于列出所有块设备
   - 通常Linux系统默认已安装

2. **mount / umount**
   - 用于挂载和卸载文件系统
   - 通常Linux系统默认已安装

3. **hdparm** (可选，用于电源管理)
   - 用于磁盘电源管理和性能调优
   - 需要单独安装

4. **cifs-utils** (可选，用于CIFS/SMB网络磁盘)
   - 用于挂载Windows网络共享
   - 需要单独安装

5. **nfs-common** (可选，用于NFS网络磁盘)
   - 用于挂载NFS网络共享
   - 需要单独安装

### 安装命令

#### Ubuntu/Debian
```bash
sudo apt-get update
sudo apt-get install util-linux hdparm cifs-utils nfs-common
```

#### CentOS/RHEL/Fedora
```bash
sudo yum install util-linux hdparm cifs-utils nfs-utils
# 或
sudo dnf install util-linux hdparm cifs-utils nfs-utils
```

#### Arch Linux
```bash
sudo pacman -S util-linux hdparm cifs-utils nfs-utils
```

### 权限要求

磁盘挂载、卸载和电源管理功能需要 root 权限。推荐配置方式：

#### 方式1：使用 sudo 运行应用
```bash
sudo dotnet run --urls "http://0.0.0.0:5000"
```

#### 方式2：配置 sudoers 免密码执行（推荐）
```bash
# 编辑 sudoers 文件
sudo visudo

# 添加以下行（将 username 替换为实际用户名）
username ALL=(ALL) NOPASSWD: /bin/mount, /bin/umount, /sbin/hdparm
```

这样配置后，应用可以无需密码执行特定的磁盘管理命令。

## 使用示例

### 临时挂载USB磁盘

1. 访问"磁盘管理"页面
2. 找到您的USB设备（通常是 `/dev/sdc1` 或类似）
3. 点击"挂载"按钮
4. 在挂载向导中：
   - 设备路径：`/dev/sdc1`
   - 挂载点：`/mnt/usb`
   - 文件系统：选择"vfat"或"ntfs"（根据USB格式）
   - 挂载类型：选择"临时挂载"
5. 点击"挂载"按钮

### 永久挂载数据盘

1. 访问"磁盘管理"页面
2. 找到您的数据盘分区（例如 `/dev/sdb1`）
3. 点击"挂载"按钮
4. 在挂载向导中：
   - 设备路径：`/dev/sdb1`
   - 挂载点：`/data`
   - 文件系统：选择"ext4"
   - 挂载选项：`defaults,noatime`
   - 挂载类型：选择"永久挂载"
5. 点击"挂载"按钮

系统会自动将配置写入 `/etc/fstab`，重启后自动挂载。

### 挂载Windows共享（CIFS/SMB）

1. 访问"磁盘管理"页面
2. 在"网络磁盘"区域点击"挂载网络磁盘"按钮
3. 在网络磁盘挂载向导中：
   - 网络磁盘类型：选择"CIFS/SMB (Windows共享)"
   - 服务器地址：`192.168.1.100` 或 `nas.local`
   - 共享路径：`share` 或 `Documents`
   - 挂载点：`/mnt/windows-share`
   - 用户名：`your_username`（可选）
   - 密码：`your_password`（可选）
   - 域：`WORKGROUP`（可选）
   - 挂载类型：选择"临时挂载"或"永久挂载"
4. 点击"挂载"按钮

### 挂载NFS共享

1. 访问"磁盘管理"页面
2. 在"网络磁盘"区域点击"挂载网络磁盘"按钮
3. 在网络磁盘挂载向导中：
   - 网络磁盘类型：选择"NFS (Linux共享)"
   - 服务器地址：`192.168.1.200` 或 `fileserver.local`
   - 共享路径：`export/data` 或 `mnt/storage`
   - 挂载点：`/mnt/nfs-share`
   - 挂载选项：`rw,sync` 或保持默认
   - 挂载类型：选择"临时挂载"或"永久挂载"
4. 点击"挂载"按钮

注意：
- CIFS永久挂载会自动创建安全的凭据文件存储密码
- NFS挂载不需要用户名密码，依赖服务器端的权限配置

### 设置磁盘休眠

1. 访问"磁盘管理"页面
2. 找到目标磁盘（例如 `/dev/sdb`）
3. 点击"电源"按钮
4. 在电源管理对话框中：
   - 设置休眠超时：例如 10 分钟
   - 点击"设置休眠超时"
5. （可选）设置 APM 级别：
   - APM 级别：128（平衡）
   - 点击"设置 APM 级别"

## 安全注意事项

1. **数据备份**：挂载前请确保数据已备份
2. **卸载前检查**：确保没有程序正在使用磁盘
3. **权限控制**：仅授予必要的 sudo 权限
4. **内网使用**：本功能建议仅在内网环境使用
5. **验证设备**：挂载前请仔细确认设备路径，避免操作错误的磁盘
6. **网络凭据安全**：
   - 永久挂载CIFS时，凭据会保存在 `/etc/cifs-credentials-*` 文件中
   - 凭据文件权限设置为 600（仅root可读写）
   - 建议使用只读账户挂载共享以降低安全风险

## 故障排除

### 挂载失败

**错误：Permission denied**
- 解决：使用 sudo 运行应用或配置 sudoers

**错误：mount point does not exist**
- 解决：系统会自动创建挂载点，如果失败请手动创建：`sudo mkdir -p /mnt/mydisk`

**错误：device is already mounted**
- 解决：磁盘已挂载，先卸载再重新挂载

**错误：unknown filesystem type**
- 解决：安装相应的文件系统支持包，例如：
  - NTFS: `sudo apt-get install ntfs-3g`
  - exFAT: `sudo apt-get install exfat-fuse exfat-utils`

### 网络磁盘挂载失败

**错误：mount error(13): Permission denied**
- CIFS：用户名或密码错误，或服务器拒绝访问
- NFS：服务器端未正确配置导出权限
- 解决：检查认证凭据和服务器配置

**错误：mount.cifs: command not found**
- 解决：安装 cifs-utils：`sudo apt-get install cifs-utils`

**错误：mount.nfs: command not found**
- 解决：安装 nfs-common：`sudo apt-get install nfs-common`

**错误：No route to host**
- 解决：检查网络连接和服务器地址是否正确
- 测试连接：`ping <server_address>`

**错误：mount error(112): Host is down**
- 解决：目标服务器未运行或防火墙阻止连接
- CIFS：确保服务器的445端口开放
- NFS：确保服务器的2049端口开放

**错误：mount error(115): Operation now in progress**
- 解决：网络延迟过高，尝试添加超时选项
- CIFS：添加选项 `timeout=60`
- NFS：添加选项 `timeo=600`

### 卸载失败

**错误：target is busy**
- 解决：有程序正在使用磁盘，关闭相关程序后再卸载
- 查看使用磁盘的进程：`sudo lsof /mnt/mydisk`
- 强制卸载：`sudo umount -l /mnt/mydisk`（不推荐）

### 电源管理失败

**错误：hdparm: command not found**
- 解决：安装 hdparm：`sudo apt-get install hdparm`

**错误：Permission denied**
- 解决：需要 root 权限执行 hdparm

**错误：HDIO_DRIVE_CMD failed: Input/output error**
- 原因：某些磁盘（如SSD或虚拟磁盘）不支持电源管理
- 解决：这是正常现象，无需担心

## 技术实现

### 使用的Linux命令

1. **lsblk -J -b**：以JSON格式列出所有块设备
2. **mount**：挂载文件系统
3. **umount**：卸载文件系统
4. **hdparm -S**：设置休眠超时
5. **hdparm -B**：设置APM级别
6. **hdparm -C**：查询电源状态

### 安全措施

1. **输入验证**：所有用户输入都经过严格验证
2. **命令注入防护**：过滤特殊字符，防止命令注入攻击
3. **路径验证**：
   - 设备路径必须以 `/dev/` 开头
   - 挂载点必须是绝对路径
4. **文件操作**：使用 .NET 的 File API 而非 shell 命令

## 相关文档

- [README.md](../README.md) - 项目总体说明
- [Linux man pages](https://man7.org/)
  - [mount(8)](https://man7.org/linux/man-pages/man8/mount.8.html)
  - [umount(8)](https://man7.org/linux/man-pages/man8/umount.8.html)
  - [hdparm(8)](https://man7.org/linux/man-pages/man8/hdparm.8.html)
  - [lsblk(8)](https://man7.org/linux/man-pages/man8/lsblk.8.html)
