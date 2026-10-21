# 模型方案：Hub 菜单书房页（纸艺立体书风格）

版本：v0.2 · 2026-10-21 · 状态：**等角低面数版已建好并导出**（3 234 三角面），还没放进菜单
概念图：`Art ref/Menu/concept/menu_concept_studyroom_v1.png`（Figma `fnupP6kEzs5Z5mMhjKOyJL` node 50:2）
等角（isometric）低面数折纸版：`Art ref/Menu/concept/menu_concept_studyroom_iso_v1.png`（Figma node 51:2）：两面墙在后角相交，大块面、清晰折痕、无细碎装饰
风格参考：琴房模型 `Hub_MusicRoom.fbx`（3 mm 卡纸片层叠、金色细边、卷草花饰和尖顶、奶油 + 暖色）
内容参考：我们现有的书房 `HexR_StudyRoom.blend`（Pressure_Demo_Room / T2 用的那间）

## 1. 一句话
把现有书房按琴房的纸艺风格做成一页立体书场景：书桌在正中，左边书柜和落地灯，右边窗和唱片机，后墙挂画。放在菜单的一页上，打开这一页时按顺序立起来。

## 2. 尺寸和规则（和琴房一致，生成器可以直接用）
- Blender 里约 **1.0 m 宽 × 0.6 m 深**，墙高约 0.5 m（加顶饰 0.58 m）；Unity 里缩放 0.45。
- 患者在 −Y 一侧；地板底面 z = 0；1 单位 = 1 m。
- 每块"纸"是 3 mm 厚的平板，倒角 1 段；花饰是 2D 轮廓挤出，不用细分曲面。
- **整页 ≤ 8 000 三角面**（琴房 14.4 万的教训），材质用 `Paper_*` 纯色（同琴房命名），只有挂画用一张小贴图集。
- 不放相机和灯。

## 3. 零件和分组（名字固定，生成器按名字分组做铰链）

| 分组（铰链） | 零件 | 立起方式 | 约面数 |
|---|---|---|---|
| `Floor` + `Rug`（舞台） | 蜂蜜木地板卡 + 金色细边；粉彩方格地毯（鼠尾草、桃、浅蓝、奶油、粉） | 从中心展开 | 300 |
| `Hinge_Back`（后墙） | 奶油墙板 + 墙裙线 + 波浪顶饰和金色鸢尾尖顶；5 幅纸片挂画（太阳山丘、知更鸟、蛋糕、鹅、草莓）；墙架（书、小盆栽、蜡烛） | 绕后底边立起 | 2 000 |
| `Hinge_SideR`（右侧墙） | 侧墙板 + 顶饰；木框窗、半放下的纸条百叶、浅蓝天空卡 | 随后墙立起 | 900 |
| `Desk` | 书桌（桌面、细腿、两个抽屉）；台灯（鼠尾草绿）、一摞书、杯子、笔筒、一张纸 | 绕前底边立起 | 1 300 |
| `Chair` | 木椅 + 鼠尾草绿坐垫 | 绕前底边立起 | 400 |
| `Bookshelf` | 高书柜（彩色书脊、小盆栽、收纳盒） | 向左外侧立起 | 1 400 |
| `FloorLamp` | 金色细杆 + 纸灯罩 | 立起后轻摆 | 300 |
| `SideTable` | 小边桌 + 浅青色唱片机 | 向右外侧立起 | 700 |
| `Plant` | 白色多面盆 + 5–6 片大纸叶 | 立起后轻摆 | 400 |

合计约 7 700。

## 4. 色板
奶油墙 `#F3E3C8`、蜂蜜木 `#C98F57`、深木 `#8A5A3B`、鼠尾草绿 `#8FAE6A`、灰青蓝 `#6F9EA8`、柔珊瑚 `#E58E73`、金色细边（同琴房 `Paper_Gold`）。

## 5. 产出
- 源文件 `art/blender/HexR_Hub_StudyRoom.blend`，用脚本生成（`art/scripts/build_hub_studyroom.py`），便于反复调整。
- 导出 `Assets/Art/Props/Hub/Hub_StudyRoom.fbx`；生成器像琴房一样按上表分组、放到书页上。

## 6. 已建（2026-10-21）

用户选了等角低面数折纸版，并要求：**桌面放大、桌上放两个网球**。

- 脚本 `art/scripts/build_hub_studyroom.py`（Blender 后台运行，不动打开着的 Blender）→ `art/blender/HexR_Hub_StudyRoom.blend`、`Assets/Art/Props/Hub/Hub_StudyRoom.fbx`，预览 `docs/art/preview/hub/studyroom_iso.png` / `studyroom_front.png`。
- 地板 0.62 m 见方，墙在西侧（左）和北侧（右），在左后角相交；进菜单时整体转 45°，让墙角朝书的后方。
- 书桌 0.34 × 0.20 m（原 0.3 × 0.17），桌上：两个网球（多面球 + 白色缝线环）、台灯、两本书、杯子。
- 三角面：地板 116、地毯 62、墙（含挂画、墙架、窗）704、书桌 480、椅子 308、书柜 1 100、落地灯 60、边桌和唱片机 360、绿植 44，**合计 3 234**。
- 物体分组名：`Floor`、`Rug`、`Hinge_Back`、`Desk`、`Chair`、`Bookshelf`、`FloorLamp`、`SideTable`、`Plant`，根物体 `StudyRoom_Paper`。
