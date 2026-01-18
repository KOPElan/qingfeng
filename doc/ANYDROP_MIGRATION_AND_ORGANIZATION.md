# Anydrop 文件迁移和组织改进

## 概述

本文档描述了对 Anydrop (云笈) 文件存储系统的改进，解决了路径迁移兼容性问题并优化了文件组织方式。

## 问题描述

### 1. 迁移兼容性问题
在更改 Anydrop 存储路径设置后，先前上传的文件无法访问。原因是：
- 数据库中存储的是**绝对路径**（如 `/path/to/old/storage/file.jpg`）
- 迁移任务移动文件后，没有更新数据库中的路径
- 导致应用仍然尝试从旧路径读取文件，造成"文件未找到"错误

### 2. 文件组织问题
所有附件文件直接存储在根目录下，格式为 `{messageId}_{guid}.ext`：
- 文件数量增多后难以管理
- 缺乏按时间的自然组织结构
- 备份和浏览困难

## 解决方案

### 1. 相对路径存储
**变更内容**：
- 新上传的文件在数据库中存储**相对路径**而非绝对路径
- 相对路径相对于配置的 `anydropStoragePath`
- 例如：`2026/01/18/123_abc-def.jpg` 而非 `/var/www/AnydropFiles/2026/01/18/123_abc-def.jpg`

**优势**：
- 迁移存储路径时，只需更新配置，数据库无需修改
- 更好的可移植性，适合备份和恢复
- 支持在不同环境（开发/生产）间移动

### 2. 按日期组织文件
**变更内容**：
- 文件存储在按日期划分的子目录中：`YYYY/MM/DD/`
- 例如：2026年1月18日上传的文件存储在 `2026/01/18/` 目录下
- 完整路径示例：`{storagePath}/2026/01/18/{messageId}_{guid}.jpg`

**优势**：
- 自然的时间组织结构
- 易于按日期浏览和管理文件
- 文件系统性能更好（避免单目录下文件过多）
- 便于实施基于时间的备份策略

### 3. 向后兼容性
**实现细节**：
- `GetAbsoluteFilePath()` 辅助方法处理新旧格式
- 自动检测路径是绝对还是相对
- 绝对路径：直接使用
- 相对路径：与 `_anydropStoragePath` 组合

**代码示例**：
```csharp
private string GetAbsoluteFilePath(string filePath)
{
    // 如果已是绝对路径，直接返回
    if (Path.IsPathRooted(filePath))
    {
        return filePath;
    }
    
    // 如果是相对路径，与存储路径组合
    return Path.Combine(_anydropStoragePath, filePath);
}
```

### 4. 增强的迁移任务
**改进内容**：
1. 移动文件到新位置
2. **更新数据库记录**为相对路径
3. 清理空目录
4. 记录详细日志

**迁移流程**：
```
1. 移动文件：源目录 -> 目标目录（保持子目录结构）
2. 遍历数据库中的所有附件记录
3. 对于每个附件：
   - 如果文件路径是绝对路径且在旧源目录下
   - 计算文件在新目录中的位置
   - 验证文件已成功移动
   - 更新数据库记录为相对路径
4. 保存数据库更改
5. 清理源目录
```

## 实施的代码变更

### AnydropService.cs
1. **AddAttachmentAsync**：使用日期子目录和相对路径
2. **UploadAttachmentFileAsync**：使用日期子目录和相对路径
3. **DownloadAttachmentAsync**：使用 `GetAbsoluteFilePath()` 处理两种路径格式
4. **DeleteMessageAsync**：使用 `GetAbsoluteFilePath()` 处理两种路径格式
5. **GetAbsoluteFilePath()**：新增辅助方法
6. **ConvertAbsolutePathsToRelativeAsync()**：数据迁移工具方法

### ScheduledTaskExecutorService.cs
**ExecuteAnydropMigrationTaskAsync** 增强：
1. 跟踪文件路径映射
2. 移动文件后更新数据库
3. 转换为相对路径格式
4. 记录更新的数据库记录数

### IAnydropService.cs
新增方法：
- `ConvertAbsolutePathsToRelativeAsync()`：用于现有数据迁移

## 使用指南

### 对于新安装
无需特殊操作，所有新上传的文件将自动：
- 存储在日期子目录中
- 使用相对路径记录在数据库中

