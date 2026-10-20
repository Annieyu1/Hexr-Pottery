# 11 学习阶段 T1 牵手 / T2 弹琴 / T5 吃饭 / T11 拧瓶盖:实现说明

版本:v0.2 · 2026-10-19(T1 改为分步流程)· 状态:白模可运行(编辑器 + Link 手部追踪测过,T5、T11 整轮跑通;**未在头显独立运行、未接手套测试**)

设计依据见 `10-two-phase-sensory-reeducation.md` §5。本文件只讲怎么用、改哪里。

## 1. 场景

| 场景 | 内容 |
|---|---|
| `Assets/Learning/Scenes/T1_HandHold.unity` | T1 牵手 |
| `Assets/Learning/Scenes/T2_Piano.unity` | T2 跟着音乐弹琴 / 数手指 |
| `Assets/Learning/Scenes/T5_Eating.unity` | T5 吃饭(勺子) |
| `Assets/Learning/Scenes/T11_OpenBottle.unity` | T11 拧瓶盖 |

- 两个场景都由模板 `Assets/Scenes/1.unity` 复制而来(OVR 相机、HexR Main、Pressure Controller、房间和书桌都在),**模板本身没改**。
- 复制后关掉了教程物体 `Game/InfoFrames`、`Game/Interactables`(只是隐藏,没删)。
- 新内容都在一个根物体下:`Learning_T1` / `Learning_T2` / `Learning_T5` / `Learning_T11`。以后可以整个拖进书房/花园场景。
- 重建:菜单 **HexRMeta > Learning > Build T1 Hand Hold Scene / Build T2 Piano Scene / Build T5 Eating Scene / Build T11 Open Bottle Scene**(会覆盖同名场景,手改的内容会丢)。
- 两个场景**还没加进 Build Settings**。

## 2. 运行

- 进入 Play 后按 **Start**(VR 里的戳按钮)或键盘 **空格** 开始;空格 = 暂停/继续,**A** = 切换辅助级,**Esc** = 结束。
- 辅助级(docs/10 §5.7):`SeeAndHear`(指尖发光 + 力度条/键名 + 声音)→ `HearOnly`(只有声音)→ `TouchOnly`(画面变暗、无声音,只剩触觉)。
- 模式:`Watch`(L-看,隐藏追踪到的真手,只看虚拟手)、`Follow`(L-跟,T2 里虚拟手变半透明,真手可见,不评分)。在任务组件的 Inspector 里改。
- 默认每个任务 4 分钟(`minutes`),一轮做完才停。**T1 例外**:不按时间,按步骤走,患者按 Finish 结束。
- T1 键盘:**2 / 3 / 4** = 作答,**R** = Feel again,**M** = One more,**F** = Finish,**S** = Skip step。
- 状态行(按钮上方)显示:任务、状态、轮次、时间、辅助级、手套是否连接。

## 3. 每轮内容

