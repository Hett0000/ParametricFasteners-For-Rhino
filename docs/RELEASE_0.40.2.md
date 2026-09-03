# 参数化紧固件 0.40.2 / Parametric Fasteners 0.40.2

## 中文

0.40.2 修复主面板顶部“快速小窗”开关在窄面板、长模板名称或英文界面下被遮挡的问题。

- 模板工具栏根据实际可用宽度自动使用单行或双行布局。
- “快速小窗”开关按当前语言测量宽度，始终优先完整显示。
- 模板名称只占剩余空间，单行省略并通过 Tooltip 显示完整内容。
- 面板宽度、语言、DPI或模板名称变化后会立即重新计算布局。
- 开关状态、自动显示行为和跨会话设置保持不变。

组件 schema 保持 v21，模板 schema 保持 v5，模板库 schema 保持 v2。

## English

Version 0.40.2 fixes the **Quick Editor** toggle being clipped by narrow panels, long template names, or English UI text.

- The template toolbar automatically switches between one and two rows using the actual available width.
- The Quick Editor toggle is measured in the active language and receives display priority.
- Template names use only the remaining width, with a single-line ellipsis and full tooltip.
- Panel width, language, DPI, and template-name changes immediately refresh the layout.
- Toggle state, automatic display behavior, and persistent settings are unchanged.

Component schema remains v21, template schema remains v5, and template-library schema remains v2.
