# 技术基础文档

## 0.38.0 智能装配与拾取

- `ViewportPickRayService`负责客户端坐标到屏幕坐标转换、视锥射线方向和反投影校验。
- `AssemblyMeasurementService`生成宿主轴向测量快照；`AssemblySuggestionCalculator`供批量放置、批量更新和检查器复用。
- `AssemblyPlacementSeed`保留来源对象、子对象、入口宿主和可靠性，圆形边及平面内环无需依赖圆心落在修剪面内。
- 批量采用长度仅存在于预检会话，最终通过最新宿主和参数重新生成草稿。
- 收藏模板库 schema v2 可选保存 `AssemblySchemeOptions`，组件 schema v21，模板 schema v5。

## 0.37.3 只咬合顶部对位孔

`EngagementOnlyAlignmentCalculator`以公称直径和独立正补偿计算对位孔，并以半角30°计算60°圆台长度。`EngagementOnlyAlignmentEnvelopeService`沿完整孔口圆周执行精确 Brep 射线相交，以最深入口作为直段计深基准；缺边、多材料区间和厚度不足均在事务提交前失败。主孔、对位直段和圆台统一进入 `CutterGeometryBuild.Shafts`，因此视口派生对象、自愈以及 Rhino、STEP、STL 输出不再分别计算。

## 1. 技术基线

- **运行环境**：Rhino 8.18+ Windows。
- **目标框架**：以 `.NET 7` 为最低目标，`AnyCPU`；兼容 Rhino 8.20+ 的 .NET 8 运行时。
- **插件 SDK**：RhinoCommon；界面使用 Eto Forms。
- **插件类型**：General Utility `.rhp`，最终通过 Yak 分发。
- **当前工作机状态**：已检测到 Rhino 8.18.25098.11001，并使用本地 .NET 8 SDK 完成构建。

