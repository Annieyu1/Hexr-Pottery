# 13 T2 弹琴：菜单（暖色、小动画）

版本：v0.1 · 2026-10-21 · 状态：M1 骨架 + M2 动效 + M3 特效已写好并能编译，场景已重建；编辑器里看过标题页；**未在头显、未接手套测试**。

设计稿：Figma 文件 `fnupP6kEzs5Z5mMhjKOyJL`（HexR Learning UI）的 **"T2 Piano Menu"** 页，9 个画板（总览、标题、选层次、选曲、准备、演奏、完成、治疗师设置、动效说明）。

## 1. 已确认的决定（2026-10-21）

| 问题 | 决定 |
|---|---|
| 菜单位置 | **A：浮在琴上方**（书桌中间，琴后上方约 25 cm，向后仰 16° 对着坐着的眼睛），健侧手够得到 |
| 风格 | 温暖温馨：奶油纸色卡片、蜂蜜色主按钮、珊瑚 / 鼠尾草绿点缀；青色只用于提示（发光琴键、指尖光点） |
| 动效 | 小动画的感觉，全部在世界空间里，**头显视角从不移动**；换场景只在 0.3 s 淡暗里瞬切 |
| 旁观相机 | 不做 |
| 治疗师按钮 | 原来 5 排按钮全部收进齿轮设置页（按住齿轮 2 秒打开） |

保留 T1 界面文件里的 Quest 规则：实心不透明面板，不用模糊 / 柔和阴影（"阴影"是卡片下方 8 px 的实色底板），只用 Inter Bold / Regular，1 设计 px = 0.5 mm，可戳目标 ≥ 6 cm。

## 2. 流程

```
① 标题 Piano time  ──Start──▶ ② 选层次 Listen / Learn / Play ──▶ ③ 选曲（左右切换、Hear it 试听、Back）
                                                                     │ Play this song
                                                                     ▼
⑥ 完成 Congratulations ◀── 演奏（只剩步骤卡片 + Pause / Skip）◀── ④ 准备（校准，淡暗对齐桌面，琴键流水灯）
   Again / Other songs / Menu
⑦ 设置（按住齿轮 2 s）：发光 / 振动 / 琴声开关，振动强度，速度，重新校准
```

- **L1 准备步骤很宽松**（2026-10-21）：卡片文字仍是 "Rest your hand on the keys / Relax, keep still"；手放到琴上，只要稳 1 秒（手掌晃动 4 cm 以内都算）就把琴移到手下、开始自动弹；6 秒内都没稳住也照样开始（用当时手的位置；一直没追踪到手就保持琴原来的位置）。L1 不需要逐指按一下。弹奏时琴键和指尖发光、琴声、手指按压同时出现（各自在设置页可关）。参数：`listenRestSeconds`、`listenStillMeters`、`listenGiveUpSeconds`。L2 / L3 仍是静止 3 秒（1.5 cm）+ 逐指校准。
- **手离开琴就暂停**（2026-10-21，L1 / L2 / L3 都适用）：演奏中手掌离开它在琴上的位置超过 8 cm 达 0.6 s（或追踪丢失 1 s），弹奏自动暂停，卡片显示 "Rest your hand on the keys / Put it back on the ghost hand"，并在原位置显示半透明青色的**幽灵手**（轻微呼吸闪烁）；手回到幽灵手 5 cm 以内保持 0.5 s，自动继续。手的位置在校准时记下（跟着琴走）；校准前用默认位置（四个指尖落在 C–G 键上）。准备步骤里也显示同一只幽灵手，告诉患者手放哪里。参数：`pauseWhenHandLeaves`、`leaveDistance`、`leaveSeconds`、`lostSeconds`、`returnDistance`、`returnSeconds`。治疗师也可以在暂停时直接按 Resume。
- 从菜单开始时，L1 / L2 **只弹选定的那一首**，弹完回到完成页（`PianoTask.oneSongPerRun`，默认开）。L3 同前。
- 完成页：L2 / L3 显示正确率（第一次就按对的音 / 全部音）和用时；L1 显示"感受到的音数"和用时。星星：≥ 50% 一颗、≥ 75% 两颗、≥ 90% 三颗（L1 三颗）。
- 演奏中按 End（暂停后出现）提前结束 → 回到选层次页，不显示完成页。

