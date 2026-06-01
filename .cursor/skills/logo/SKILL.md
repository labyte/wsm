---
name: logo
description: logo设计要求
disable-model-invocation: true
---

# Logo

## 适用场景
- 任务栏图标、窗体图标、托盘图标。


## 设计要求
- 主色改为 #2196F3基础，做渐变。
- 使用小圆角。
- 显示WSm字母，白色字体，间距紧凑，显示在一行；W/S/m 按字形底边对齐。
- 去掉透明留边让图形尽量铺满。
- 使用 `scripts/generate-wsm-logo.py` 生成 PNG 与 ICO，保证任务栏、窗体、exe 嵌入图标、桌面快捷方式一致。

## 变更后校验
- 图标变更后执行：`python scripts/generate-wsm-logo.py`
- 必须执行：
  - `dotnet build WSM.sln`