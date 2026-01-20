# 通知服务使用指南

## 概述
通知服务允许任何功能在应用程序中创建和管理通知。用户可以在顶部栏的通知按钮中查看所有通知。

## 在组件中使用

### 1. 注入服务
```csharp
@inject INotificationService NotificationService
```

### 2. 创建通知

#### 信息通知
```csharp
await NotificationService.CreateNotificationAsync(
    "信息标题",
    "这是通知的详细内容",
    "Info"
);
```

#### 成功通知
```csharp
await NotificationService.CreateNotificationAsync(
    "操作成功",
    "文件已成功上传",
    "Success"
);
```

#### 警告通知
```csharp
await NotificationService.CreateNotificationAsync(
    "警告",
    "磁盘空间不足，请及时清理",
    "Warning"
);
```

#### 错误通知
```csharp
await NotificationService.CreateNotificationAsync(
    "错误",
    "无法连接到服务器",
    "Error"
);
```

#### 带操作链接的通知
```csharp
await NotificationService.CreateNotificationAsync(
    "任务完成",
    "备份任务已完成，点击查看详情",
    "Success",
    "/scheduled-tasks",  // 点击"查看详情"时导航到的URL
    "✅"                 // 可选：自定义图标
);
```

## 在服务中使用

```csharp
public class MyService
{
    private readonly INotificationService _notificationService;

    public MyService(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task DoSomethingAsync()
    {
        try
        {
            // 执行操作
            // ...

            // 成功后发送通知
            await _notificationService.CreateNotificationAsync(
                "操作完成",
                "操作已成功完成",
                "Success"
            );
        }
        catch (Exception ex)
        {
            // 失败时发送错误通知
            await _notificationService.CreateNotificationAsync(
                "操作失败",
                $"操作失败: {ex.Message}",
                "Error"
            );
        }
    }
}
```

## 通知类型说明

| 类型 | 图标 | 边框颜色 | 使用场景 |
|------|------|----------|----------|
| Info | ℹ️ | 蓝色 | 一般信息提示 |
| Success | ✅ | 绿色 | 操作成功 |
| Warning | ⚠️ | 黄色 | 警告信息 |
| Error | ❌ | 红色 | 错误信息 |

## 高级功能

### 获取未读通知数量
```csharp
var count = await NotificationService.GetUnreadCountAsync();
```

### 获取所有未读通知
```csharp
var unreadNotifications = await NotificationService.GetUnreadNotificationsAsync();
```

### 标记通知为已读
```csharp
await NotificationService.MarkAsReadAsync(notificationId);
```

### 删除通知
```csharp
await NotificationService.DeleteNotificationAsync(notificationId);
```

## 实时更新

通知服务支持实时更新。当创建、标记已读或删除通知时，UI会自动更新：

```csharp
protected override async Task OnInitializedAsync()
{
    // 订阅通知变更事件
    NotificationService.OnNotificationChanged += OnNotificationChanged;
}

private async Task OnNotificationChanged()
{
    // 通知发生变化时的处理
    await InvokeAsync(StateHasChanged);
}

public void Dispose()
{
    // 取消订阅
    NotificationService.OnNotificationChanged -= OnNotificationChanged;
}
```

## 最佳实践

1. **使用合适的通知类型**：根据消息的重要性和性质选择正确的类型
2. **保持消息简洁**：标题应简短明了，内容应简洁清晰
3. **避免过度通知**：只在重要的操作完成或错误发生时发送通知
4. **提供操作链接**：对于需要用户后续操作的通知，提供相应的链接
5. **自定义图标**：可以使用 emoji 或其他图标来增强通知的可读性

## 示例场景

### 文件上传
```csharp
// 上传成功
await NotificationService.CreateNotificationAsync(
    "文件上传成功",
    $"文件 {fileName} 已成功上传",
    "Success",
    "/file-manager"
);
```

### 定时任务
```csharp
// 任务执行完成
await NotificationService.CreateNotificationAsync(
    "定时任务执行完成",
    $"任务 {taskName} 已成功执行",
    "Success",
    "/scheduled-tasks"
);
```

### 系统监控
```csharp
// 磁盘空间警告
await NotificationService.CreateNotificationAsync(
    "磁盘空间警告",
    "磁盘使用率已超过 80%，请及时清理",
    "Warning",
    "/system-monitor"
);
```
