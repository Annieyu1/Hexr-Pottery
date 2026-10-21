# 模型说明：Hub 菜单牵手页（T1）——折纸茶花园

版本：v0.1 · 2026-10-21 · 状态：**已建模并导出**（2 536 三角面），还没放进菜单
概念图：`Art ref/Menu/concept/menu_concept_garden_iso_v1.png`（Figma `fnupP6kEzs5Z5mMhjKOyJL` node 52:2）
风格：和书房页一样，等角、低面数折纸（`docs/art/hub-studyroom-brief.md`）

## 1. 内容
花园中央一张小圆桌（奶油色桌面、白色单脚），两边各一个石凳，面对面；桌上两杯茶（白杯、珊瑚色杯碟、纸折热气）、一个茶壶（珊瑚色壶盖）、一盘点心（草莓蛋糕一块、饼干两块）。后两边是绿篱 + 白色尖头栅栏，背角前面一座挂纸玫瑰的拱门，北侧栅栏上一只小鸟；左右两个角各一棵多面体树；左边一丛紫色风信子，右边郁金香和黄色小花；前角一条石板小路通到桌子。

## 2. 文件
- 生成脚本 `art/scripts/build_hub_garden.py`（Blender 后台运行，不动打开着的 Blender）
- 源文件 `art/blender/HexR_Hub_Garden.blend`；导出 `Assets/Art/Props/Hub/Hub_Garden.fbx`
- 预览 `docs/art/preview/hub/garden_iso.png`、`garden_front.png`
- **手改会保留**：脚本创建的物体都带自定义属性 `paper_build`。再次运行时只删掉带这个标记的物体再重建，你自己加的或复制出来没有标记的物体不动。

## 3. 尺寸和分组
- 草地 0.62 m 见方（同书房），绿篱和栅栏在西（左）北（右）两侧，拱门在左后角前；进菜单时转 45°，让这个角朝书的后方。
- 物体分组（Unity 生成器按名字做铰链）：

| 组 | 内容 | 三角面 |
|---|---|---|
| `Floor` | 草地卡、浅色草块、金色细边 | 104 |
| `Path` | 4 块石板 | 112 |
| `Hinge_Back` | 绿篱、栅栏、拱门和玫瑰、小鸟 | 1 108 |
| `Tree_L` / `Tree_R` | 两棵树 | 40 / 40 |
| `Table` | 圆桌、茶壶、两杯茶、点心盘 | 556 |
| `Stools` | 两个石凳 | 96 |
| `Flowers_L` / `Flowers_R` | 风信子 / 郁金香和小黄花 | 220 / 260 |
| 合计 | | **2 536** |

- 根物体 `Garden_Paper`；FBX 导出不勾 "Bake Space Transform"（会把组下面的网格转歪）。
