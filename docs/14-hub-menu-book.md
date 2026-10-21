# 14 Hub 菜单：立体书（Soft Low-poly Pop-up Book）

版本：v0.6 · 2026-10-21 · 状态：灰盒的**开场、立体件、翻页、书签、开始按钮、每页粒子**都能用：编辑器 Play 里用 MCP 测过（开场 → 书签跳到第 3 页 → Begin 加载 T5_Eating），未上头显、未用真手戳过。

## 1. 已确认的决定

| 问题 | 决定 |
|---|---|
| 形式 | 书平放在桌面上，立体场景长在书页上，翻到这一页时从纸面弹出（不是封闭的房间） |
| 页面 | **一个左右页（spread）= 一个任务**；第一页是琴房（T2） |
| 概念图 | v4（简化、浅粉窗帘）：`Art ref/Menu/concept/menu_concept_v4_music_room.png`，Figma `fnupP6kEzs5Z5mMhjKOyJL` node 45:2；v1–v3 也在同一文件 |
| 视角 | 头显视角从不移动；书在座位正前方 |
| 动画驱动 | 代码驱动（协程 + `Tween`），不用 Animator；以后每页的立体件用一个 0–1 的展开进度带动所有铰链 |
| 制作顺序 | ① 翻页 ✔ → ② 立体件铰链 + 开场 ✔ → ③ 书签 / 开始按钮 ✔ → ④ Blender 模型 → ⑤ 头显实测 |

## 2. 开场（约 3.5 秒，只播一次）

2026-10-21 用户觉得开书太繁琐：**去掉合上的书和开书**，书一开始就是打开的（空白书页），直接从翻页开始。

| 时间 | 动作 |
|---|---|
| 0–0.5 s | 书摊开，书页空白 |
| 0.5–1.4 s | 一张纸从右往左翻过去（翻页声），落到第一页 |
| 1.4–3.2 s | 文字淡入，第一页的立体件按顺序立起来，粒子小爆发 |
| 3.2–3.8 s | 书签依次滑出，Begin 火漆章弹出；之后才能操作 |

参数：`playIntro`（关掉 = 一开始就显示第一页）、`introDelay`、`textSeconds`；翻页和立起的时长沿用 `turnSeconds`、`unfoldSeconds`。

## 3. 立体件

每个会立起来的件是一个铰链（`PopUpHinge`），轴心放在折线上，件挂在它下面。整页只有一个进度 0–1（`PopUpSpread.progress`），每个铰链只在自己那一段进度里动：

| 件 | 折线 / 倒向 | 进度区间 | 回弹 |
|---|---|---|---|
| 拱窗背景板 | 底边，向前倒（面朝下） | 0–0.45 | 轻 |
| 两侧小拱窗 | 背景板左右边，折到背后；打开时朝座位转 25° | 0.15–0.5 | 轻 |
| 钢琴 | 前底边，向前倒 | 0.25–0.65 | 中 |
| 琴凳 | 前底边，向前倒 | 0.4–0.75 | 中 |
| 盆栽（左页） | 向左外侧倒 | 0.5–0.85 | 中 |
| 花（右页） | 向右外侧倒 | 0.55–0.9 | 中 |
| 音符 | 向前倒 | 0.7–1 | 最弹 |

立体的件（钢琴、花盆）倒下时同时压扁成薄片，像纸板一样贴在纸上；进度为 0 时整页的件都隐藏，避免和纸面闪烁。翻页时进度倒着走，件就按相反顺序折回去。完全打开后，叶子、花和音符轻轻摆动（各自 3–5°、周期 2.6–4.2 s），折叠时停下。

在 Inspector 里选中某页的 `PopUp`，拖 `Progress` 滑条就能预览；每个铰链的角度、区间、回弹、压扁比例都在 `PopUpHinge` 上。

只有琴房这一页有灰盒模型；其他三页暂时是一块背景板 + 一个彩色方块。

## 4. 翻页怎么做

一次翻页约 3.3 秒：

1. 立体件折回纸面（0.6 s），同时要被翻起的那一页上的文字淡出（0.2 s）。
2. 一张纸（`PageLeaf`）盖在那一页上，颜色和那页一样；下一页的纸和内容已经在它下面。纸绕书缝翻到另一边（0.9 s，先慢后快再减速）。纸由 4 条铰链连着的窄条组成：抬起时外边缘先走，落下时外边缘落后，所以会弯。
3. 落下后，把它盖住的那页换成新一页，纸隐藏，文字淡入（0.3 s），新一页的立体件立起来（1.8 s）。

