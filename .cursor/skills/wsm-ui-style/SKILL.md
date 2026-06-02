---
name: wsm-ui-style
description: Apply WSM desktop UI conventions for Material Design 3 shell layout, navigation rail/sidebar behavior, and theme switching persistence. Use when editing MainWindow, navigation styles, responsive shell structure, or light/dark theme interaction.
disable-model-invocation: true
---

# WSM UI Style

## 适用场景
- 修改 `MainWindow`、侧边栏、导航分组、导航交互。
- 调整主题切换（浅色/深色）与持久化行为。
- 对齐 `MaterialDesignInXamlToolkit` 示例项目的布局和交互。

## 技术约束
- **UI 框架：WPF**（Legacy net48 / Modern net8），**不是 WinUI 3**。
- 使用 `MaterialDesignThemes.Wpf` 的现有组件与样式键。
- 优先复用 `MaterialDesign3.NavigationRail*`、`MaterialDesign3.NavigationDrawer*` 样式。
- 不引入 WinUI 2.x / WinUI 3、Windows App SDK、UWP 已弃用 API。

## 实施规则
1. 侧边栏与导航
   - 侧边栏优先采用 Navigation Rail（图标+标题）形态。
   - 抽屉与 Rail 并存时，保持交互一致：窄屏抽屉、宽屏常驻 Rail。
   - 导航项标题显示优先使用与控件模板兼容的 `DataTemplate` 方案。

2. 主题切换
   - 使用 `PaletteHelper` + `IThemeManager` 同步主题状态。
   - 浅色/深色切换后需立即生效，并同步 UI 开关状态。
   - 主题偏好必须持久化，应用重启后恢复上次选择。

3. 视觉一致性
   - 全局字体在 `AppTheme.xaml`：`AppFontFamily`（微软雅黑 → 思源黑体 → Segoe UI → Roboto）；日志区 `AppLogFontFamily`。
   - 颜色、背景、选中态优先使用 MaterialDesign 动态资源，避免硬编码颜色。
   - 若出现边框/描边问题，先排查容器与 `ListBoxItem` 模板层级，再局部覆盖样式。

## 变更后校验
- 必须执行：
  - `dotnet build WSM.sln`
- 若涉及 XAML 样式绑定：
  - 确认导航标题、图标、选中态、主题切换均可见且可交互。

## 输出要求
- 提交说明需明确：
  - 修改了哪些文件。
  - 为什么按当前方式实现（与参考项目的对应关系）。
  - 编译验证结果。