**T1 牵手**(组件 `HandHoldTask`,2026-10-19 改为分步流程,去掉了孩子的手;用患者自己的 VR 手):
- **Step 1 · 握住手**:模型手以握手姿势悬在桌面**等待垫**上方并**招手**(手指从食指到小指依次缓慢张开、合拢,周期约 2.4 s)。同时显示**引导**:一只浅青色半透明、缓慢呼吸的**幽灵手**(患者的右手,握手姿势,掌心对着模型手),上方一个上下浮动的**箭头**指向它。患者握住后引导消失。患者的手掌靠近模型手(< `grabDistance` 8 cm)**并且握拳**(四指平均弯曲 > `gripCurlDegrees` 50°)时,模型手绑定到真手上并跟着移动。(原来"6 s 后模型手自己过来"的辅助已去掉,见下方界面说明。)
- **Step 2 · 轻握**:先预告"我们会轻轻握你的手",然后轻握 0.25,让患者感受 5 s。
- **Step 2.1 · 回握**:提示"用同样的力握回去",持续 5 s。手套**测不了患者的力**,这一步只把这段时间的手指平均弯曲角度记进日志。
- **Step 3 / 3.1**:流程和 Step 2 / 2.1 一样,换成紧握 0.65。
- **Step 4.n · 数次数**:以轻握为底,捏 2/3/4 下,每次随机是轻(0.4)还是重(0.7),1 Hz。6 种组合放进一个洗牌袋,抽完再重洗。捏完后浮空按钮 **2 / 3 / 4** 出现,患者用手戳按钮作答(按钮放在健侧,所以患侧手可以一直握着),然后显示绿色"Correct!"或红色"Not quite. It was N",并播放提示音。
- 做满 `minQuizRounds`(5)轮以后,每轮结束会出现 **One more / Finish** 两个按钮;按 Finish 显示 **Congratulations!**。
- **中途松手**(Step 2–4):四指弯曲 < `releaseCurlDegrees` 35° 持续 0.4 s,就判定为松手。这时模型手放开、**滑回等待垫**,再招手并重新显示幽灵手和箭头,气囊放气,字幕提示"再握住"。重新握住后**接着当前步骤**,不回 Step 1;计时只在握住时走。捏的过程中松手的话,重新握住后**重捏一遍**。作答时松手不提示。
- 手丢失追踪按松手处理(模型手同样滑回等待垫)。
- **界面(2026-10-19,按 Figma "HexR Learning UI – Step Card" 的 Task 1 + Page 1 通用元素)**:操作区上方一张步骤卡片(步骤标签、手形图标、标题、提示、5 段进度条),代替原来的字幕、步骤标题和对错面板。卡片文案:Step 1 Reach for the hand;Step 2 A gentle hold;**Step 2-1 / 3-1** 提示语改为 "Now hold back with the same strength";Step 3 A firm hold;Step 4 Count the squeezes(提示语后加 "· Round x");作答 How many squeezes?;答对绿色 "Yes, N squeezes / Well done";答错红色 "It was N squeezes / Let's feel them again"(答错后把正确次数再捏一遍,不再作答,直接进下一轮);Step 5 Well done!。每步结束卡片变成 1 s 绿色 "Done"。
- 作答面板在健侧:2 / 3 / 4 和 **Feel again**(同样的次数和轻重再捏一遍,然后回到作答)。满 5 轮后同一位置出现 **One more / Finish**(设计稿里没有,按同样的胶囊按钮样式补上)。卡片上不显示答对数(设计规范:学习阶段没有分数)。
- **不再自动辅助**(设计规范 "Nothing moves by itself"):去掉了"6 s 后模型手自己过来"。没握住手超过 `tipAfterSeconds`(8 s),出现黄色 Tip 卡("Put your hand where the ghost hand is"),卡片加黄色描边;握住后消失。日志记 `tip_shown`。
- 治疗师按钮按设计稿:Start、Pause、Hints(= 切换辅助级)、**Skip step**(跳过当前步骤或当前轮,日志记 `skip_step`);暂停后是 Resume 和 End。键盘:R = Feel again,S = Skip step。
- 实现:`StepCard` / `TipCard`(`Assets/Learning/Scripts/UI/`),构建器 `LearningSceneBuilder.T1Ui.cs` 生成全部界面(九宫格圆角贴图 `Assets/Learning/Art/UI/round_panel.png` / `round_ring.png`,图标在 `Art/UI/T1/`,Inter 字体在 `Art/Fonts/`,1 设计 px = 0.5 mm)。按钮仍是 Meta 的戳按钮,只换外观,戳的区域按设计尺寸调整。
- T1 不显示指尖黄点(`ChannelHighlights`),也没有编辑器替身手(2026-10-19);在电脑上测试需要 Link 手部追踪。
- 握住时模型手的弯曲调小了(轻握 0.3、紧握 0.45,捏的峰值 0.42 / 0.55),拇指只弯 1/4(`StylizedHand.thumbCurlScale` 0.25),`palmGap` 改为 2.2 cm,用来减少穿模。

**T2 弹琴**:2026-10-20 改为三个层次(L1 感受 / L2 学弹 / L3 测试),用患者自己的手、琴键对齐真实桌面,详见 **`12-t2-piano-three-levels.md`**。

**T5 吃饭**(组件 `EatingTask`,用患者自己的 VR 手;眼睛看着碗,手靠触觉拿稳勺子):
- 拿勺:手(拇指和食指指尖的中点)靠近勺柄 7 cm 内,勺子吸到手里成三指握,拇+食+中持续压力 0.4,之后勺子一直跟着真手。
- 舀粥(软):勺尖进到碗里碰到粥面 → 拇+食 20 Hz 短振 0.25 s(碰碗底)→ 三指压力**慢慢**升到 0.48(满勺)。
- 送到嘴边:勺尖离嘴(头显下方约 8 cm)13 cm 内 → 食物消失,压力回到 0.4(空勺变轻)。
- 舀花生(硬):一次尖锐脉冲(拇+食)→ 三指压力**快速**升到 0.55。和粥是一对"软/硬""轻/重"的对比。
- 放下勺子:勺尖低到桌面附近 → 松开,勺子回到桌上。
- **每一步都有辅助**:7 s 内患者没完成,物体会来找手——勺子飞到手里、碗升到勺子下面、食物飞到嘴里、勺子自己放下。日志步骤记 `_by_patient` 或 `_by_assist`。
- 参数在 `EatingTask` 上:`grabDistance`、`mouthDistance`、`waitForPatientSeconds`、`assistSpeed`、`gripDirection`(勺柄在手中的方向),触觉事件在 T5 touch events。
- 首版只有勺子;筷子、端汤碗(手掌 + 五指 + 汤晃动慢波)后续再加。

