# Minimal API vs Controller 架构对比

## 概述

本文档对比了当前实现的 **Minimal API** 方式与传统的 **Controller** 方式的区别、优劣势，以及为什么使用静态类。

## 当前实现 (Minimal API)

```csharp
// Endpoints/SystemMonitorEndpoints.cs
public static class SystemMonitorEndpoints
{
    public static void MapSystemMonitorEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/system")
            .WithTags("System Monitor");

        group.MapGet("/cpu", async (ISystemMonitorService service) =>
        {
            var info = await service.GetCpuInfoAsync();
            return Results.Ok(info);
        });
    }
}

// Program.cs
app.MapSystemMonitorEndpoints();
```

## 传统实现 (Controller)

```csharp
// Controllers/SystemMonitorController.cs
[ApiController]
[Route("api/system")]
public class SystemMonitorController : ControllerBase
{
    private readonly ISystemMonitorService _service;
    
    public SystemMonitorController(ISystemMonitorService service)
    {
        _service = service;
    }
    
    [HttpGet("cpu")]
    public async Task<ActionResult> GetCpu()
    {
        var info = await _service.GetCpuInfoAsync();
        return Ok(info);
    }
}

// Program.cs
builder.Services.AddControllers();
app.MapControllers();
```

---

## 核心区别

### 1. 类的性质

**Minimal API (静态类)**
- 使用 `static class` 和 `static method`
- 通过扩展方法模式扩展 `WebApplication`
- 无需实例化，没有构造函数
- 依赖注入通过方法参数实现

**Controller (实例类)**
- 使用普通类继承 `ControllerBase`
- 通过依赖注入容器实例化
- 在构造函数中注入依赖
- 每个请求创建新实例（默认 Scoped 生命周期）

### 2. 为什么使用静态类？

#### Minimal API 的设计理念
```csharp
// 扩展方法必须在静态类中定义（C# 语言要求）
public static class SystemMonitorEndpoints
{
    // 扩展方法必须是静态的
    public static void MapSystemMonitorEndpoints(this WebApplication app)
    {
        // 端点配置逻辑
    }
}
```

**原因：**
1. **C# 语言限制**: 扩展方法必须定义在静态类的静态方法中
2. **无状态设计**: 端点映射是一次性配置，不需要维护状态
3. **性能优化**: 不需要实例化，避免对象创建开销
4. **清晰的职责**: 只负责路由配置，不处理业务逻辑

#### 依赖注入的区别

**Minimal API** - 参数注入：
```csharp
// 每个端点独立声明依赖
group.MapGet("/cpu", async (ISystemMonitorService service) =>
{
    // service 由框架自动注入
    var info = await service.GetCpuInfoAsync();
    return Results.Ok(info);
});
```

**Controller** - 构造函数注入：
```csharp
public class SystemMonitorController : ControllerBase
{
    private readonly ISystemMonitorService _service;
    
    // 依赖在构造函数中注入，所有方法共享
    public SystemMonitorController(ISystemMonitorService service)
    {
        _service = service;
    }
    
    [HttpGet("cpu")]
    public async Task<ActionResult> GetCpu()
    {
        var info = await _service.GetCpuInfoAsync();
        return Ok(info);
    }
}
```

---

## 优劣对比

### Minimal API 的优势 ✅

#### 1. **性能更优**
- **启动速度**: 减少反射和元数据扫描
- **内存占用**: 无需为每个请求创建 Controller 实例
- **请求延迟**: 更直接的调用路径，减少中间层

```
性能测试对比（Microsoft 官方数据）:
- 内存占用: Minimal API 减少 ~30%
- 请求吞吐: Minimal API 提升 ~15-20%
- 启动时间: Minimal API 减少 ~40%
```

#### 2. **代码更简洁**
```csharp
// Minimal API - 5 行
group.MapGet("/cpu", async (ISystemMonitorService service) =>
{
    var info = await service.GetCpuInfoAsync();
    return Results.Ok(info);
});

// Controller - 13 行（包括类定义）
[ApiController]
[Route("api/system")]
public class SystemMonitorController : ControllerBase
{
    private readonly ISystemMonitorService _service;
    
    public SystemMonitorController(ISystemMonitorService service)
    {
        _service = service;
    }
    
    [HttpGet("cpu")]
    public async Task<ActionResult> GetCpu()
    {
        var info = await _service.GetCpuInfoAsync();
        return Ok(info);
    }
}
```

#### 3. **更灵活的组织方式**
```csharp
// 可以将相关端点分组在同一个文件
public static class SystemMonitorEndpoints
{
    public static void MapSystemMonitorEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/system");
        
        // CPU 相关
        group.MapGet("/cpu", GetCpu);
        group.MapGet("/cpu/cores", GetCpuCores);
        
        // 内存相关
        group.MapGet("/memory", GetMemory);
        group.MapGet("/memory/usage", GetMemoryUsage);
    }
}
```

#### 4. **更好的路由分组**
```csharp
var group = app.MapGroup("/api/system")
    .WithTags("System Monitor")
    .RequireAuthorization()  // 整组应用授权
    .WithOpenApi();          // 整组生成 OpenAPI
```

#### 5. **现代化设计**
- .NET 6+ 推荐的轻量级 API 方式
- 更符合微服务架构
- 更容易迁移到独立服务

### Minimal API 的劣势 ❌