Rhino 8.18 默认使用 .NET 7；Rhino 8.20 起默认使用 .NET 8。插件针对 .NET 7 编译，以覆盖当前工作机，同时避免新的 `.NET Framework` 依赖。参考 [RhinoCommon 指南](https://developer.rhino3d.com/guides/rhinocommon/)和[迁移到 .NET Core](https://developer.rhino3d.com/en/guides/rhinocommon/moving-to-dotnet-core/)。

## 2. 建议解决方案结构

```text
src/
  RhinoMM.Core/          预设、参数、补偿公式、验证规则
  RhinoMM.Geometry/      RhinoCommon 切割体与布尔服务
  RhinoMM.Plugin/        命令、Eto 面板、文档事件、UserData、导出
tests/
  RhinoMM.Core.Tests/    无 Rhino UI 的快速测试
  RhinoMM.Integration/   Rhino 环境中的几何、持久化和导出测试
data/
  fastener-presets.v1.json
  locking-nut-presets.v1.json
docs/
```

`Core` 不依赖 UI；`Plugin` 只负责编排输入、文档状态和服务调用。几何构造集中在 `Geometry`，避免命令类重复实现尺寸逻辑。

### 2.1 螺丝头轴向偏移语义

`FastenerComponentData.HeadEmbedDepth`在组件 schema v17 中同时表达嵌入与离面，不新增持久化字段：正值为嵌入，零值为头底贴面，负值为整颗螺丝向放置面外侧移动。控制点与`PlacementFrame`原点不移动。

螺杆实际末端统一按`HeadEmbedDepth + Length`计算。智能宿主扫描、L+1D、L+自定孔底、只咬合长度校验及配套螺母轴向范围必须使用该值；不得在中间服务中把负偏移截断为零。`HeadEmbedDepth <= 0`时头部切割集合为空，杯头架桥设置在编辑草稿规范化阶段关闭。

## 3. 公共命令

| 命令 | 用途 |
| --- | --- |
| `ParametricFasteners` | 打开或聚焦“参数化紧固件”停靠面板 |
| `ParametricFastenersPlace` | 启动虚拟紧固件跟随鼠标的智能连续放置 |
| `ParametricFastenersPlaceClassic` | 使用经典宿主选择、面/点方式放置 |
| `RhinoMMPanel` | 兼容旧工作流的面板命令 |
| `RhinoMMPlaceHole` | 兼容旧工作流的经典放置组件命令 |
| `RhinoMMEditHole` | 编辑现有组件 |
| `RhinoMMAdoptFastener` | 将当前普通螺丝对象接管为 RhinoMM 组件 |
| `RhinoMMRelinkHole` | 重新绑定宿主 |
| `RhinoMMExportPrint` | 无损导出 STEP/STL |
| `RhinoMMValidate` | 检查预设、组件和宿主状态 |

命令英文名保持稳定，面板和命令提示使用中文资源。脚本化命令不得依赖面板当前焦点。

## 4. 领域模型

### 4.1 FastenerPreset

```text
schemaVersion       数据格式版本
presetId            稳定唯一 ID，例如 gb-compatible.socket-cap
displayNameZh       中文显示名
standardReference   标准题录和年份
equivalentSource    ISO 等效来源与版本
dataStatus          iso-equivalent | gb-verified
kind                socketCap | countersunk | hexBolt | hexNut
sizes[]             各公称规格及尺寸
```

每条 `size` 至少包含公称直径、粗牙螺距、三档通孔直径、头部/螺母外形尺寸和深度。规格覆盖从 M1.6 开始；如果某类标准来源没有对应 M1.6 尺寸，则构建时报告缺项，不得由相邻规格插值。沉头角度必须来自数据文件，不得散落硬编码在 UI 或命令中。

### 4.2 PrintProfile

```text
profileId
name
xySizeDeltaMm
zDepthDeltaMm
directBiteReductionBySize
createdAt
updatedAt
```

`xySizeDeltaMm` 表示打印机孔径建模修正；正值扩大 CAD 孔以补偿 FDM 孔偏小。`directBiteReductionBySize` 按公称规格保存直接咬合缩量，因为同一个绝对值不适合同时用于 M1.6 和 M12。全局配置保存在插件设置中，孔组件保存配置名称和数值快照，数值快照是几何计算的最终依据。

### 4.3 HoleComponentData

使用带独立 GUID 的自定义 Rhino `UserData`，并通过 `ArchivableDictionary` 序列化：

```text
schemaVersion
componentId
presetId
presetDataVersion
nominalSize
fitClass
proxyObjectId
adoptedSourceObjectId?
profileSnapshot
fastenerOpacityPercent   0..100，默认 70
cutterOpacityPercent     0..100，默认 35
xyOverrideMm?
zOverrideMm?
depthMode
blindDepthMm?
placementPlane
axisFlip
rotationRadians
targetBindings[]
state              valid | brokenLink | invalidGeometry
```

schema v15 在六角螺母组件中增加 `HexNutStyle`。`Standard`继续读取普通规格表；`NylonInsertLocking`读取独立的防松螺母外包络表。M2、M2.5 数据带工程扩展标记，M3–M12 标记为 GB/T 889.1-2015 兼容尺寸。切割、代理、统计和导出不得各自复制尺寸常量。

必须实现复制、变换、读写和版本迁移。未知的更高版本数据只能只读显示，不能以旧代码覆盖。参考 Rhino 官方 [User Data 示例](https://developer.rhino3d.com/en/samples/rhinocommon/user-data/)。

### 4.4 HoleTargetBinding

```text
targetObjectId
shaftFitRole              clearance | threadEngagement
fitClass?                 close | normal | loose，仅通孔使用
directBiteReductionMm?    正数，仅咬合预孔使用
xyCorrectionOverrideMm?
depthMode                 throughTarget | blind
blindDepthMm?
includeHeadSeat           每个组件最多一个 true
isPreviewVisible          仅控制视口显示，默认 true
isBooleanEnabled          控制导出布尔，默认 true
```

同一组件内 `targetObjectId` 必须唯一。首版 `threadEngagement` 表示直接拧入打印材料的无螺纹圆柱预孔，不等同于标准 6H 内螺纹；数据模型为未来的 `tapPilot` 或真实螺纹策略预留版本字段，但首版不暴露未实现模式。

### 4.5 FastenerProxy

`FastenerProxy` 是组件在 Rhino 视口中的可选择代表，使用标准公称尺寸生成简化头部和杆部，不生成螺旋牙型。它与 `HoleComponentData` 共享稳定 `componentId`，并满足：

- 选择代理、任一切割体或对象属性中的组件链接，都能解析到同一个组件。
- 代理和全部切割体加入 `参数化紧固件::<componentId>` 原生组，点击任一可见成员时整体选择。
- 本体和切割模块使用按组件稳定命名的两套材质；修改透明度只更新材质，不重建几何。
- 代理变换更新 `placementPlane`，随后重建全部绑定切割体并重新验证相交。
- 代理默认不参与打印导出，只作为放置、选择和装配检查的可视对象。
- 更新几何时优先使用 `ObjectTable.Replace` 保持 `proxyObjectId`；若 Rhino 必须产生新 ID，则在同一事务中修正所有反向索引。

### 4.6 组件索引

维护文档级运行时索引：`componentId -> proxyObjectId/cutterObjectIds/targetBindings`，以及 `RhinoObjectId -> componentId` 反向索引。索引只用于选择与性能优化，3DM 中的 UserData 才是持久化事实来源；打开文档、Undo/Redo 或索引不一致时必须从 UserData 重建。

## 5. 几何规则

- 所有预设尺寸以毫米存储，构造前转换为当前 Rhino 文档单位。
- 打印机 XY 修正加到最终建模直径或六角对边尺寸一次；Z 增量加到最终槽深一次。
- 穿过通孔的最终建模直径：`D_clearance = D_standard(fitClass) + C_printer + C_binding`。
- 咬合预孔的最终建模直径：`D_engagement = D_nominal - I_bite + C_printer + C_binding`，其中 `I_bite > 0` 来自按规格校准的数据或显式输入。
- 必须验证 `D_clearance > D_nominal + tolerance`，以及 `0 < D_engagement < D_nominal - tolerance`。
- 切割体必须是闭合、方向一致的 Brep；生成后立即验证 `IsSolid`、有效性和包围盒。
- 杯头由通孔柱体和头部沉孔组成；沉头由通孔和数据驱动角度的锥台组成。
- 六角头和螺母槽使用以对边尺寸定义的六角柱，允许绕轴旋转。
- 贯穿长度按每个宿主与轴线的进入点、离开点分别计算，并在两端增加 `max(10 × absoluteTolerance, 0.2 mm)` 余量；交点失败时才回退到紧包围盒投影。
- 咬合孔深度可取 `L + 2D_nominal` 或自定义值，均从螺杆头下方起算，并裁到当前宿主的实际重叠区间。
- 同一孔组件按 `HoleTargetBinding` 为每个宿主生成独立轴孔切割体；头部沉孔/沉头只并入 `includeHeadSeat = true` 的宿主切割体。
- M1.6 等小尺寸在布尔前比较最小切割尺寸与文档绝对容差：容差大于最小尺寸的 `1/50` 时警告，大于 `1/20` 时阻止计算。

## 6. 放置和绑定

### 智能所见即所得放置

1. `SmartPlacementGetter` 从视口鼠标坐标生成观察射线；自由落点使用射线与封闭 Brep/Extrusion 的最近准确交点，Osnap 落点优先关联当前面或捕捉对象附近的有效面。
2. 放置面的局部 UV 框架提供绕轴方向，经过实体朝内法线校正后形成组件 `PlacementFrame`。
3. 宿主扫描先通过有限螺杆轴段与包围盒相交做候选过滤，再调用准确轴线/Brep 求交；任何包围盒回退结果均不得自动绑定。
4. `SmartHostClassifier` 按进入深度排序：单宿主为咬合体，多宿主最深处为咬合体、其余为穿过体；实体体积重叠或同深度时返回歧义错误。
5. 代理体和切割体使用现有几何服务在内存生成，由 `DynamicDraw` 显示；预览阶段不新增 Rhino 对象、图层、组或材质。
6. 每次有效点击写入新的组件 ID，GetPoint 保持运行；命令结束时最后一颗组件进入编辑会话，整个命令共享 Rhino 的一次 Undo。

### 面放置

1. 通过子对象选择获取 `BrepFace` 和宿主对象。
2. 将用户点击投影到面上，求 UV、点和法向，建立局部放置平面。
3. 在法向两侧用文档容差偏移测试实体包含关系；能判断时自动指向内部，不能判断时要求用户确认。
4. 持续重建轻量预览，只有确认时才写入文档。

### 起点放置

1. 先收集明确的宿主，并为每个 GUID 创建独立 `HoleTargetBinding`。
2. 获取起点和第二点，规范化轴向；两点距离小于容差则拒绝。
3. 使用当前视图/CPlane 仅辅助输入，不把它作为隐式最终方向。

### 变换和生命周期

- 监听 Rhino 文档对象变换、替换、删除和撤销相关事件。
- 同一变换中只移动宿主时，对其唯一关联或同变换多宿主的组件应用相同变换。
- 多个宿主发生不同变换、宿主替换为不支持类型或 GUID 消失时，将组件标记为 `brokenLink`。
- 事件处理必须防止递归，并把自动同步纳入同一 Undo 记录。

### 选择读取与编辑同步

- 监听 Rhino 选择变化，但用短延迟合并连续事件，避免框选时反复刷新面板。
- 选中一个 RhinoMM 代理或切割体时，通过反向索引读取 `HoleComponentData`，面板切换为编辑状态并显示组件 ID、预设版本和绑定列表。
- 选中宿主时只列出关联组件；存在多个关联项时必须由用户明确选择。
- 面板编辑写入独立草稿模型；输入变化只更新预览，不立即覆盖 UserData。
- 用户点击“应用”后执行 `Validate -> Build proxy -> Build per-target cutters -> BeginUndoRecord -> Replace geometry and UserData -> Rebuild index -> Redraw`。
- 构建或验证在写入前失败时不触碰文档；写入阶段异常时撤销整个记录，旧组件继续有效。

### 普通模型接管

1. 接受单个闭合 Brep、Extrusion 或 InstanceReference；若已有 RhinoMM UserData，直接转入编辑。
2. 从圆柱面、旋转对称候选、包围盒和头部轮廓估算轴线、杆径及头型，只产生候选，不直接提交。
3. 将候选尺寸与预设表做容差范围匹配；显示候选标准、规格、偏差和“自动候选/手动选择”状态。
4. 用户确认预设、规格、入口和轴向后，生成简化代理与切割预览。
5. 应用时默认隐藏源对象、保存 `adoptedSourceObjectId` 并创建 RhinoMM 组件；块实例不修改共享定义。
6. 源对象被删除或无法恢复不影响已接管组件，但面板显示“源备份不可用”。

## 7. 导出管线

1. **收集**：仅接受用户选择的闭合 Brep/Extrusion，并查找显式绑定组件及每个宿主的孔配合角色；排除 `RhinoMM::Fasteners` 代理、`RhinoMM::Cutters` 和隐藏的接管源对象。
2. **预检**：验证单位、对象类型、闭合状态、组件版本、绑定唯一性、头部承座唯一性、最终孔径、链接与相交。
3. **复制**：创建临时 Headless `RhinoDoc`，复制宿主并转换 Extrusion 为 Brep。
4. **重建**：为每个宿主从其 `HoleTargetBinding` 生成专属切割体，不依赖文档中可能过期的显示 Brep。
5. **布尔**：只把该宿主的专属切割体用于该宿主副本，按宿主执行 `Brep.CreateBooleanDifference`，使用源文档绝对容差和实体方向。
6. **诊断**：批量布尔失败时在内存中逐组件重试，仅用于定位；最终仍执行全有或全无策略。
7. **写出**：STEP 使用 `FileStp.Write`；STL 使用 `FileStl.Write` 和显式网格参数。包含 M1.6 时，网格最大边长不得大于最小有效孔径的 `1/20`。
8. **提交**：先写同目录临时文件，成功关闭并复读验证后原子替换目标路径。
9. **清理**：关闭临时文档，不修改源文档选择之外的状态。

### Rhino 内渲染成果

- `RhinoPlacementExportService` 在共享布尔结果之外，按所选宿主 ID 收集关联控制点组件并按组件 ID 去重。
- 紧固件实体通过 `FastenerGeometryFactory.CreateProxy` 从持久化参数重建；`IsBooleanEnabled` 只控制宿主切割，不取消紧固件与宿主的装配关联。
- 动态预览对钢和黄铜使用不同显示材质，但鼠标基点始终来自布尔宿主包围盒。
- 提交阶段才创建 `参数化紧固件::渲染紧固件` 图层和共享 PBR 材质；Esc 不触碰文档，写入异常会回滚本次新建资源。
- 面板使用 Eto `MouseButtons.Alternate` 区分 Rhino 导出图标右击，并调用独立命令入口；是否包含紧固件由显式执行参数决定，不使用会污染后续命令的一次性全局标志。

### 全局显示状态

- `GlobalDisplaySettingsService`持久化本体和切割模块透明度，并在插件加载时同步到新建组件草稿。
- 滑块变化后从实际控制点读取当前文档全部组件，按组件 ID 去重，在单一 Undo 中更新对象元数据及每组件稳定材质。
- 选择读取只更新几何和孔参数编辑状态，不得把旧组件透明度反向写入全局显示状态。

RhinoCommon 提供官方的 [STEP 写出接口](https://developer.rhino3d.com/api/rhinocommon/rhino.fileio.filestp)、[STL 写出接口](https://developer.rhino3d.com/api/rhinocommon/rhino.fileio.filestl)和 [Brep 布尔差集](https://developer.rhino3d.com/api/rhinocommon/rhino.geometry.brep/createbooleandifference?overload=1)。

### 模板式更新状态

0.23.0 起，面板状态拆分为三个互不覆盖的职责：

- `FastenerUpdateTemplate` 保存用户当前准备应用的几何与工艺参数。
- `SelectedComponentSummary` 只读统计当前选中的控制点、规格分布、宿主数量和健康状态。
- `ComponentEditorSession` 仅在用户执行“读取组件”时把既有组件载入模板；放置、复制、变换和刷新只同步对象缓存。

批量更新以当前控制点选择作为唯一目标来源。模板先覆盖每个组件的基础参数和同角色绑定，再由智能组件执行宿主重识别与几何预检。预览及导出布尔开关使用可空覆盖语义：未操作时保留各组件原值，用户明确操作后才批量统一。全部草稿预检通过后才能进入一次 Rhino Undo 提交。

### 咬合孔深度 v10

0.24.0 起，界面只暴露完全贯穿、L+1D 和 L+自定毫米三种咬合孔深度。自定模式继续使用 `HoleTargetBinding.BlindDepth` 存储追加毫米，计算为 `HeadEmbedDepth + Length + BlindDepth`。旧 L+2D 和绝对盲孔枚举保留用于 JSON 反序列化，组件载入时迁移为等效追加毫米；新写入数据不再产生旧模式。

## 8. 容差、单位与错误处理

- 未设置模型单位时禁止生成与导出。
- 几何运算始终使用 `RhinoDoc.ModelAbsoluteTolerance`；数据比较使用毫米空间的独立小容差。
- STL 默认将临时几何缩放为毫米，界面明确显示输出单位。
- 所有用户错误返回结构化诊断：错误代码、组件 ID、宿主 ID、阶段、可执行修复建议。
- 不在日志中写入用户完整文件路径以外的敏感数据；不联网读取规格数据。

## 9. 测试策略

- **数据测试**：架构、唯一 ID、M1.6–M12 覆盖、粗牙螺距、正数尺寸、三档孔径顺序、来源字段。
- **公式测试**：单位换算、通孔正余量、咬合缩量、打印机修正、六角对边、深度和安全余量。
- **几何测试**：四类孔型的 M1.6/M2/M6/M12、平面/曲面、自动朝内方向、经典轴向、盲孔/贯穿。
- **多宿主测试**：同一轴线的通孔宿主与咬合宿主得到不同孔径；切割体不跨宿主误用；承座只出现在指定宿主。
- **文档测试**：UserData 保存重开、复制、变换、删除、Undo/Redo 和版本迁移。
- **读取编辑测试**：分别选择代理、切割体和宿主；验证面板读取结果、歧义列表、原子更新、ID 保持、失败回滚和 Undo/Redo。
- **接管测试**：普通 Brep、Extrusion、块实例、已有 RhinoMM 对象、无法识别对象、源对象隐藏/恢复和手动映射。
- **导出测试**：STEP/STL 复读、闭合性、单位、几何体积、切割体排除、源对象不变。
- **失败测试**：无交集、非闭合、尺寸退化、布尔失败、目标文件已存在和临时写入失败。

集成验收应比较导出前后源对象 GUID、几何哈希和组件计数，并使用已知量规几何验证孔径，而不仅检查命令返回成功。

## 10. 标准数据治理

标准题录以国家标准信息公共服务平台为准。当前首版关注：

- [杯头对应标准（GB/T 70.1-2008）](https://openstd.samr.gov.cn/bzgk/std/newGbInfo?hcno=AA481BA038B2F9CE68879E6B087F4F73)
- [GB/T 70.3-2023 降低承载能力内六角沉头螺钉](https://openstd.samr.gov.cn/bzgk/std/newGbInfo?hcno=F6B723A6B05D3C27FD72C3D925A956A4)
- GB/T 5783-2025 紧固件 六角头螺栓 全螺纹
- [GB/T 6170-2015 1 型六角螺母](https://openstd.samr.gov.cn/bzgk/std/newGbInfo?hcno=BDDE5AF77AC2FBC2D194289F10C69A4B)
- [GB/T 5277-1985 紧固件 螺栓和螺钉通孔](https://openstd.samr.gov.cn/bzgk/std/newGbInfo?hcno=7C6B6039F8E5FB5FD8F318E06BAFB0B5)
- [杯头沉孔参考（GB/T 152.3-1988）](https://openstd.samr.gov.cn/bzgk/gb/newGbInfo?hcno=9959F71ECC47D1E24E0FF3B19CF53CE8)
- [GB/T 196-2025 普通螺纹 基本尺寸](https://openstd.samr.gov.cn/bzgk/std/newGbInfo?hcno=7923264EF5B418EA786AAEACA4835708)
- [GB/T 197-2018 普通螺纹 公差](https://openstd.samr.gov.cn/bzgk/std/newGbInfo?hcno=4B9C1010476FF8385A59AD3076EC3A68)

采标全文或尺寸表没有合法来源时，只能录入可追溯的 ISO 等效数据并设置 `dataStatus: iso-equivalent`。取得授权资料、双人复核并通过尺寸回归测试后，才允许切换为 `gb-verified`。

ISO 273 的一般用途通孔范围包含从 1 mm 起的细、中、粗系列；螺纹基础尺寸和公差分别由普通公制螺纹标准管理。咬合预孔属于 FDM 工艺参数，不应伪装成 GB/ISO 标准尺寸。

# 0.25.0 杯头沉孔双层架桥几何

- `FastenerComponentData.CounterboreBridgeEnabled`与`CounterboreBridgeLayerHeight`属于组件级几何参数，schema v11 缺省为关闭和 0.20 mm。
- 杯头沉孔主体截止于`HeadEmbedDepth`。第一层切割中央切线槽，第二层切割边长等于最终杆孔直径的方孔，之后由轴孔切割体恢复圆孔。
- 切线位置使用`HoleDiameterCalculator`得到的宿主最终孔径；v14 新公式中通孔包含孔径修正，咬合孔包含咬合缩减，两者均最后叠加目标覆盖值。
- 架桥开启时一个头部承座绑定包含三个`HeadCutter`：沉孔主体、第一层切线槽和第二层方孔。
- `CutterGeometryService`负责宿主厚度、文档公差和架桥有效空间校验；预览对象只是显示缓存，导出继续实时重建切割几何。

# 0.26.0 单宿主只咬合与显示预览事务

- `FastenerComponentData.EngagementOnly`属于组件级宿主策略，schema v12 对旧组件缺省为关闭。
- `EngagementOnlyHostValidator`使用放置面宿主的精确轴向离开深度计算最大螺杆长度，并拒绝螺杆物理范围内的第二宿主。
- `SmartHostBindingService`在创建、更新和导出前统一执行严格校验；智能组件关闭模式后恢复既有自动重识别流程。
- `GlobalDisplaySettingsDialog`在一个 Rhino Undo 记录中更新共享显示材质和组件元数据；取消时撤销预览事务并恢复会话设置。
- 0.26.1 起，`FastenerUpdateTemplate`在通孔角色转换为只咬合角色时，显式从原咬合绑定或`SmartBindingProfile`继承模块开关，禁止继承通孔布尔状态。
- `BooleanExportService`在筛选启用绑定前调用`SmartHostBindingService.TryReconcile`，以控制点参数和当前宿主几何构造只咬合的规范化绑定。
- schema v13 修复 v12 只咬合组件的角色开关错配：咬合绑定以持久化`SmartBindingProfile`中的咬合开关为准；之后面板即时修改会同时更新绑定和智能模板。

# 0.27.0 孔径公式版本与贯穿覆盖范围

- `HoleDiameterFormula.NominalIndependent`采用 `Clearance=D+correction+override`、`Engagement=D-bite+override`。
- `LegacyStandardWithSharedCorrection`只用于保持 schema v13 及更早组件的既有尺寸；`FastenerUpdateTemplate.ApplyTo`会把显式更新转换为新公式。
- `CutterFootprintEnvelopeService`以最终孔径探测圆柱和宿主做布尔交集，再在组件局部轴向提取完整最小/最大覆盖范围；通孔和完全贯穿咬合孔统一使用该范围。
- 0.27.1 将同一最小覆盖位置用于盲咬合孔入口，避免倾斜进入面残留楔形材料；盲孔终点仍使用公式深度，只有达到完整最大覆盖位置时才按贯穿处理。
- 正常自动识别使用未裁剪的第一宿主离开深度校验螺杆是否完整穿出，并要求轴向范围内存在后方宿主。

# 0.30.0 快速编辑与模板基础设施

- `FastenerTemplateData`是独立于 3DM 组件的用户级模板 schema v1。模板包含可复用几何与工艺参数，但不包含组件 ID、绑定、宿主、坐标、目标附加修正或全局显示值。
- `FastenerTemplateLibraryService`通过 Rhino `PersistentSettings`保存最近 5 条、收藏 50 条及最后一次成功放置/更新；规范化签名会清除类型无关字段并去重。
- `ViewportQuickEditorService`监听控制点选择和 Rhino 命令生命周期，使用 250 ms 延迟显示无任务栏 Eto 窗口；快速草稿独立于`EditorState`。
- `FastenerGeometryPreparationService`统一执行智能宿主重识别、组件校验、本体构建、切割体构建和警告汇总。
- `ComponentUpdateCoordinator`统一单组件/批量提交、一次 Undo、控制点选择恢复和编辑会话缓存同步；主面板、快速编辑、参数手柄和重复更新均复用该管线。
- 参数手柄采用临时`GetPoint`与`DynamicDraw`，不创建 Rhino Grip 或文档对象；每次第二次点击确认后才提交组件。
- 组件 schema 保持 v16；模板 schema 与 3DM schema 分离，模板损坏不会影响已有模型加载。

# 0.30.1 紧凑快速编辑器

- `ViewportQuickEditorWindow`使用 252 DIP 逻辑宽度和类型相关高度；状态行仅在警告或错误时参与布局。
- 快速窗口标题由当前草稿实时格式化为 `M3X20` 等核心规格，完整组件摘要只作为 Tooltip。
- 确定继续复用`FastenerGeometryPreparationService`和`ComponentUpdateCoordinator`，提交后通过用户关闭事件抑制同一控制点再次自动弹出。
- `RhinoMMPanel`直接绑定`ViewportQuickEditorService.AutoShowEnabled`，复用`QuickEditor.AutoShow`持久化键，不引入新的组件字段。

# 0.30.2 热熔贯穿包络与工具栏命令共享

- `CutterGeometryService`不再对热熔螺母执行本体厚度阻断；六角螺母仍保留安装槽厚度校验。
- 热熔贯穿使用`HeatSetMouthDiameter`和`CutterFootprintEnvelopeService`计算最大入口圆截面的完整宿主覆盖范围，切割终点取参数深度与覆盖出口余量的较大值。
- `ComponentRefreshCoordinator`统一面板和公开刷新命令的修复、选择恢复及编辑会话同步。
- RUI 通过公开的应用更新、刷新和更多命令复用面板工作流；Rhino 输出按钮使用`right_macro_id`提供包含紧固件实体的右击宏。
- 组件 schema 保持 v16。

# 0.30.3 快速标题与面板图标资源

- `FastenerLabels.ShortKind`集中提供杯头、沉头、六角头、普通/防松六角螺母和热熔螺母的短名称。
- `ViewportQuickEditorWindow`使用短类型和草稿规格生成单行标题，普通/防松切换会触发标题刷新。
- 面板图标以16、24、32、48像素PNG帧封装为嵌入式`panel.ico`，并通过`Panels.RegisterPanel`资源重载及`PanelType.System`注册。
- 组件schema保持v16。

# 0.32.0 批量创建、文档索引与问题中心

- `FastenerDocumentIndexService`是文档局部、可重建的运行时索引。它用 `RTree` 保存普通封闭 Brep/Extrusion 宿主的包围盒，并从控制点元数据构建组件健康摘要；3DM 用户字符串仍是唯一持久化事实来源。
- 对象新增、删除、替换、属性修改、文档打开/关闭和 Undo/Redo 会使索引失效；智能鼠标预览、轴向宿主扫描和点集放置先查询空间索引，再执行精确 Brep 求交。
- `SmartPlacementService.EvaluateAtPoint`只接受位于实体面公差内的点，不进行最近面投影。多个宿主表面在同一公差范围内重合时返回歧义失败。
- `BatchPlacementService`从 Point、PointCloud、开放曲线端点和圆/圆弧中心提取候选，按文档公差去重，并以不可变 `FastenerTemplateData`逐点预检。
- `ParametricFastenersBatchPlace`默认全有或全无；用户显式选择跳过失败点时只提交有效/警告草稿。最终统一调用 `FastenerComponentService.CreateOrReplaceMany`，确保一次 Undo 和独立组件 ID。
- `ComponentNavigatorPanel`只消费只读索引并改变选择，不调用 `EditorState.Load`。筛选结果可交给现有更新协调器、统计和导出入口，因此不会形成第二套组件写入逻辑。
- `ComponentBooleanHealthService`只在用户点击“深度检查”后，基于当前控制点参数实时构造切割体，并对宿主内存副本执行真实布尔；结果只缓存在导航器会话中，索引修订变化后自动清除。
- `FastenerLightweightProxyCache`只缓存 WorldXY 局部坐标中的紧固件本体，键覆盖影响实体外形的参数但不包含组件 ID、宿主或放置坐标；最多保留 48 项，移动鼠标时仅复制并变换。
- 智能放置悬停阶段使用命令级宿主 RTree、宿主级射线求交和解析孔轮廓，不进入完整切割准备；单击时生成一次 `PreparedFastenerGeometry` 并通过已准备提交入口写入。
- 点位预检、深度检查和导航筛选均不创建临时 Rhino 文档对象或材质；取消不会留下残留。
- 插件版本为 0.32.0，组件 schema 继续保持 v16。

# 0.32.1 智能放置宿主索引热修复

- 索引候选的可用性必须通过当前 `RhinoDoc.Objects.FindId`判断，不依赖可能被 Rhino 临时分离的缓存 `RhinoObject.Document`。
- RTree 零结果时先检查索引内宿主包围盒，再回退到 0.30 使用的 `SmartHostBindingService.CaptureHosts`，避免文档打开、对象替换或事件时序造成全局“未命中封闭实体”。
- 回退只在空间索引没有返回任何候选时运行，正常拾取仍使用 RTree 快速路径。
- 组件 schema 保持 v16。

# 0.32.2 智能放置候选路径回退

- 0.32.1 只能修复索引返回零候选的情况；当 RTree 返回非空但错误的预筛选集合时，仍可能漏掉鼠标下方实体。
- 智能鼠标拾取、点位严格匹配和轴向宿主分类恢复使用命令启动时的 `SmartHostBindingService.CaptureHosts` 快照，与 0.30.3 的已验证行为一致。
- 文档级索引继续服务组件导航器与问题中心，但不再作为放置能否工作的前置依赖。
- 组件 schema 保持 v16。

# 0.32.3 批量预检窗口布局

- `BatchPlacementDialog`改用固定摘要、可伸缩`GridView`和固定操作栏，只有表格行参与纵向缩放。
- 默认高度按点位数量在320–520 DIP间计算；表格列根据可用宽度重排并预留滚动条空间，避免横向滚动。
- 有效、警告和失败统计使用语义主题状态色；无失败时显示可创建数量，有失败时必须显式勾选跳过后才能创建。
- 对话框监听Rhino主题变化并复用`FastenerUiTheme`，组件 schema继续保持v16。

# 0.34.0 检查、交付与自定义规格架构

## 组件和规格解析

- `FastenerComponentData` schema v17 通过 `DeliveryMetadata` 保存装配编号、项目分组和备注，并保存可空的自定义定义 ID、名称和完整 `FastenerDefinitionSnapshot`。
- `FastenerSpecResolver`是所有几何入口的统一规格解析器：组件存在自定义快照时优先使用快照，否则回退内置 `FastenerCatalog`。放置、更新、预览、手柄、复制、检查和导出不得绕过该解析器。
- `FastenerTemplateData` schema v2 同步保存自定义定义身份和尺寸快照，但继续排除组件 ID、宿主、绑定、控制点和交付元数据。
- `UserFastenerLibraryService`使用独立 schema v1 和插件持久化设置保存用户定义；导入、保存前由 `UserFastenerDefinitionValidator`执行有限值、正值及类型尺寸关系校验。

## 只读检查与维护事务

- `AssemblyInspectionService`从文档索引和控制点参数建立只读报告；布尔和碰撞运算只在内存副本上执行，不写入 Rhino 文档。
- 检查结果使用稳定问题代码、严重级别、组件/宿主定位和可空长度建议，UI 只负责筛选、定位和显式调用更新协调器。
- `ComponentMaintenanceService`将刷新、重绑和清理分离。重绑候选由轴线区间、切割体包围盒重叠和目标关系评分产生，只有用户确认的映射才进入一次 Undo 提交。
- 清理以实际 Rhino Point 控制点为唯一生命周期依据；缺少预览切割体不是删除组件的条件。

## 原子交付快照

- `OutputCenterService`先固定输出范围、组件数据、宿主副本和布尔结果，再从同一快照派生 3DM、STEP、STL、Excel 和 CSV。
- 写入使用最终目录旁的临时目录；任一格式失败即删除临时目录，全部文件成功后使用目录重命名提交，避免出现半套交付文件。
- 3DM/STEP 可附带渲染紧固件实体；STL 始终只包含布尔后的可打印宿主。Excel 与 CSV 使用同一 `FastenerStatisticsReport` 和宿主交付信息映射。
- 输出前调用装配检查器。宿主丢失、参数无效、确定性碰撞和布尔失败阻断输出；薄壁、孔距、工具空间和关闭布尔属于可确认警告。

# 0.35.0 统一 UI 与增量任务架构

- `FastenerUiTheme`集中提供字体、间距、控件高度、卡片、边框、选中、焦点、状态和进度语义；窗口只登记语义角色，不比较旧颜色。
- `CreateWindowShell`固定摘要与操作栏，仅内容行设置`ScaleHeight`；响应式断点必须使用扣除边距和滚动条后的实际宽度。
- `FastenerOperationProgress`统一空闲、准备、执行、成功、警告、失败和取消状态；`FastenerOperationStatusView`负责阶段、计数、进度和取消入口。
- `FastenerUiOperationContext`只在 Rhino 主线程的组件、宿主或格式步骤之间调用`RhinoApp.Wait`让出 UI；禁止后台线程访问`RhinoDoc`或`Brep`。
- 装配检查和批量预检取消时丢弃结果；重绑在确认后统一提交；输出先写临时目录，取消或异常均清理后返回。
- `ViewportOperationFeedback`使用短时 DisplayConduit 绘制控制点环或联合包围盒，不写入 Rhino 对象、材质或组件元数据。
- `AssemblyInspectorDialog`按文档运行单一非模态实例并监听文档变化；过期报告只能定位，不能采用长度建议。
- 组件 schema 保持 v17；UI 状态、进度与反馈均为非持久化运行时数据。

# 0.35.5 Yak 安装与启动自修复

- 正式发布物是顶层包含`ParametricFasteners.rhp`、同名RUI、核心依赖、manifest和许可说明的 Rhino 8.18 Windows Yak 包；ASCII载荷名称降低跨电脑路径解析风险，原始插件目录不再承担最终用户安装职责。
- 离线安装器使用 Rhino 自带`Yak.exe install <local.yak>`，安装前验证进程、SHA256、包结构与系统级注册冲突，安装后通过`yak list`和实际RHP路径双重确认，并在插件GUID根项写入`Name/FileName/LoadMode`。
- 插件覆盖`PlugIn.LoadTime`为`AtStartup`。`OnLoad`只注册设置、面板和服务，工具栏协调延迟到首次`RhinoApp.Idle`，避免与Rhino启动布局恢复竞争。
- RUI文件GUID固定为`5c120a44-494a-4973-a495-e8c4218b0c22`。协调器遍历已打开工具栏：当前路径存在时不操作；同GUID旧路径只关闭旧文件并打开程序集同目录RUI；其他RUI和布局不受影响。
- 关于窗口通过程序集实际路径判断Yak/手动安装，并公开RHP、RUI路径和健康状态。组件schema继续保持v17，安装修复不接触3DM元数据。

# 0.38.4 Yak首次启动激活修复

- Rhino首次处理简略注册时会将根项`FileName`迁移到`PlugIn`子项并生成`CommandList`；0.38.3同时使用Yak和手工简略注册，导致首次启动可能只完成登记而没有激活命令。
- 离线安装器不再创建`Name/FileName/LoadMode`，Yak成为正式安装和版本切换的唯一注册来源；升级只删除明确位于参数化紧固件Yak目录中的旧根项`FileName`。
- 诊断工具以`PlugIn\FileName`为完整注册事实，根项路径仅视为等待Rhino首次处理的临时状态，并安全处理所有缺失或空值。
- 插件仍使用`PlugInLoadTime.AtStartup`，安装后只需完整启动Rhino一次。

# 0.36.0 可靠内核与文档自愈

## 单一事实来源

- `FastenerComponentSnapshotService`只从唯一实际控制点读取完整组件 JSON；代理体和切割体只保留组件 ID、角色、绑定 ID、宿主 ID及派生签名。
- 控制点实际 Point 坐标优先于 JSON 中的旧放置原点，方向和参数仍来自控制点 JSON。重复控制点、损坏 JSON 和组件 ID 冲突直接归类为损坏。
- `ComponentEditorSession`只缓存组件 ID、选择顺序和文档修订号；任何编辑、统计、检查和导出在使用前重新解析控制点。

## 几何与事务

- `FastenerGeometryPreparationService`统一执行规格解析、组件校验、本体构建和切割构建，返回 `PreparedFastenerGeometry`。
- 参数签名覆盖规格快照、放置坐标、装配方式、孔径/孔深、螺母、热熔、架桥、绑定开关、宿主几何指纹、单位与公差；派生对象另存几何签名、角色和分段序号。
- 智能预览、文档代理、Rhino/STEP/STL 输出均消费相同准备结果，预览缓存使用参数签名、宿主指纹和文档修订号作为键。
- `ComponentMutationJournal`记录提交过程中的新增、删除、属性、内部组和材质变化；异常时按逆序恢复。批量操作在全部准备成功后才开始写入。

## 健康扫描

- `ComponentDocumentHealthService`在文档打开后的 Idle 阶段以约 12 ms 时间片执行只读扫描。
- 扫描比较控制点快照、宿主可用性、准备签名、派生对象角色/分段/几何签名、内部组和共享显示引用，生成正常、需重建、待重绑或损坏状态。
- 扫描不会修改对象、创建 Undo 或改变文档修改状态；正常报告保持静默。
- 确定性修复只提交“需重建”组件；待重绑进入人工向导，损坏组件只定位和解释，缺少控制点的孤立对象由独立清理命令处理。

# 0.36.1 咬合孔入口直线倒角

- `FastenerComponentData` schema v18 增加 `EngagementEntryChamferEnabled` 与 `EngagementEntryChamferSize`；旧组件迁移为关闭和 `0.50 mm`。
- `FastenerTemplateData` schema v3 同步保存倒角参数；放置预设、最近模板、收藏、重复放置和重复更新共用同一数据。
- `EngagementEntryEnvelopeService`先以最终咬合孔半径测量入口与出口；不以放大后的倒角半径反向改变入口包络。
- 平面入口把实际 BrepFace 平面转换到螺丝局部坐标，解析求解轴向45°锥体的闭合交线；曲面或跨面入口按径向方向执行有界根求解。
- 包围盒只限制射线和根的搜索范围，所有入口深度均来自精确 Brep 相交；入口达到45°物理临界、靠近边缘或存在多个轴向区间时明确停止。
- `EngagementEntryChamferCalculator`令锥面保持1:1斜率。锥口小端位于最深入口之后 C，因而最深侧的径向与轴向倒角均不少于 C；外侧只增加布尔余量。
- 主圆柱和倒角锥体同时写入 `CutterGeometryBuild.Shafts`，因此智能预览、代理对象、健康修复及 Rhino/STEP/STL 导出使用同一几何段集合。
- 倒角只在 `ScrewAssemblyMode.ThreadEngagement`且绑定角色为`ThreadEngagement`时生效；模块关闭导出布尔时主孔与倒角一起排除。

# 0.36.3 双模式入口倒角

- `FastenerComponentData` schema v19 增加 `EngagementEntryChamferMode`；v18组件固定迁移为`AxialFortyFive`，避免既有倒角改变。
- `FastenerTemplateData` schema v4以及放置预设、智能摆放签名、收藏和重复操作同步保存倒角方式。
- `AxialFortyFive`继续使用`EngagementEntryEnvelopeService`与`EngagementEntryChamferCalculator`，其几何输出保持0.36.2不变。
- `SurfaceEqualDistance`先在宿主副本上切出主孔，从首次入口的单一平面Face识别完整圆柱孔口边环，再调用RhinoCommon distance-from-edge Chamfer，对宿主表面和孔壁分别退让C。
- 等距倒角完成后以“原宿主 − 倒角后宿主”反求完整宿主专属切割体，并再次应用验证体积；无法稳定复现时不会产生预览对象或导出结果。
- 表面等距明确拒绝曲面、跨非共面入口、多候选边环、边缘材料不足和非唯一布尔结果，不使用最近边或包围盒近似。
- 倒角模式进入参数签名和健康检查；切换模式后旧派生对象被标记为需重建，智能预览、代理、一键修复和三类导出共用同一切割结果。
# 0.38.5 范围隔离与宿主区间

- `BooleanExportService`只从控制点仓储收集绑定到所选宿主的组件，并逐绑定调用共享切割服务；不再对全文件执行宿主重识别。
- `SmartHostIntervalService`以精确 Brep 求交、交点聚类和区间中点实体内外判断生成轴向材料区间。中心轴命中接缝时，八条公差级微偏移射线至少五条一致才接受。
- `ConfirmedEngagementHostId`属于组件 v21 的宿主决策事实。更新时只验证该宿主是否仍在轴后方并可到达，不自动替换。
- 热熔安装孔以入口最大直径计算完整截面覆盖范围，参数深度和宿主出口余量取较大值；超过宿主只产生贯穿警告。

# 0.38.6 光标预览与精确提交分离

- `SmartPlacementCursorPreview`只保存前表面、放置轴、参数签名、本体局部代理和显示变换，不包含绑定、切割体或准备几何。
- 光标路径使用RTree和精确前表面求交，但不调用轴向宿主扫描、角色分类或`FastenerGeometryPreparationService`。
- `FastenerLightweightProxyCache`拥有WorldXY局部代理；DynamicDraw通过`PushModelTransform`显示，避免逐帧复制和变换Brep。
- `SmartPlacementAnchorSignature`在点击时比较参数、宿主、面、位置、轴向和文档修订；通过后才执行完整准备与一次事务提交。
