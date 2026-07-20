# Design QA — 参数化紧固件 0.12.1

## Source checks

- 面板使用 28 DIP 控件、4/8/12 DIP 间距和 8 DIP 卡片内边距。
- ≥420 DIP 时自定义长度与嵌入深度并排，三个孔参数按三列显示；340–419 DIP 为两列，窄面板为单列。
- 底部固定为八个等宽图标按钮，最小按钮宽度 28 DIP、间距 2 DIP，在 280 DIP 面板中无需横向滚动。
- 八个图标均包含 1x、1.5x、2x 帧、普通深色和主操作白色版本，并提供完整中文 Tooltip。
- 0.12.1 将图标调整为 20 DIP，统一使用 Rhino 蓝几何、橙色动作提示和深灰轮廓；已通过直接渲染的普通/主操作双状态联系表检查。
- 新建状态只强调“放置”，编辑及批量状态只强调“更新”。
- 单组件模块卡片已压缩，批量模式显示通孔与咬合孔两种角色模板。

## Functional checks

- 57/57 项自动化测试通过。
- Release 构建零警告、零错误。
- Rhino Compat 检查通过。
- 已生成并原位部署 `ParametricFasteners, Version=0.12.1.0`。

## Native Rhino capture

- 已成功启动 Rhino 8，并确认应用窗口可发现。
- Windows 图形捕获在读取窗口画面前返回：`SetIsBorderRequired failed: 不支持此接口 (0x80004002)`。
- 因此本环境无法生成当前面板截图，也无法完成 280–500 DIP 与 100%–200% 缩放的视觉截图对照。

## Final result

`blocked` — 源码布局检查、自动化测试、构建、Compat 和部署通过；实际 Rhino 截图对照被当前 Windows 捕获接口阻止，需要在 Rhino 中目视确认。