同一时间只显示一页的内容，所以两套立体场景永远不会穿插。翻页时有一声代码合成的纸张"刷"声（`MenuSounds.Flip`）。

## 5. 书签和开始

| 元素 | 位置 | 操作 | 反馈 |
|---|---|---|---|
| 书签（每个任务一个：Piano / Hand / Meal / Bottle，任务色） | 右页块外侧，伸出 4 cm、深 6 cm，平躺，第一页离座位最远 | 从上往下戳 = 翻到那一页（不管隔几页，都只翻一张纸） | 手指靠近时抬起 5 mm；当前页的书签一直抬起，并有一圈奶油色边 |
| Begin 火漆章（珊瑚色圆，直径 7 cm） | 书前方正中，和 `<` `>` 同一排，向后仰 30° | 戳 = 开始当前页的任务 | 风铃声，立体件最后几件轻轻点一下头，0.5 s 淡到暖黑，加载任务场景 |
| `<` `>` | 书前方左右 | 戳 = 上一页 / 下一页 | — |

键盘测试：← / → 翻页，1–4 跳页，Enter = Begin。场景没进 Build Settings 时，Begin 只点头不跳转，并在 Console 里警告。

生成场景时，`Hub_Menu` 和 4 个任务场景会追加到 Build Settings 末尾（不改第一个启动场景）。任务场景里暂时没有回到菜单的按钮。

## 6. 动效（简洁、温馨）

每页有自己的粒子，量少、慢、柔和，只在这一页立着的时候出现：

| 页 | 粒子 | 颜色 |
|---|---|---|
| 琴房（T2） | 音符 | 蜂蜜黄 / 珊瑚 / 鼠尾草绿 |
| 牵手（T1） | 小爱心 | 珊瑚 / 两种浅粉 |
| 吃饭（T5） | 热气团（慢慢变大、变淡） | 白 / 奶油 |
| 拧瓶盖（T11） | 气泡（细圈 + 小高光） | 浅蓝 / 白 |

- **漂浮**：立体件全部立好后开始，每秒 1–2 个从立体件后面慢慢往上飘，带一点左右晃动，3–4.5 秒淡出；同时最多 20 个。翻页开始时停止发射，已有的自然飘走。
- **小爆发**：立体件刚立好时（开场、每次翻页后）和按 Begin 时，同款粒子往上撒一次（8–12 个，1.2–1.6 秒落下淡出）。
- **Begin 火漆章**：等待时轻轻"呼吸"（放大 3%，2.4 秒一次）。
- 书签靠近抬起、植物摆动、翻页声都保持不变。

粒子图是 128 px 白色遮罩，Unity 里上色：音符用 T2 的 `icon_note`，爱心 / 热气 / 气泡由 `art/scripts/make_hub_particles.py` 生成（`Assets/Learning/Art/UI/Hub/`）。


## 7a. 书的模型（2026-10-21）

用户的 Blender 书 `/Users/xuzihan/Documents/Model/book.blend`（AI 生成的书 + 自己做的弧形纸面）已接入：

| 文件 | 内容 |
|---|---|
| `Assets/Art/Props/Hub/Hub_BookOpen.fbx` | 打开的书：`Book_R`、`Page_R`、`FrontHalf`（→ `Book_L`、`Page_L`，转好 180°）、`Ribbon`。纸面是拱起来的 |
| `Assets/Art/Props/Hub/Hub_BookClosed.fbx` | 合上的书（新做，大小和打开的书一样）：`Closed_Base`、`Closed_Ribbon`、`ClosedFront`（封面轴）→ `Closed_Top` |
| `Assets/Art/Props/Hub/Textures/` | 从 FBX 解出的贴图（书、纸面、页边条纹） |

