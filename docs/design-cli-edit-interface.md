# CLI 图纸修改接口（--inspect / --ops）

> 本档由代码事实重建（原档从未入库）；契约以 `DwgParser.Cli/Program.Edit.cs` + `DwgParser.Core/Format/DwgEditor.cs` 为准。

## 定位

机读面的图纸值级改写：供技能/脚本调用，非交互式编辑器。**只做值级改写**（文本/属性/删除/加注），
不做几何编辑；每枚操作的落地与否以报告自证，产物要么全绿落盘要么不留。

## --inspect（候选枚举）

```
dwginfo <文件.dwg> --inspect <候选.json> [--kind <实体类型>] [--contains <子串>] [--limit N]
```

输出每枚候选实体的句柄、类型、当下可跑算子清单（`DwgEditor.InspectOps`：
`TextRejection/AttribRejection/DeleteRejection` 判空则该算子列入）。供人/技能挑句柄后组装操作单。

## --ops（批量改写）

```
dwginfo <文件.dwg> --ops <操作单.json> --output <产出.dwg> --report <报告.json> [--convert <目标版本>]
```

- **必配** `--output` 与 `--report`：不存在原地覆盖源图的通路（缺失即退出码 2）。
- 算子（操作单 JSON 数组元素）：
  | 算子 | 字段 | 语义 |
  |---|---|---|
  | `setText` | handle, text | 改实体文本（MText/Text 族，`TextRejection` 判定可写性） |
  | `setAttrib` | handle, text/tag? | 改 ATTRIB 值 |
  | `delete` | handle | 删实体（含块内），被引用者拒落 |
  | `addMText` | 位置/内容字段 | 新增 MText，报告返回新句柄 |
- **中文值只走文件**（操作单 JSON），不走 argv——避免控制台编码损字。
- `--convert <版本>` 可叠加：一步完成「改 + 跨版本转」。

## 退出码语义

| 码 | 含义 | 产物 |
|---|---|---|
| 0 | 全绿已落盘 | 产出 DWG + 报告 |
| 1 | 有枚未落地或自证不合 | **不留**（产出与报告均不写） |
| 2 | 用法不合（缺参/路径不当） | 无 |

报告 JSON（`EditReport`）逐操作记录 `op / handle / status / why`；`why` 来自 DwgEditor 的具名拒绝
理由（句柄不存在、类型不符、被块引用拒删等），是机读的失败原因字段，不进 stderr 散文。

## 实现指针

- CLI 编排：`DwgParser.Cli/Program.Edit.cs`（OpsMode / InspectMode）
- 语义与拒绝判据：`DwgParser.Core/Format/DwgEditor.cs`（Apply / 四算子 case / 各 Rejection）
- 报告结构：`DwgEditor` 内 `EditReport`（Outcomes 逐枚 + Written 总旗标）
