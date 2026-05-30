---
name: wsm-winsw-spec
description: Apply WinSW integration rules for WSM. Use when editing WinSW command invocation, XML generation, service install/refresh lifecycle, logging, troubleshooting, and deferred file operations.
disable-model-invocation: true
---

# WSM WinSW Spec

## 适用场景
- 修改 `WinSwHostService`、`WinSwCliExecutor`、WinSW 调用方式与执行链路。
- 修改 WinSW XML 生成规则与服务配置映射。
- 修改服务安装/刷新/卸载、WinSW 日志与排障流程。

## 文档基准
- 命令语义：`assets/winsw/cli-commands.md`
- 配置语义：`assets/winsw/xml-config-file.md`
- 日志与错误：`assets/winsw/logging-and-error-reporting.md`
- 自恢复策略：`assets/winsw/self-restarting-service.md`
- 延迟文件操作：`assets/winsw/deferred-file-operations.md`
- 排障流程：`assets/winsw/troubleshooting.md`
- 总体说明：`assets/winsw/WinSW.md`

## 实施规则
1. 命令执行
   - 优先采用文档定义的命令格式：`winsw <command> <path-to-config>`。
   - 若支持多执行源（x64/x86/custom/global），必须可追踪实际执行路径并可配置。
   - 错误信息必须保留 WinSW 原始 stdout/stderr 关键信息，便于定位。

2. XML 配置
   - 仅输出 `xml-config-file.md` 定义的配置项。
   - 自动恢复仅允许使用 WinSW 标准项：`onfailure` / `resetfailure`。
   - 无文档对应项（例如自定义卡死/假死策略）不得写入 WinSW XML。
   - 布尔项按文档显式写入 `true/false`（避免跨版本默认值差异）。

3. 生命周期与刷新
   - `install/start/status/refresh/uninstall` 行为必须与 `cli-commands.md` 对齐。
   - `refresh` 失败时，仅允许使用文档可解释的回退策略，并提供明确提示。
   - 运行中涉及文件替换/删除场景，优先采用文档推荐的延迟文件操作机制。

4. 日志与排障
   - 日志字段与错误提示需对齐 `logging-and-error-reporting.md`。
   - 排障步骤与诊断建议需对齐 `troubleshooting.md`，避免经验性“猜测修复”。

## 变更后校验
- 必须执行：
  - `dotnet build WSM.sln`
- 必须验证：
  - `install/start/status/refresh/uninstall` 至少一轮可执行。
  - 生成 XML 字段仅包含文档允许项。
  - 错误时可从日志中定位到具体 WinSW 命令与输出。

## 输出要求
- 提交说明需明确：
  - 对应了哪些 WinSW 文档条款。
  - 删除了哪些非 WinSW 标准配置项（如有）。
  - 命令链路与编译验证结果。