**T11 拧瓶盖**(组件 `TwistCapTask`,2026-10-15 与你确认:患侧手拧盖、开盖即结束、按真实手腕转角检测):
- 容器固定在桌上的底座里,只用患侧手拧。每轮换一个容器:**紧瓶盖**(拇+食,转 150° 才开封,压力 0.35→0.7)/ **松瓶盖**(拇+食,45° 就开,压力只到 0.45)/ **罐头盖**(五指 + 手掌一起,120° 开封)。
- 手指(拇指和食指指尖中点)到瓶盖边缘 4.5 cm 内 = 捏住,压力起来。
- 拧的检测:手横向(食指根 → 小指根)绕瓶身轴转过的角度,逆时针(从上往下看)= 打开;单帧跳变 > 45° 当作追踪跳动忽略。松手换握时不计角度。
- 紧的阶段:压力随角度升高,每 30° 一下 25 Hz 短振("咔哒")。开封:一次 35 Hz 强振,压力落到 0.2。松的阶段:更轻的咔哒,再转 270°(罐头 180°)盖子脱落。
- 辅助:7 s 内没捏到 → 瓶子移到手指那里;捏住后 7 s 没转、又停转 4 s → 瓶盖自己以 60°/s 转,触觉照常;患者一转就交回给患者。日志步骤 `open_patient<度>_assist<度>` 记录患者和辅助各转了多少度。
- 参数都在 `TwistCapTask` 的 `containers` 列表里(每种容器的通道、开封角度、压力、咔哒间隔),以及 `engageMargin`、`waitForPatientSeconds`、`stallSeconds`、`assistTurnDegPerSec`。
- 为限制蓝牙流量,压力只在变化 ≥ 0.05 时重发;咔哒间隔不小于 0.3 s。

歌曲在 Inspector 的 `Songs` 里加,格式:`E E F G | G F E D | E:1.5 D:0.5 D:2`;`C+E` 是两指同时,`-` 是休止。只能用 C–G 五个音。

## 4. 改参数

- 所有触觉事件都在任务组件的 **T1/T2 touch events** 里(`HapticSpec`:通道、类型、强度 0.1–1、速度、频率、次数、结束压力),就是 docs/10 §5.9 的事件库。
- T1 招手:`beckonPeriod`(2.4 s)、`beckonOpenCurl` / `beckonClosedCurl`(手指张合范围)、`beckonBob`(浮动幅度,默认 0,因为要和幽灵手对齐)。握住时的弯曲:`gentleCurl`、`firmCurl`、`squeezePeakLight`、`squeezePeakFirm`。引导的呼吸和箭头浮动在 `HandGuide` 组件上。
- T1 握手检测与跟随:`gripCurlDegrees`、`releaseCurlDegrees`、`releaseDebounceSeconds`、`grabDistance`、`followSharpness`、`palmGap`、`palmForward`。T1 时长:`feelSeconds`、`matchSeconds`、`previewSeconds`、`feedbackSeconds`、`doneSeconds`(1 s)、`tipAfterSeconds`(8 s)。Step 4:`squeezeCounts`(默认 2/3/4)、`minQuizRounds`(默认 5)。以上都在 `HandHoldTask` 上。
- 患侧手:任务组件上的 `hand`(会同时设置 `HapticPlayer`)。T2 的琴键是按右手建的,改左手要重建场景(把 `LearningSceneBuilder.DefaultHand` 改成 Left)。

## 5. 换成 Blender 模型