#### 1. **缺少某些 Controller 特性**
```csharp
// Controller 支持的特性
[Authorize(Roles = "Admin")]           // 方法级授权
[ValidateAntiForgeryToken]            // 防 CSRF
[Produces("application/json")]        // 内容协商
[ProducesResponseType(200)]           // 响应类型文档

// Minimal API 需要手动实现
group.MapGet("/data", [Authorize(Roles = "Admin")] 
    async (IService service) => { ... });
```

#### 2. **模型绑定较简单**
```csharp
// Controller - 自动模型绑定和验证
[HttpPost]
public ActionResult Create([FromBody] CreateRequest request)
{
    // 自动验证 DataAnnotations
    if (!ModelState.IsValid) return BadRequest(ModelState);
}

// Minimal API - 需要手动验证
group.MapPost("/create", async (CreateRequest request, IValidator validator) =>
{
    var result = await validator.ValidateAsync(request);
    if (!result.IsValid) return Results.BadRequest(result.Errors);
});
```

#### 3. **测试相对复杂**
```csharp
// Controller - 易于单元测试
var controller = new SystemMonitorController(mockService);
var result = await controller.GetCpu();

// Minimal API - 需要集成测试
var factory = new WebApplicationFactory<Program>();
var client = factory.CreateClient();
var response = await client.GetAsync("/api/system/cpu");
```

### Controller 的优势 ✅

#### 1. **更成熟的生态**
- 丰富的特性和过滤器
- 完善的文档和示例
- 更多的第三方库支持

#### 2. **更好的组织结构**
```csharp
public class SystemMonitorController : ControllerBase
{
    // 清晰的类结构
    private readonly IService1 _service1;
    private readonly IService2 _service2;
    
    // 构造函数
    public SystemMonitorController(...) { }
    
    // 辅助方法
    private void ValidateInput() { }
    
    // 公共端点
    [HttpGet]
    public ActionResult Get() { }
}
```

#### 3. **强类型模型绑定**
- 自动参数绑定
- 内置验证
- 更好的 IDE 支持

### Controller 的劣势 ❌

#### 1. **性能开销**
- 每个请求创建 Controller 实例
- 更多的反射和元数据处理
- 较大的内存占用

#### 2. **代码冗余**
- 需要更多样板代码
- 继承层次结构
- 构造函数注入样板

---

## 为什么选择 Minimal API？

### 1. **项目需求匹配**
- QingFeng 是一个现代化的 NAS 管理系统
- 需要高性能的 API 响应
- API 结构相对简单，不需要复杂的 Controller 特性

### 2. **与现有架构一致**
```csharp
// Program.cs 已使用 Minimal API 风格
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// 已有的 Minimal API 端点
app.MapGet("/api/files/download", async (...) => { });
app.MapPost("/api/files/upload", async (...) => { });
```

### 3. **.NET 10 最佳实践**
- Microsoft 推荐的现代化方式
- 更好的性能和更少的资源占用
- 面向未来的架构

### 4. **扩展性考虑**
```csharp
// 易于拆分为微服务
public static class SystemMonitorEndpoints
{
    public static void MapSystemMonitorEndpoints(this WebApplication app)
    {
        // 这些端点可以轻松提取到独立的微服务中
    }
}
```

---

## 何时使用 Controller？

### 推荐使用 Controller 的场景：

1. **复杂的业务逻辑**
   - 需要大量过滤器和中间件
   - 复杂的模型验证
   - 需要继承共享逻辑

2. **团队熟悉度**
   - 团队更熟悉 Controller 模式
   - 现有代码库使用 Controller
   - 需要保持一致性

3. **特定功能需求**
   - 需要 IActionFilter
   - 需要 ModelState 验证
   - 需要内容协商

---

## 混合使用

两种方式可以在同一项目中共存：

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// 注册 Controllers
builder.Services.AddControllers();

var app = builder.Build();

// 使用 Minimal API
app.MapSystemMonitorEndpoints();

// 使用 Controllers
app.MapControllers();

app.Run();
```

---

## 总结

### 当前实现的优势

✅ **性能优化**: 更快的响应速度，更低的内存占用  
✅ **代码简洁**: 100+ 端点用更少的代码实现  
✅ **易于维护**: 按服务域组织，职责清晰  
✅ **现代化**: 符合 .NET 10 最佳实践  
✅ **扩展性**: 易于拆分为微服务  

### 为什么使用静态类

1. **技术要求**: C# 扩展方法必须在静态类中定义
2. **设计理念**: 端点配置是一次性操作，无需状态
3. **性能考虑**: 避免对象实例化开销
4. **职责单一**: 只负责路由配置，不处理业务

### 关键理念

**Minimal API 的核心思想**：
> 将路由配置与业务逻辑分离。静态类负责配置端点，业务逻辑在 Service 层处理。依赖通过参数注入，而不是构造函数注入。

**Controller 的核心思想**：
> 将相关的端点和逻辑组织在一个类中。Controller 是一个有状态的类，通过构造函数注入依赖，在方法中处理请求。

---

## 参考资料

- [Microsoft Docs - Minimal APIs overview](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis)
- [Microsoft Docs - Choose between controller-based APIs and minimal APIs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/apis)
- [Performance comparison: Minimal APIs vs Controllers](https://devblogs.microsoft.com/dotnet/asp-net-core-updates-in-dotnet-7/#performance-improvements)
