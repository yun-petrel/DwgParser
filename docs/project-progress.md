# DwgParser 项目进展

> 容量约束：**本档 ≤ 8 KB（8192 B）**。超限时把已收口的细节迁到 `.workbuddy/memory/` 或对应技能档，
> 本档只留现势目标、未解决项与变更清单。

## 下一步目标（2026-11-03）：实体类重构

### 目标架构

- **文档 = 容器 + 实体集合类**：`DwgDocument` 与各节/表容器管版本、句柄与段索引；实体集合类
  （模型空间、图纸空间、块内容）持有「句柄 → 实体」映射与稳定遍历序。
- **实体只担两件事**：① **读写自己的字节流**——读侧按源版本解码、写侧按目标版本编码，位形对齐
  `dwg.spec`；② **把属性字段暴露给渲染**——实体自身不含绘制逻辑。
- **渲染走设备层**：实体只调用设备层提供的底层绘图接口（线 / 圆 / 弧 / 样条 / 填充 / 文字 / 块引用
  等基元），由**属性字段**（图层与颜色、线型与比例、填充样式、文字样式与字高、可见性等）驱动设备层
  状态；实体不持有绘图上下文，渲染层也不反向依赖解码细节。

### 重构要点

1. **三段分离**：字节流编解码 / 属性模型 / 渲染提交各自分片，单文件守 800 行硬上限。
2. **集合类统一**：`Add` / `Remove` / `Enumerate` / `OwnerHandle` 口径一致，集合遍历序即序列化序。
3. **设备层收敛**：接口基元集最小化，`DwgParser.Rendering` 只依赖该接口。
4. **不变量**：零第三方依赖进 Core、解析失败不抛异常、写侧产物字节不变（回归门不动）。

落地计划（车道划分、文件所有权、波次、验收门）见 `docs/entity-refactor-plan.md`。

**进度**：W0 已完成（2026-11-04）——三契约落位（`IEntityCollection`/`IEntityCodec`+Registry/
`IDrawDevice.PushState/PopState`）、G1 构建门首次转绿（Rendering/CLI 补 windows TFM 等三处既有门修复）、
基线 3 fixture 存 `out/base/`（JSON/SVG/同版本转换逐字节可比，清单 manifest.json）。下一步 W1：
L1a 曲线/几何族拆分 + L4 集合层接入 + L5a 渲染三路合一。冻结偏差与裁定见计划档 §3.1。

## 2026-09-29 HATCH 渐变块跨版本处理修复

### 问题概述

DwgParser 在跨版本 DWG 转换中，HATCH 实体的渐变填充块（gradient fill block，
libredwg dwg.spec 中标记为 `SINCE(R_2004a)`）处理存在多个缺陷，导致：

1. **AC1015→AC1027 转换中 HATCH 69DEC/69DEE 丢失**：R2000 源无渐变块，
   R2010+ 目标读取器期望消费渐变块，未插入时几何错位 → EndOfStreamException
2. **AC1018→AC1015 转换中 HATCH 17CA4 丢失**：R2004+ 源含渐变块，
   R2000 目标读取器不消费，按位搬运后渐变块残留 → 后续字段错位
3. **AC1015→AC1015（MgF2 波片）HATCH 17C9A 源侧解码缺陷**：
   polyline path bulge BD 消费位偏移不一致（源文件固有问题，非转换引入）

### 已实施的修复

#### 1. R2000 对象映射 per-page 增量重置 (`R2000File.cs`)
- **根因**：`TryReadObjectMap` 中 `lastHandle`/`lastOffset` 在页循环外声明，
  跨页累积导致第 2 页起增量从上一页末值累加 → 越界偏移 → 映射解码中断
- **修复**：将声明移入页循环内部，与 libredwg decode.c:748-749 一致
- **效果**：MgF2→AC1015 映射从 607 条恢复到 763 条，lost 从 76→0