### 对于现有安装（已有数据）

#### 方法 1：通过迁移任务（推荐）
1. 在设置页面更改 `anydropStoragePath`
2. 系统会提示创建迁移任务
3. 确认并等待迁移完成
4. 迁移任务会自动：
   - 移动所有文件
   - 更新数据库路径为相对格式

#### 方法 2：手动转换（不改变存储位置）
如果只想转换现有绝对路径为相对路径（不移动文件）：

```csharp
// 通过依赖注入获取 IAnydropService
var count = await anydropService.ConvertAbsolutePathsToRelativeAsync();
Console.WriteLine($"转换了 {count} 条记录");
```

### 迁移后验证
1. 检查文件是否可以正常下载
2. 验证新上传的文件存储在日期子目录中
3. 检查数据库 `AnydropAttachments` 表的 `FilePath` 列是否为相对路径

## 技术细节

### 路径格式对比

**旧格式（绝对路径）**：
```
数据库: /var/www/AnydropFiles/123_abc-def-ghi.jpg
文件系统: /var/www/AnydropFiles/123_abc-def-ghi.jpg
```

**新格式（相对路径 + 日期组织）**：
```
数据库: 2026/01/18/123_abc-def-ghi.jpg
文件系统: /var/www/AnydropFiles/2026/01/18/123_abc-def-ghi.jpg
```

### 文件命名约定
保持不变：`{messageId}_{guid}{extension}`
- `messageId`：消息 ID
- `guid`：唯一标识符（防止冲突）
- `extension`：原始文件扩展名

### 目录结构示例
```
AnydropFiles/
├── 2026/
│   ├── 01/
│   │   ├── 15/
│   │   │   ├── 123_abc-def.jpg
│   │   │   └── 124_xyz-uvw.pdf
│   │   ├── 16/
│   │   │   └── 125_lmn-opq.png
│   │   └── 18/
│   │       ├── 126_rst-wxy.mp4
│   │       └── 127_aaa-bbb.jpg
│   └── 02/
│       └── 01/
│           └── 128_ccc-ddd.doc
└── old_files_before_migration/
    ├── 100_old-file-1.jpg
    └── 101_old-file-2.pdf
```

## 性能影响

### 读取性能
- **无影响**：`GetAbsoluteFilePath()` 是简单的路径组合操作，性能开销可忽略
- 文件系统访问时间与路径格式无关

### 写入性能
- **轻微开销**：需要创建日期子目录（仅首次，后续重用）
- 目录创建检查很快，并且有文件系统缓存

### 迁移性能
- 取决于文件数量和大小
- 使用复制+删除策略确保数据完整性
- 批量更新数据库记录

## 安全考虑

1. **路径遍历防护**：
   - 所有路径操作通过 `Path.Combine()` 和 `Path.GetRelativePath()` 标准化
   - `FileManagerService.IsPathAllowed()` 仍然生效

2. **权限检查**：
   - 迁移任务需要源目录和目标目录的读写权限
   - 失败的操作会记录日志但不影响其他文件

3. **数据完整性**：
   - 使用"复制-验证-删除"模式
   - 仅在验证成功后才删除源文件
   - 数据库更新在事务中完成

## 故障排除

### 问题：迁移后仍然找不到文件
**原因**：
- 迁移任务未成功完成
- 数据库未正确更新

**解决方案**：
1. 检查迁移任务执行历史
2. 验证文件是否在新位置
3. 检查数据库 `FilePath` 是否为相对路径
4. 如有必要，重新运行迁移任务

### 问题：新上传的文件找不到
**原因**：
- 存储路径配置不正确
- 目录权限问题

**解决方案**：
1. 检查系统设置中的 `anydropStoragePath`
2. 验证应用有创建子目录的权限
3. 检查应用日志中的错误信息

### 问题：旧文件可访问，新文件不可访问
**原因**：
- `anydropStoragePath` 配置与实际文件位置不匹配

**解决方案**：
1. 检查配置的存储路径
2. 验证新文件确实在日期子目录中
3. 检查相对路径计算是否正确

## 总结

这些改进解决了 Anydrop 系统的两个主要痛点：
1. ✅ **迁移兼容性**：通过相对路径和增强的迁移任务
2. ✅ **文件组织**：通过按日期的自动分类

系统现在更加健壮、可移植，并且更易于管理和维护。