- 手:`StylizedHand` 可直接挂在导入的手模型上。把 15 个关节(拇/食/中/无名/小 × 3,近→远)填进 `joints`,6 个接触点(5 个指腹 + 手掌)填进 `contacts`,手指伸直时右键组件 **Capture straight pose**。关节须绕本地 X 轴向掌心弯曲。
- 琴:`PianoKeyboard` 填 5 个琴键的枢轴(在琴键后缘)和渲染器。
- **T1 模型手已换成 Meta 的 OpenXR 右手**(2026-10-19):`Packages/com.meta.xr.sdk.core/Meshes/HandTracking/OpenXRRightHand.fbx`,贴图在 `Assets/Learning/Art/Hand/`(`T1PartnerHand_BaseColor.png` 是程序生成的肤色、红晕和褶皱,指甲是手绘的;`T1PartnerHand_Normal.png` 是从指甲和褶皱烘焙的法线贴图;材质是 `HandPartnerSkin.mat`)。重建 T1 场景时,构建器会自动摆正模型、填好 15 个关节和 6 个接触点,并算出每个关节的弯曲轴(`StylizedHand.ConfigureImported`)。如果模型丢失或者患侧选的是左手,会退回白模。Blender 源文件和说明见 `docs/art/t1-hand-texture-brief.md`。
- T1 模型手等待和离开的位置由 `PartnerOffer` / `PartnerAway` 两个空物体决定;握住时的位置由真手决定。
- T1 握手的向下角度(2026-10-20):`HandHoldTask` 上的 `offerPitchDegrees`(默认 20°,手指低于水平,绕掌心转)、`offerRollDegrees`(默认 0 = 拇指朝上;正值让模型掌心朝下转)、`offerRaise`(整体抬高,米)。只在 Play 时作用到 `PartnerOffer` / `PartnerAway`,场景里的两个空物体保持构建时的水平姿势;引导手、箭头、等待垫会跟着一起移动。Play 中改这几个值能实时看到效果,但退出 Play 会还原,定下来后要在停止状态下再填一次并保存。作答按钮、Finish 按钮、反馈板、Congratulations 在 `Learning_T1` 下的 `AnswerButtons` / `FinishChoice` / `Feedback` / `Congratulations` 里,可以直接拖动。
- 琴底座等白模直接删掉换模型,没有脚本依赖。

## 6. 数据

每次运行写到 `Application.persistentDataPath/learning/<参与者>_<任务>_<时间>/`:
- `events.csv`:每个触觉事件一行(轮次、步骤、事件 ID、通道、强度、速度、频率、次数、手套是否连接、气压回读)。
- `session.json`:时长、轮数、暂停次数、结束原因、用过的辅助级、**每个通道的累计刺激秒数**(剂量)、各事件次数。T1 另外记 `quiz_trials` / `quiz_correct`,以及 `PATIENT_LET_GO`(松手次数)。
- `trials.csv`(目前只有 T1):每次作答一行(真实次数、轻/重、回答、对错、反应时间);另外还记 Step 1 是患者自己伸手还是手自己过来、Step 2.1 / 3.1 的平均手指弯曲角度、每次握住 / 松开、被打断的捏。

学习阶段不评分,数据只用于剂量和复现。T1 的 Step 4 是例外:有对错反馈。

## 7. 已知限制 / 下一步

- **未上手套测试**:压力强度、上升速度、脉冲(捏 N 下)的实际手感都要上机确认;多通道脉冲接口的强度上限是 0.7。
- Quest 上 SDK 每 100 ms 才发一次蓝牙,节奏精度有限(所以音符间隔 ≥0.6 s)。
- T1 的手掌朝向是从追踪关节(手腕、指根)算出来的,模型手贴合真手的角度需要在头显里看一次再调(`palmGap`、`palmForward`、`PartnerOffer` 位置)。
- T1 握手检测的阈值(50° / 35°)是估计值,没在真手上校准过。握住模型手时手指会被模型手挡住,追踪可能把手指算得偏直;要在头显里看 `trials.csv` 的弯曲角度再调。痉挛、一直握拳的患者会被直接判成"握住"。
- T1 作答按钮按健侧手来戳设计;如果患者用患侧手去戳,会先触发松手,作答阶段允许这样做。
- T5 的勺子按"拇指和食指指尖中点 + 手的朝向"贴在手上,握持角度需要在头显里看一次再调 `gripDirection`;辅助时碗会升到勺子的高度,手举得高时碗会悬空,这是有意的(让不能动的患者也能完成)。
- T11 手腕转角是从手部追踪的指根位置算的,转得快或手指被挡住时会抖;"咔哒"在拧得快时会被 0.3 s 间隔限速。罐头盖的"五指 + 手掌"抓握姿势没有单独检测,只看指尖是否靠近盖子。
- T1 手指痉挛、握拳的患者,追踪可能不稳,模型手会跟着抖;必要时调低 `followSharpness`。
- 界面文字是英文:TextMeshPro 默认字体没有中文字形,要中文需先加中文字体资源。
- 暂停后恢复会重新施加保持中的压力,但正在进行的"捏 N 下"会被打断。
