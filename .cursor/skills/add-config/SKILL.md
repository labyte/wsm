---
name: add-config
description: 当设置添加服务和修改服务配置时，遵循此SKILL
disable-model-invocation: true
---

# Add Config

## 适用场景
- 添加服务功能。
- 修改服务配置。

## 文档基准
- 配置语义：`assets/winsw/xml-config-file.md`
- 日志与错误：`assets/winsw/logging-and-error-reporting.md`

## 界面布局
- 使用标签或者导航切换配置模块
- 配置模块包含：基本、日志、重启策略、依赖
- 其他暂时设置为默认值值


## 实施规则
1. 基本
   - 可执行文件，选择其他所有配置项生成默认值
   - 服务ID:在设置页面中增加服务ID的默认规则
      - 程序名称：直接使用程序名
      - 前缀+程序名
   - 服务名称:在设置页面中增加服务名称的默认规则
      - 程序名称：直接使用程序名
      - 前缀+程序名
   - 描述:在设置页面中增加描述的默认规则
      - 程序名称：直接使用程序名
      - 前缀+程序名
   - 启动模式，选项采用中文
   - 延时启动
   - 自动刷新

2. 日志
   - 可选择日志方案，方案一：管理器提供的方案（winsw）,方案2：读取服务自身输出的文件。
   - 方案一：管理器提供（winsw）
      - 日志字段与错误提示需对齐 `logging-and-error-reporting.md`。
      - 排障步骤与诊断建议需对齐 `troubleshooting.md`，避免经验性“猜测修复”。
      - 参数使用中文显示
   - 方案2：外部日志：
      - 配置文件路径
      - 设置实时追踪，可以借助ps1脚本执行
      - 配置tail 多少行
      - 此方案下，禁止winsw输出日志文件
      - 提供选择日志文件夹，设置匹配日志文件扩展（如 .log/.data/.txt），读取最新改动的日志文件，以适应多个日志文件的清空

3. 重启策略
   - action 只提供 restart，none，不要重启系统
   - 选项采用中文
   - 重置失败，支持数值设置和单位选择，单位：分钟，小时，天，月

4. 依赖设置
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
