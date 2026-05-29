# WSM — Windows Service Manager

基于 WinSW 的 Windows 服务管理工具（模式 A：Legacy + Modern 双应用）。

## 环境要求

- Windows 10+（**日常开发调试 Modern 版**）
- .NET SDK 8.0+（含 `net8.0-windows` 工作负载）
- .NET Framework 4.8 开发者包（编译 Legacy 版）

## 快速开始（Win10 优先）

```powershell
# 还原并编译
dotnet restore WSM.sln
dotnet build WSM.sln -c Debug

# 运行 Modern 版（Win10/11）
dotnet run --project src/WSM.App.Modern/WSM.App.Modern.csproj

# 运行 Legacy 版（Win7~8.1，亦可在 Win10 上运行）
dotnet run --project src/WSM.App.Legacy/WSM.App.Legacy.csproj
```

## 解决方案结构

```
src/
├── WSM.Core/              netstandard2.0  领域模型与接口
├── WSM.Infrastructure/    net48 + net8    WinSW / SCM / 持久化
├── WSM.App.Shared/        net48 + net8    共享 UI（Material Design 3）
├── WSM.App.Legacy/        net48           Legacy 入口
└── WSM.App.Modern/        net8-windows    Modern 入口（Win10 调试首选）
```

## WinSW 资源

将 WinSW Framework 版放入 `assets/winsw/`：

- `WinSW-x64.exe`
- `WinSW-x86.exe`（托管 32 位目标程序）

详见 [assets/winsw/README.md](assets/winsw/README.md)。

## 文档

- [实现步骤文档](docs/IMPLEMENTATION.md)

## P0 状态

- [x] 解决方案与五项目脚手架
- [x] Material Design 3 主界面骨架
- [x] Snackbar 冒泡提示（无 MessageBox）
- [x] 系统托盘（最小化到托盘、右键菜单、双击恢复）
- [x] Modern / Legacy 均可编译运行

## P1 状态（WSM.Core）

- [x] 领域模型：`ManagedService`、日志/失败策略/校验结果等
- [x] 核心接口：`IWinSwHostService`、`IServiceRepository`、`ILogAggregator` 等
- [x] 纯逻辑：`WinSwXmlGenerator`、`ServiceConfigValidator`、`LogLevelParser`、`ServiceIdSuggester`、`OsVersionHelper`
- [x] 单元测试：`tests/WSM.Core.Tests`（25 项）