## 3. 动效

| 动作 | 做法 |
|---|---|
| 卡片飞入 | 从琴后方（下 25 cm、远 15 cm）飞到位，0.45 s，ease-out-back，每个元素间隔 0.08 s，缩放 0.6 → 1，同时淡入 |
| 选择 | 选中的卡片放大到 108%、亮出蜂蜜色描边、轻"啵"一声；其余两张后退缩小；然后整屏淡出 |
| 换曲 | 卡片向箭头方向滑出 4 cm 再弹回，播放前几个音（只有琴声，无触觉） |
| 齿轮 | 按住时齿轮转动，满 2 s 打开设置 |
| 准备 | 对齐真实桌面时 0.25 s 淡到暖黑、移动视角、0.35 s 淡回；随后 5 个琴键从左到右亮一遍 |
| 完成 | 星星和音符从琴里迸出（粒子），琴键流水灯，C-E-G-C 风铃声，星星依次弹出 |
| 标题氛围 | 音符从琴后慢慢飘起（标题、选层次、选曲时有；演奏时关掉，不分散对触觉的注意） |

声音全部在代码里合成（`MenuSounds`），没有外部音频资源。

## 4. 代码

| 文件 | 作用 |
|---|---|
| `Scripts/Tasks/Piano/PianoMenu.cs` | 菜单流程、飞入 / 飞出、选择动画、换曲、完成页、设置页、齿轮长按；键盘：Enter 主按钮、1/2/3 选层次、←/→ 换曲、Backspace 返回、G 设置 |
| `Scripts/UI/Tween.cs`、`FadeGroup.cs` | 缓动函数；整组 Sprite + TMP 一起淡入淡出 |
| `Scripts/UI/ScreenFade.cs` | 眼前的暖黑淡出球（隐藏视角移动） |
| `Scripts/UI/MenuSounds.cs` | 啵、滴答、打开、风铃 |
| `Scripts/Tasks/Piano/PianoTask.cs` | 新增：一首一轮、`RunEnded` 事件和 `PianoRunResult`、`KeyWave()`、校准时用淡暗包住视角移动 |
| `Scripts/UI/StepCard.cs` | 新增 `accent` / `accentTint` 颜色（默认仍是 T1 的青色；T2 用蜂蜜色） |
| `Editor/LearningSceneBuilder.T2Menu.cs` | 按 Figma 生成所有页面、暖色 Pause / Skip、暖色步骤卡片、粒子、淡出球 |
| `Assets/Learning/Art/UI/T2/icon_*.png` | 13 个图标（白色遮罩，Unity 里上色），由 `art/scripts/make_t2_icons.py` 用 Blender 从 SVG 形状渲染 |

## 5. 已知限制 / 待做

- 只在编辑器里看过标题页；其他页面的布局、按钮手感要在头显里确认（尤其戳卡片时的误触，卡片和"Play this song"按钮分开放了）。
- 患侧手的切换（左 / 右）没放进设置页：换手要重建场景（`LearningSceneBuilder.DefaultHand`）。
- 粒子数量很少（同时最多 24 / 40 个），Quest 上帧率要实测。
- 2026-10-21 在编辑器 Play 时 Unity 崩溃过一次，崩溃在 Meta OpenXR 插件（`OVRPlugin` 的 `xrPollEvent`），发生在用 MCP 截图强制渲染时，不在菜单代码里；之后截图尽量在编辑模式下做。
- 乐谱架框、手套外观仍未做（见 `art/t2-piano-brief.md`）。