- 两本书在 Unity 里都绕 Y 转 180°（Blender 的前方导出后在 Unity 的 +Z），缩放 0.556，一页 0.246 × 0.364 m。纸面高度：书缝 31 mm，拱顶 54 mm，外缘 39 mm。
- 合上的书（`Hub_BookClosed.fbx`）和开书动画已去掉（见第 2 节）；FBX 还留在项目里，场景不再用它。
- **贴着弧形纸面**：生成场景时在两页纸面上临时加网格碰撞体，往下打射线量高度（用完删掉）：立体件的折线放到它脚下纸面的最低点；标题文字跟着纸面斜一点、抬到刚好不穿；书签、粒子按纸面高度放。
- **翻动的纸**：8 条，静止时弯成右页的弧度，翻到左页时镜像，翻到一半时变平；每条显示纸面贴图对应的一段（花边会跟着翻）。
- 没有模型文件时，生成器自动退回灰盒。

## 7. 代码

| 文件 | 作用 |
|---|---|
| `Assets/Learning/Scripts/Hub/PopUpBook.cs` | 每页一个 `Spread`（任务名、场景名、左右纸色、左右文字 `FadeGroup`、立体件 `PopUpSpread`）；开场、`Next / Prev / GoTo / Begin`、翻页流程，书签和 Begin 的出现，每页粒子（`ambient` / `burst`），火漆章呼吸，`Opened` / `Turned` / `Began` 事件；键盘测试 |
| `Assets/Learning/Scripts/Hub/BookTab.cs` | 书签：戳 = 跳页，靠近抬起，当前页有边，打开后滑出 |
| `Assets/Learning/Scripts/Hub/PopUpHinge.cs` | 一条折线：轴、倒下 / 立起角度、进度区间、回弹、倒下时的压扁比例 |
| `Assets/Learning/Scripts/Hub/PopUpSpread.cs` | 一页的进度、`Animate(目标, 秒)`、完全打开后的摆动 |
| `Assets/Learning/Scripts/Hub/PageLeaf.cs` | 翻动的那张纸：`Pose(角度, 弯曲)`；Inspector 里拖 `previewAngle` / `previewBend` 可以直接预览 |
| `Assets/Learning/Scripts/UI/MenuSounds.cs` | 新增 `Flip()` |
| `Assets/Learning/Editor/LearningSceneBuilder.Hub.cs` | 菜单 **HexRMeta > Learning > Build Hub Menu (grey box)** → `Assets/Learning/Scenes/Hub_Menu.unity` |

灰盒尺寸：一页 25 × 33 cm，页块厚 2.2 cm，封面 4 mm。4 个任务页：Piano time（T2）、Hold a hand（T1）、Mealtime（T5）、Open a bottle（T11），每页纸带一点不同的底色，方便看清翻的是哪张。左页前方印标题，右页前方印页码。书前方一排：`<`、Begin、`>`（向后仰 30°）。右页块外侧 4 个书签。

可调参数（`PopUpBook` 的 Inspector）：开场 `playIntro`、`introDelay`、`textSeconds`；翻页 `foldSeconds`、`fadeOutSeconds`、`turnSeconds`、`unfoldSeconds`、`fadeInSeconds`、`bendDegrees`；菜单 `tabSeconds`、`tabStagger`、`beginFadeSeconds`、`sealPulse`、`sealPeriod`；`startSpread`。粒子的数量、速度、大小在各页 `Particles_Drift` / `Particles_Burst` 的 ParticleSystem 上调。

## 8. 已知限制 / 下一步

- 翻动的纸只有纸色，没有印刷内容；等有了印刷贴图，再把贴图按窄条切开贴到纸上，这样翻的时候能看到原来那页的内容。
- 还没有：任务结束后回到菜单（并翻到刚才那页）。
- 书签平躺、从上往下戳：桌面对齐后要在头显里确认手指不会先碰到真实桌面（书签比桌面高约 2.5 cm）。
- `<` `>` 还是 Meta 示例按钮的灰色外观。
- 件在刚开始立起、快倒平时会有几毫米穿进纸面（实心灰盒的厚度）；换成 Blender 的纸板模型后会好很多。
- 精细模型：交付清单 `docs/art/hub-model-checklist.md`（逐个 FBX 打勾替换），设计细节 `docs/art/hub-book-brief.md`（火漆章、书签、书本身、琴房各件、第 2–4 页建议）。
- 粒子帧率要在 Quest 上实测（4 页合计最多 8 个粒子系统，但同时只有 1 页在发射）。
- 书没有和真实桌面做对齐（沿用模板桌子）；T2 的对齐方法以后搬过来。
