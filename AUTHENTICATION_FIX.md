# 认证状态持久化修复

## 问题描述
登录后访问 Docker 等需要认证的页面时,会自动跳转到登录页。这是因为 `AuthenticationStateService` 使用 `Scoped` 生命周期,当页面使用 `forceLoad: true` 导航或刷新时,Blazor Server 电路会重置,导致认证状态丢失。

## 解决方案
使用 `ProtectedSessionStorage` 将认证状态持久化到浏览器会话存储中,即使电路重置也能恢复认证状态。

## 修改的文件

### 1. `Services/AuthenticationStateService.cs`
- 添加 `ProtectedSessionStorage` 依赖注入
- 将用户信息持久化到加密的会话存储中
- 实现延迟加载和自动恢复机制
- 所有方法改为异步

### 2. `Services/AuthenticationService.cs`
- 更新 `LoginAsync()` 调用 `SetUserAsync()` 而不是 `SetUser()`
- 更新 `LogoutAsync()` 调用 `ClearAsync()` 而不是 `Clear()`
- 更新 `GetCurrentUserAsync()` 从会话存储获取用户
- 更新 `DeleteUserAsync()` 使用异步清除方法

### 3. `Components/Layout/MainLayout.razor`
- 将 `OnInitialized()` 改为 `OnInitializedAsync()`
- 创建 `UpdateCurrentUserAsync()` 异步方法
- 更新 `OnLocationChanged` 使用异步方法

## 功能特性

? **会话持久化**: 用户登录状态在会话期间保持,即使页面刷新或电路重置
? **安全存储**: 使用 ASP.NET Core 的 `ProtectedSessionStorage` 加密存储用户信息
? **自动恢复**: 页面加载时自动从会话存储恢复认证状态
? **向后兼容**: 不影响现有的登录/登出逻辑
? **支持强制刷新**: `forceLoad: true` 导航不会丢失认证状态

## 测试步骤

### 测试 1: 基本登录功能
1. 启动应用程序
2. 访问登录页面
3. 使用有效凭据登录
4. 验证成功跳转到主页
5. **预期结果**: 用户成功登录,显示用户名

### 测试 2: 页面导航保持登录状态
1. 登录后,点击导航到 Docker 页面
2. 等待页面加载完成
3. **预期结果**: Docker 页面正常显示,不会跳转到登录页

### 测试 3: 页面刷新保持登录状态
1. 登录后,访问任意需要认证的页面(如 Docker、SystemMonitor)
2. 按 F5 或点击浏览器刷新按钮
3. **预期结果**: 页面重新加载后仍然显示内容,用户保持登录状态

### 测试 4: 语言切换保持登录状态
1. 登录后,访问设置页面
2. 切换语言(这会触发 `forceLoad: true` 导航)
3. **预期结果**: 页面刷新,语言切换成功,用户仍然保持登录状态

### 测试 5: 登出功能
1. 登录后,点击登出按钮
2. **预期结果**: 成功跳转到登录页,会话存储被清除

### 测试 6: 多标签页测试
1. 登录后,在新标签页中打开同一应用
2. 在新标签页访问需要认证的页面
3. **预期结果**: 新标签页也自动识别登录状态(会话存储在同一浏览器会话中共享)

### 测试 7: 关闭浏览器后
1. 登录后,完全关闭浏览器
2. 重新打开浏览器,访问应用
3. **预期结果**: 需要重新登录(SessionStorage 在浏览器关闭后清除)

## 技术细节

### ProtectedSessionStorage vs ProtectedLocalStorage

我们选择 `ProtectedSessionStorage` 而不是 `ProtectedLocalStorage`:

- ? **SessionStorage**: 标签页/窗口关闭后自动清除,更安全
- ? **LocalStorage**: 持久化到磁盘,除非手动清除否则永久保存

### 安全考虑

1. **加密存储**: ASP.NET Core 自动使用数据保护 API 加密存储的用户信息
2. **会话隔离**: 每个用户会话独立,不会跨用户共享
3. **自动过期**: 浏览器会话结束时自动清除
4. **HTTPS**: 确保在生产环境使用 HTTPS 防止会话劫持

### 性能影响

- **初始化开销**: 首次访问时需要从会话存储读取(微秒级)
- **内存缓存**: 读取后缓存在内存中,后续访问无需再次读取
- **写入开销**: 登录/登出时写入会话存储(毫秒级)

## 故障排除

### 问题: 刷新后仍然跳转到登录页

**可能原因**:
1. 浏览器禁用了 SessionStorage
2. 浏览器处于隐私/隐身模式(某些浏览器限制 Storage API)
3. 会话存储数据损坏

**解决方法**:
1. 检查浏览器控制台是否有 JavaScript 错误
2. 清除浏览器缓存和会话数据
3. 尝试使用不同的浏览器

### 问题: 登录后立即跳转到登录页

**可能原因**:
1. `AuthorizedPageBase` 在 `ProtectedSessionStorage` 初始化之前检查认证状态

**解决方法**:
- 已在代码中处理:使用延迟初始化模式,确保在访问前完成初始化

## 未来改进

1. **添加刷新令牌**: 实现自动刷新机制,延长会话有效期
2. **添加活动跟踪**: 记录用户最后活动时间,实现自动登出
3. **多因素认证**: 添加 2FA 支持
4. **记住我功能**: 可选的 LocalStorage 持久化

## 回滚方案

如果需要回滚到旧版本:

1. 恢复 `AuthenticationStateService.cs` 到旧版本(使用内存存储)
2. 恢复 `AuthenticationService.cs` 中的 `SetUser()` 和 `Clear()` 调用
3. 恢复 `MainLayout.razor` 中的同步方法

注意:回滚后将失去认证状态持久化功能。