#### 2. HATCH 渐变块降级丢弃 (`DwgObjectStreamWriter.Carry.cs`)
- **根因**：R2004+ 源 HATCH 几何含渐变块，按位搬运到 R2000 目标后，
  R2000 读取器不消费渐变块 → 后续 elevation/pattern/num_paths 全部错位
- **修复**：`HatchGeometryWindow` 检测降级方向（R2004+ 源→R2000 目标），
  从 `HatchGradientEndBit` 起搬运几何，丢弃渐变块
- **效果**：HATCH 17CA4 保留

#### 3. HATCH 渐变块升级插入 (`DwgObjectStreamWriter.Carry.cs`)
- **根因**：R2000 源 HATCH 无渐变块，R2007+ 目标读取器期望消费 → 几何错位
- **修复**：`WriteHatchGradientIfUpgrade` 插入最小渐变块
  （7×BL/BD0 = 14 位 + 空渐变名 2 位，共 16 位）
  - AC1018 目标：内联 `WriteTV("")` 空串（BS(0) = 2 位）
  - AC1027 目标：渐变名写入集中字符串流（`hatchGradientName` 作为
    `CarryWithStrings` 的 `leadingString` 参数传入）
- **调用位置**：`WriteObject.Reencode.cs` 的 reencode 分支，
  `WriteCommonEntityData` 之后、MTEXT 特化之前

#### 4. 样条拟合点插入鲁棒性 (`DwgObjectStreamWriter.Hatch.cs`)
- **根因**：`LocateHatchFitInsertsForPreR2007Source` 遇到未知 curveType 时
  `return null` 丢弃已发现的样条锚点 → 样条拟合点 BL(0) 未插入 →
  R2010+ 读取器期望消费 `numFitPoints` 字段 → EndOfStreamException
- **修复**：
  - 未知 curveType 时 `goto doneSegs` 中断当前路径片段遍历，但保留已发现锚点
  - 异常/越界时返回已有锚点而非 null
  - 样条 numKnots/numCtrl 越界时 `break` 而非 `return null`

#### 5. 集中字符串流标志 (`DwgObjectStreamWriter.Carry.cs`)
- **修复**：`CarryWithStringsR2007` 末尾添加 `rec.WroteCentralStringStream = true`
- **原因**：若不设置，`FinalizeObjectFrame` 在 `StringFields.Count == 0` 的
  边缘情况下可能追加多余的 `has_strings=0`，覆盖已写入的 `has_strings=1`

### 已知未解决问题

1. **MgF2→AC1015 的 1 个源侧 HATCH 缺陷**
   - HATCH 17C9A 的 polyline path 存在 bulge BD 消费位偏移不一致
   - 源文件同版本 reload 也是 errors=1，属源文件固有缺陷
   - 测试断言应放宽为 `after.Errors <= before.Errors`

2. **跨 R2007 边界的渐变名集中字符串流**
   - R2007+ 目标下渐变名走集中字符串流，与读取端 `ReadTV()` 的
     `StringStream` 重定向交互已在逻辑层面验证正确
   - 缺少实际 DWG 测试文件验证（测试数据文件不在当前代码库中）

### 文件变更清单

| 文件 | 变更内容 |
|------|----------|
| `DwgObjectStreamWriter.Carry.cs` | `WriteHatchGradientIfUpgrade`（渐变块升级插入）、`HatchGeometryWindow`（降级丢弃）、`CarryWithStringsR2007` 末尾设置 `WroteCentralStringStream` |
| `DwgObjectStreamWriter.Hatch.cs` | `LocateHatchFitInsertsForPreR2007Source` 鲁棒性增强（未知 curveType 不丢弃锚点） |
| `DwgObjectStreamWriter.WriteObject.Reencode.cs` | reencode 分支中调用 `WriteHatchGradientIfUpgrade` |
| `DwgObjectRecord.cs` | 新增 `HatchGradientEndBit`、`HatchGradientName`、`HatchPatternSlotBit`、`HatchPatternName` 字段 |
| `HatchEntity.cs` | `ReadBody` 中捕获 `HatchGradientEndBit`、`HatchGradientName`、`HatchPatternName` |
