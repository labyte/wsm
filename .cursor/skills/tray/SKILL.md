---
name: tray
description: 托盘设计要求
disable-model-invocation: true
---

# tray

## 适用场景
- 托盘
- 鼠标移入显示：WSM服务监控中（运行服务数量/总服务数）


## 托盘菜单
- 打开主窗口（运行服务数量/总服务数） 替换原有的显示主窗口，但是点击还是应该打开主窗口
- 启动全部服务。
- 停止全部服务。
- 退出。


## 变更后校验
- 必须执行：
  - `dotnet build WSM.sln`