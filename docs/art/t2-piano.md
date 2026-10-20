# T2 弹琴：模型交付说明

版本：v0.2 · 2026-10-21 · 状态：**已接进 T2 场景**（场景重建后用的是这个模型；编辑器里测过，未上头显）
需求见 `t2-piano-brief.md`；任务逻辑见 `../12-t2-piano-three-levels.md`。

## 1. 文件

| 文件 | 内容 |
|---|---|
| `art/blender/HexR_T2_Piano.blend` | 集合 `T2_Keyboard`、`T2_Props`（书堆、小猫）、`Preview_Only`（预览桌面、指尖位置标记、灯、相机，不导出） |
| `Assets/Art/Props/T2/T2_Keyboard.fbx` | 薄桌面琴 |
| `Assets/Art/Props/T2/T2_MusicBooks.fbx`、`T2_CatFigurine.fbx` | 桌面小道具 |
| `art/textures/t2/Palette_Base.png` | 外壳、黑键、道具的调色板图集（FBX 内嵌） |
| `art/scripts/build_t2_piano.py` | 生成 .blend（无界面） |
| `art/scripts/export_t2.py` | 导出 FBX（无界面，不碰打开的 Blender） |
| `docs/art/preview/t2_piano_seat.png` / `_front.png` / `_close.png` | 预览渲染 |

重做：`Blender -b --factory-startup --python art/scripts/build_t2_piano.py`，再 `Blender -b art/blender/HexR_T2_Piano.blend --python art/scripts/export_t2.py`。
（Blender 4.5 退出时会打印一条 `list.remove(x)` 错误，是退出时的插件清理，不影响输出。）

## 2. 层级（`T2_Keyboard.fbx`，导回 Blender 实测）

```
T2_Keyboard            根空物体。原点 = 键 E4 中线、白键顶面、白键前沿往里 35 mm（指尖线）
├─ Key_C … Key_G       可弹的 C4–G4，各自一个物体，共用网格 T2_WhiteKey
│                      原点 = 铰链（后沿、顶面、居中）：x = −0.044 / −0.022 / 0 / 0.022 / 0.044，y = 0.095，z = 0
│                      只有一个材质槽 T2_KeyWhite
├─ T2_WhiteKeys_Static 其余 24 个白键（材质 T2_KeyWhite）
├─ T2_BlackKeys        20 个黑键（调色板）
└─ T2_Case             外壳：底板、低前沿、两侧挡板、后面板（毛毡条、屏幕、2 个旋钮、8 个预设按钮、音箱槽）（调色板）
```

患者在 Blender −Y 一侧看 +Y（和书房、厨房一致）；Blender +Z = Unity +Y。

## 3. 尺寸（米，实测）

| 项 | 值 |
|---|---|
| 整台 | 宽 0.668 × 深 0.180；键顶以下 0.012（底面 z = −0.012），后面板最高 +0.025（旋钮） |
| 白键 | 间距 0.022，宽 0.0205，长 0.13（y −0.035 … 0.095），厚 0.010 |
| 黑键 | 宽 0.012（顶面收窄），长 0.08（y 0.015 … 0.095），z −0.003 … +0.008，按真琴左右错开 |
| 前沿 | y −0.045 … −0.035，顶面 z = −0.005（低于键顶） |
| 键数 | **C2–C6 四个八度，29 白 + 20 黑**（2026-10-20 改大）；可弹的 C4–G4 在中间略偏右（E4 在原点，外壳左边到 −0.378，右边到 +0.290） |
| 三角面 | 琴 3216；书堆 252；小猫 740 |

## 4. Unity 导入（2026-10-21 已做）
- 材质已提取到 `Assets/Art/Props/T2/Materials/`（`M_Palette`、`T2_KeyWhite`），三个 FBX 都重映射到这两个材质；`T2_KeyWhite` 光滑度调到 0.35。
- 贴图提取到 `Assets/Art/Props/T2/Textures/Palette_Base.png`，**Point 过滤、无 Mipmap、不压缩**。
- 发光材质 `Assets/Learning/Materials/T2_KeyLit.mat`：浅青色 + 青色自发光（和 UI 的 `#00B4C7` 一致），由构建器生成。

## 5. 在 T2 里怎么用（2026-10-21 已做）
- 构建器（`LearningSceneBuilder.T2.cs`）发现 `T2_Keyboard.fbx` 就用模型，找不到才用白模。模型放在 `Keyboard/Model` 下，`Model` 绕 Y 转 180°（Blender −Y 朝患者，导入后要转半圈）。
- `PianoKeyboard` 模型模式：`modelRoot` = `Model`，按患者指距把整个模型在 X 方向缩放 `spacing / 0.022`，只有 `Key_C`…`Key_G` 会转；按下角度 3°；按键方向按每个键的实际朝向自动判断。
- 琴键位置：琴**垂直于手指方向**摆放（不按指尖连线，指尖本来就是弧形，拇指更靠后），键距和键 E 的位置取自除拇指外四个指尖在键线上的投影。
- **对齐真实桌面**：校准的"手放平"一步后，把 VR 相机架（`OVRCameraRig`）上下移动，使患者的真实桌面正好在虚拟书桌高度（琴底贴着书桌）。超过 ±0.3 m 视为追踪错误，不移动；移动量记在 `calibration.json` 的 `desk_shift_mm`。
- 书堆放在琴左后方（治疗师面板后面），小猫在右后方；它们固定在桌上，不跟琴走。
- 键名（C D E F G 或 1–5）用奶油色，衬在琴的深色后面板上。

## 6. 没做的
- 手套外观（B）：要先确认患者追踪到的手能不能换贴图。
- 乐谱架框（C）：等乐谱 UI 定下来再做。
- 书封面上的音符图案是平面拼出来的，很简单；要更精致可以换成贴图。
- 预览渲染只在 Blender 里看过，**还没在 Unity / 头显里看过**。
