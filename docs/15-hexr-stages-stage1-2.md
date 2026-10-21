# 15 HexR Stages:Stage 1 + Stage 2(从 PICO 版移植)开发文档

版本:v1.1 · 2026-10-21 · 状态:**已实现,编辑器内冒烟测试通过;未接手套、未上 Quest**。场景已由生成器重建(`Assets/HexRStages/Scenes/HexR_Stages.unity`)。本文件描述最终行为和具体改法,实现记录见第 7 节。

---

## 0. 给接手会话的说明(先读)

| 项 | 值 |
|---|---|
| Unity 项目 | `/Users/xuzihan/Documents/Lab/Vibration/HexR-Developer-Tutorial-Meta-OVR` |
| Unity 版本 | 6000.6.0f1,URP,Android(Quest 3) |
| XR / 手套 | Meta XR SDK 205(Interaction SDK:`Grabbable` + `HandGrabInteractable`),HexR 包 `com.microtube.hexr` v0.9.3 |
| 工作目录 | **只在 `Assets/HexRStages/` 下改**,外加本文档 |
| Unity MCP | 项目根 `.mcp.json` 已配置 `unity`(mcp-for-unity 10.3.0)。在项目根目录启动会话即可加载;Unity 里需打开 MCP for Unity 窗口确认桥接已连 |
| PICO 参考项目 | `/Users/xuzihan/Documents/HEXR-Pico`(只读参考,不要改) |

**约束**

- 当前分支 `feature/lighting-urp`,有大量用户未提交的改动(Learning、Hub、ProjectSettings 等)。**不要切分支、不要 stash、不要提交**,除非用户明确要求。
- 不改 `Assets/Learning/`、`Assets/PressureDemo/`、`Assets/Stage3/`、`Assets/Scenes/*.unity`(模板只读)、HexR / Meta OVR 的配置。
- Unity 编辑器开着时命令行 `-batchmode` 打不开项目(报 "another Unity instance is running")。用 MCP 操作编辑器,或让用户点菜单生成场景。
- 自检编译的办法(编辑器开着也能用):复制 `Library/Bee/artifacts/1300b0aE.dag/Assembly-CSharp.rsp`,去掉 `-out/-refout/analyzer/additionalfile` 行,补上 `Assets/HexRStages/Scripts/*.cs`,用 `Unity.app/Contents/Resources/Scripting/DotNetSdk/dotnet exec .../sdk/8.0.318/Roslyn/bincore/csc.dll /noconfig @xxx.rsp` 编译到临时目录。Editor 程序集同理(引用换成刚编出的 Assembly-CSharp.dll)。

---

## 1. 现状(`Assets/HexRStages/`)

| 文件 | 作用 | 本次 |
|---|---|---|
| `Scripts/GrabNotifier.cs` | 订阅 Meta `Grabbable.WhenPointerEventRaised`,多手计数,发 `Grabbed` / `Released` | 加暂停屏蔽 |
| `Scripts/HapticStrength.cs` | 设置 `HexRInteractableHaptics`:`fireOn=Select`、`followGrip=false`、`strength` | 不变 |
| `Scripts/BallSafetyReset.cs` | 低于 `minimumY` 回原位;`ResetNow()` 供重玩 | 改(重力、抓取中不复位) |
| `Scripts/DemoFlowController.cs` | Stage 根物体切换 | 改(回到菜单 = 重开 Stage 1) |
| `Scripts/Stage1Controller.cs` | Stage 1.1 / 1.2 状态机 | 改(标牌时机、Next、暂停) |
| `Scripts/StiffnessBall.cs` / `BasketTrigger.cs` / `StiffnessTrialManager.cs` | Stage 2 | 改(随机、One more try、暂停) |
| `Scripts/FruitItem.cs` / `FruitSortingZone.cs` / `Stage3Controller.cs` | Stage 3 桃子分类 | **本次不动**,流程里暂不进入 |
| `Editor/HexRStagesBuilder.cs` | 菜单 **HexRMeta > Build HexR Stages Scene**:复制 `Assets/Scenes/Meta.unity` → `Assets/HexRStages/Scenes/HexR_Stages.unity`,克隆 Ball-A 做所有球,建平台/篮子/文字/戳按钮,加进 Build Settings 第一位 | 改(重力、碰撞体、新 UI) |

命名空间统一 `HexRMeta.Stages`(编辑器 `HexRMeta.Stages.EditorTools`),避免和全局的 `Assets/Stage3/Scripts/FruitItem.cs` 冲突。

模板 `Meta.unity` 里可用的东西:`OVRCameraRig`、HexR 手套 rig、`Game/Interactables/Group1/Ball-A|B|C`(根上有 `Rigidbody`、`Grabbable`、`PointableUnityEventWrapper`、`HexRInteractableHaptics`、旧的 `ForceBall`;子物体 `HandGrabInteractable`、`Visuals`、`Audio`),旧排序区 `Zone/ZoneWeak|Medium|Strong`(只拿位置,然后删掉)。生成器已处理这些。

---

## 2. 需求(已和用户确认)

Stage 1 和 Stage 2 连在同一个场景里。界面文字沿用 PICO 版的英文。

### 2.1 Stage 1.1 —— 同一个球捏两次

| 步 | 画面 / 指引 | 球力度 | 进入下一步的条件 |
|---|---|---|---|
| 1 | 桌上一个球(中央平台)。指引 **"Squeeze the object gently"** | **10** | 捏一次 |
| 2 | 显示结果 **WEAK / 10**,停 1 秒 | 10 | 自动 |
| 3 | 指引 **"Squeeze the object again"** | **60** | 捏一次 |
| 4 | 显示结果 **HARD / 60**,停 1 秒 | 60 | 自动 |
| 5 | 指引 **"Can you feel the difference?"**,停 3 秒(不需要回答) | — | 自动 → 1.2 |

- **"捏一次" = 抓起再松开**(松开时判定)。和 PICO 一样:`OnRelease` 触发后先显示结果、1 秒后进下一步。

### 2.2 Stage 1.2 —— 两个球对比

| 步 | 画面 / 指引 | 条件 |
|---|---|---|
| 6 | 中央球消失,两个球从中心 0.5 秒滑到左右平台。左 = 力度 **10**,右 = 力度 **60**。指引 **"Feel the difference between the two balls"** | 两个球**各至少捏过一次**(抓起再松开) |
| 7 | 两块标牌 **Weak** / **Strong** 从桌面弹出到各自球的**前方**(球和患者之间),0.3 秒缩放弹出动画 | 自动 |
| 8 | 标牌出现后患者仍可继续捏两个球体会区别(力度不变) | 标牌出现 **2 秒**后 |
| 9 | 出现 **Next** 按钮 | 按下 → Stage 2 |

- 与第一版代码的区别:**标牌原来在两个球出现时就显示,要改成两个球都捏过之后才弹出**;Next 在标牌出现 2 秒后出现(原来是两个都捏过立即出现)。

### 2.3 Stage 2 —— 三个球放进三个篮子

| 步 | 画面 / 指引 |
|---|---|
| 1 | 桌上 3 个**外观完全一样**的球;3 个篮子,牌子 **Weak / Medium / Strong**。指引 **"Feel each ball and put it in the matching basket"** |
| 2 | 三个球的力度是 **10 / 35 / 60**,**每轮随机分配到三个位置**(Fisher–Yates 洗牌),日志打印本轮分配 |
| 3 | 球**进入篮子触发区就算放入**(和 PICO 一样,不要求松手);一个篮子只记一个球;球被拿出来 → 取消记录、计数减一。显示 **"PLACED x / 3"** |
| 4 | 三个篮子都有球 → 结算:显示 **"RESULT\n\n n / 3 CORRECT"**;每个篮子的信息板变绿(对)/ 红(错);每个球上方显示它的正确答案(WEAK / MEDIUM / STRONG);结算后不再响应放入/拿出 |
| 5 | 出现 **One more try** 按钮。按下:球回原位、隐藏答案、篮子颜色复原、计数清零、**重新随机力度**,开始新一轮 |

- Stage 2 之后没有下一关(没有 Next)。Stage 3 代码保留但流程不进入。

### 2.4 全程规则

**物理(按 PICO 版设置)**

- 球:`Rigidbody` 质量 0.01、**useGravity = true**、isKinematic = false、Interpolate、碰撞检测 ContinuousDynamic。
- 抓在手里时不受重力(Meta `Grabbable._kinematicWhileSelected = true`,模板已是 true);松手后按手速抛出、自然下落(`_throwWhenUnselected = true`,模板已是 true)。
- 球能落在桌上、平台上、篮子里:**桌面、平台、篮子底和四壁都要有实体碰撞体**(第一版代码里平台和篮子的碰撞体被删掉了,要加回来);篮子里另有一个 trigger 用来判定。
- **安全复位**:球心低于 **桌面高度 − 0.3 m** 就回到**这个球自己的原位**(PICO 各球的阈值不统一,统一成这个)。被抓着时不复位。复位流程:`LockKinematic` → 速度清零 → 设位姿 → 等一个物理帧 → `UnlockKinematic` → 1 秒冷却。

**常驻按钮(Stage 1 和 Stage 2 都显示)**

| 状态 | 显示的按钮 |
|---|---|
| 进行中 | **Pause**、**Menu** |
| 已暂停 | **Start**(继续)、**Menu** |

- **Pause**:游戏逻辑停住(指引计时、结果显示倒计时、标牌/Next 的延时、Stage 1.2 分球动画、Stage 2 的判定都暂停);球不能抓;手套不出力;球在原地冻结(不下落)。画面上显示 **"Paused"**。
- **Start**:从暂停处继续。
- **Menu**:**回到 Stage 1 的最开头**(暂时没有真正的菜单):所有球回原位、Stage 2 状态清空、取消暂停、只显示 Stage 1、从 2.1 第 1 步开始。
- 场景启动后直接开始 Stage 1(和 PICO 一样,不需要先按开始)。

---

## 3. 实现方案

### 3.1 新增 `Scripts/StagePause.cs`(全局暂停)

不要用 `Time.timeScale = 0`:Meta Interaction SDK 的手部追踪和戳按钮依赖正常时间,暂停后按钮可能按不动。改用自己的暂停标志:

```csharp
public class StagePause : MonoBehaviour
{
    public static StagePause Instance { get; private set; }
    public bool IsPaused { get; private set; }
    public event Action<bool> PausedChanged;
    public void Pause();   // IsPaused=true,通知
    public void Resume();  // IsPaused=false,通知
    // 协程里用:yield return StagePause.Wait(seconds); —— 暂停期间不计时
    public static IEnumerator Wait(float seconds);
}
```

暂停时每个球(由 `BallSafetyReset` 或新的 `PausableBall` 组件监听 `PausedChanged`):

1. 关掉 `HandGrabInteractable`(子物体上的组件 `enabled = false`)→ 正在抓的手会被取消;
2. 关掉 `HexRInteractableHaptics`(它的 `OnDisable` 会释放手套压力);
3. `rigidbody.LockKinematic()` 冻结;记录并清零速度;
4. 继续时反过来:`UnlockKinematic()`、恢复速度、重新启用组件。

⚠️ 第 1 步取消抓取会让 `GrabNotifier` 发出 `Released`,**必须忽略暂停期间的 Released**,否则会被算成"捏了一次"。在 `GrabNotifier.HandlePointerEvent` 里:暂停中只更新计数、不发事件;或者在控制器里判断 `StagePause.Instance.IsPaused`。

### 3.2 刚体操作统一用 Meta 的锁

`Grabbable` 用引用计数的 `Rigidbody.LockKinematic()` / `UnlockKinematic()`(`Oculus.Interaction.RigidbodyKinematicLockerExtension`)管理 kinematic。**代码里不要再直接写 `rb.isKinematic`**,否则和 Grabbable 打架(松手后球可能一直悬空或抓着时掉下)。涉及:`Stage1Controller.SetPhysics`(分球前锁、分完解锁)、`BallSafetyReset`、暂停。

### 3.3 `Stage1Controller` 改动

- 所有 `WaitForSeconds` 换成 `StagePause.Wait`;分球动画循环里暂停时 `yield return null` 且不推进 `elapsed`。
- 1.2 的状态补充:`ComparisonReady` → 两个都捏过 → `LabelsShown`(弹出标牌协程,0.3 秒缩放 0→1)→ 2 秒后 `Completed`(显示 Next)。
- 标牌 `softInfo` / `hardInfo` 改成**两个都捏过之后**才 `SetActive(true)`;位置在球的前方(朝患者方向 0.08 m、桌面上方)。标牌文字只写 **Weak** / **Strong**。
- 1.2 指引文字 `"Feel the difference between the two balls"`。
- `Next` 按钮(原 `stage12Menu` 里的 Continue)改名为 Next,调用 `demoFlow.GoToStage2()`。
- 分球前 `LockKinematic`,结束后 `UnlockKinematic`,两个球落在平台上(平台要有碰撞体)。
- 新增 `public void ResetStage()`:停协程、所有球 `ResetNow()`、调用 `StartStage1()`;给 Menu 用。
- 力度:1.1 时中央球 10 → 60;1.2 时左 10、右 60(已有 `UpdateHapticStrength`,保留)。

### 3.4 `StiffnessTrialManager` 改动

- `StartNewTrial()` 里随机:

  ```csharp
  var levels = new List<(int value, StiffnessCategory cat)> {
      (10, StiffnessCategory.Weak), (35, StiffnessCategory.Medium), (60, StiffnessCategory.Strong) };
  // Fisher–Yates 洗牌后依次 balls[i].Configure(levels[i].value, levels[i].cat)
  ```

- 结算后按钮只剩 **One more try**(`OnOneMoreTryPressed(PointerEvent)` → 现有 `ReplayStage2()` 逻辑:球 `ResetNow` → 等一个物理帧 → 篮子 `ClearBasket` → `StartNewTrial`)。删掉 Next。
- 暂停中 `ReportPlacement` / `ReportRemoval` 直接忽略(或排队,忽略即可:暂停时球被冻结,不会进出篮子)。
- 新增 `public void ResetStage()`(给 Menu 用,= 重玩一轮)。
- 进度 / 结果文字、篮子变色、显示答案沿用现有代码。

### 3.5 `DemoFlowController` 改动

- 新增 `BackToMenu()`:`StagePause.Resume()` → Stage 2 `ResetStage()` → 显示 Stage 1 根、隐藏其它 → Stage 1 `ResetStage()`。
- `GoToStage3` 保留但不再被调用。

### 3.6 新增 `Scripts/StageControlPanel.cs`(常驻按钮)

- 持有三个按钮组 `Pause`、`Start`、`Menu` 和 "Paused" 文字;监听 `StagePause.PausedChanged` 切换显示(进行中:Pause+Menu;暂停:Start+Menu+Paused 文字)。
- 方法:`OnPausePressed(PointerEvent)`、`OnStartPressed(PointerEvent)`、`OnMenuPressed(PointerEvent)`(→ `DemoFlowController.BackToMenu()`)。
- 放在 Stage 根物体之外,两个 Stage 都可见。

### 3.7 `HexRStagesBuilder` 改动

- `CloneGrabbable`:设置 `Rigidbody`:`mass=0.01, useGravity=true, isKinematic=false, interpolation=Interpolate, collisionDetectionMode=ContinuousDynamic`;确认 `Grabbable` 的 `_kinematicWhileSelected`、`_throwWhenUnselected` 为 true(SerializedObject 设置);`BallSafetyReset.minimumY = tableY - 0.3`。
- **桌面碰撞**:检查模板 `Game/RoomEnvironment` 里桌子是否有碰撞体(模板里有 9 个 BoxCollider、2 个 MeshCollider,未逐个核实)。在 `tableCenter` 正下方做 `Physics.Raycast`,打不到桌面就在 `tableY` 加一块不可见的 `BoxCollider` 桌面(大小覆盖球位和篮子区域,约 1.2 × 0.6 m,厚 0.02 m)。
- **平台**:`MakePlatform` 保留碰撞体(现在是删掉的)。平台顶面 = 球底。
- **篮子**:四壁和底保留 `BoxCollider`(实体);另加内部 trigger(现有 `AddZoneTrigger`,高度改成和壁同高 0.05 m 左右,避免球在篮子上方经过就被算放入)。壁高可提到 0.06 m 防弹出。
- Stage 1.2 标牌:改为位于球前方(`softPos - forward*0.08`,高度桌面 + 0.05),文字 "Weak" / "Strong",初始隐藏,缩放从 0 开始由脚本弹出。
- Stage 1 菜单组:按钮文字 **Next**。
- Stage 2 结果按钮:只建 **One more try**。
- 新建 `ControlPanel` 根物体:Pause / Start / Menu 三个戳按钮(用现有 `PlaceButton`,竖排在桌子左侧 `tableCenter - right*0.45`,高度桌面 + 0.35),"Paused" 文字放 HUD 位置。挂 `StageControlPanel`、`StagePause`。
- Stage 3:加开关常量 `IncludeStage3 = false`,为 false 时不建 Stage3Root。
- 重建后在 Scene 视图截图检查布局(MCP 可以截图)。

### 3.8 音效

模板 Ball-A 的 `Audio` 子物体和 `PointableUnityEventWrapper` 上可能已连了抓起/放下声音(PICO 版是抓起 `PickUpObjectAudio`、松开 `DropObjectAudio`)。克隆后检查是否还在;没有的话先不加,列进待办。

---

## 4. 布局(沿用生成器现有坐标系)

- 坐标系:`forward` = OVRCameraRig 水平朝向,`right` = 其右侧。
- 球位:模板 Ball-A/B/C 的位置(从左到右排序),桌面高 `tableY` ≈ 球心 − 半径。
- Stage 1:中央球 = 三个球位的中点;左右球 = 中点 ∓ `right*0.20`;平台直径 0.10、高 0.02。
- Stage 2:球在三个球位;篮子在模板 `ZoneWeak/Medium/Strong` 的位置(比球离患者更远约 0.27 m),大小取原 zone 尺寸(约 0.17 × 0.12 m)且不超过间距的 85%。
- HUD 文字:rig + 上 1.05 m + 前 0.9 m。
- 流程按钮(Next / One more try):`tableCenter + right*0.45`,桌面上方 0.35 m。常驻按钮在左侧对称位置。

---

## 5. 验收清单

编辑器(Play + Meta XR Simulator 或 Link)与 Quest 实机各过一遍:

- [ ] 启动直接进入 Stage 1.1,指引正确,中央球力度 10。
- [ ] 抓起不松手不会推进;松开后显示 WEAK / 10,1 秒后提示再捏,力度变 60。
- [ ] 第二次松开显示 HARD / 60 → "Can you feel the difference?" 3 秒 → 分出两个球,落在平台上不掉。
- [ ] 只捏一个球不出标牌;两个都捏过后标牌从前方弹出;2 秒后出现 Next;标牌出现后仍可捏,力度仍是 10 / 60。
- [ ] Next → Stage 2:三个球外观一致;多玩几轮,日志里力度分配会变化。
- [ ] 球扔/放进篮子会落进去,"PLACED x / 3" 正确;拿出来计数减一;一个篮子放第二个球不计。
- [ ] 三个都放好后显示 n / 3 CORRECT、篮子变色、球上显示答案;One more try 后全部复原并重新随机。
- [ ] 球掉到桌下(低于桌面 0.3 m)回到自己的原位;抓着时不会被复位。
- [ ] Pause:倒计时/动画停住,球冻结、抓不起来、手套不出力;**暂停不会被算成捏了一次**;Start 继续。
- [ ] Menu:任何时候(包括暂停中、Stage 2 结算后)都回到 Stage 1.1 第 1 步,所有球归位。
- [ ] Console 无报错;Quest 上 72 Hz 稳定。

---

## 6. 已知风险

- **重力 + 手部抓取**:Meta 的 throw 速度对 0.01 kg 的球可能偏大,球容易飞出桌面。需要实机调 `Grabbable` 的 throw 参数或球的质量/阻力。
- **篮子判定**:按 PICO 规则"进入即算",手拿着球经过篮子也会计入。若实测误判多,可改成"松手后球在篮子里才算"(PICO 有未使用的 `BallGrabState.OnBallReleased` 就是为此准备的)。
- **暂停时取消抓取**:依赖关闭 `HandGrabInteractable` 让手松开;若 SDK 行为不同(手仍抓着),改为在暂停期间把球的 `Grabbable` 也关掉。
- **HexR 压力切换**:`HapticStrength.Strength` 在球被抓住期间改值,手套是否立即变化未验证(Stage 1.1 是在松开后才改,问题不大)。
- 未在头显独立运行、未接手套测试。

---

## 附录 A:PICO 参考

| 内容 | 位置(`/Users/xuzihan/Documents/HEXR-Pico/`) |
|---|---|
| Stage 1 状态机 | `Assets/_Project/Scripts/Scene1/Stage1Controller.cs` |
| Stage 2 | `Assets/_Project/Scripts/Scene2/StiffnessTrialManager.cs`、`BasketTrigger.cs`、`StiffnessBall.cs` |
| 安全复位 | `Assets/_Project/Scripts/BallSafetyReset.cs` |
| 主场景 | `Assets/_Project/Scenes/Stage1.unity`(Stage1Root + Stage2Root + FlowController) |
| 抓取 | `Assets/HexRAssets/Main Script/HexRGrabbable.cs`:抓住时 `isKinematic=true, useGravity=false`;松开时 `isKinematic=false`,`Gravity=On` 时恢复重力 |

PICO 场景里实际的连线(供对照):

- `CentralBall`:OnGrab → `Stage1Controller.OnPicoGrab` + 抓起音效;OnRelease → `OnPicoRelease`(= 完整捏一次)+ 放下音效。
- `SoftNew`(10)/ `Hardball`(60):OnRelease → `RegisterSoftExperience` / `RegisterHardExperience`。
- Stage 2 `BallA/B/C`:OnGrab/OnRelease → `BallGrabState.HandleGrab/HandleRelease` + 音效;BallA、BallC 还残留了对 Stage 1 的调用(无害,不用移植)。
- 篮子:管理器引用的 3 个触发器是 `SoftBox`、`SoftBox (1)`、`SoftBox (2)` 下的 `Trigger1`,类别分别 Weak / Medium / Strong;`MediumBox`、`HardBox` 是旧物体。
- 球刚体:mass 0.01、useGravity 1、isKinematic 0、Interpolate、ContinuousDynamic。

---

## 7. 实现记录(2026-10-21)

**和第 3 节的差异**

- `StagePause.PausedChanged` 做成**静态**事件(`StagePause.IsPausedNow` 也是静态),这样球在 `OnEnable` 里订阅时不受 Awake 顺序影响。
- 暂停、分球、复位都由 `BallSafetyReset` 管:每种原因各持有一把 `LockKinematic`,只加一次、只解一次;协程被打断(Menu、Stage 根被隐藏)时在 `ResetNow` / `OnDisable` 里归还,球不会一直悬空。
- 关抓取前先把 `GrabNotifier.Muted` 置位:SDK 关 interactable 时会同步取消手,这次取消不算"捏一次"。分球(0.5 秒)和复位那一个物理帧里也关抓取。
- Pause / Start 共用左侧同一个按钮位,Menu 在下方。暂停期间 Next / One more try 不响应。
- 平台用 BoxCollider(圆柱自带的 Capsule 顶面是圆的)。模板房间的 `DeskSurface` 已覆盖所有球位和篮子位,生成器没有加备用桌面。
- 模板球的抓起/松开音效(`BasicGrabPickupAudio` / `BasicGrabReleaseAudio`)克隆后仍在,不用另加。
- 编辑器调试键:Space 捏中央球,1 / 2 捏左 / 右球,T 重开 Stage 1,P 暂停 / 继续,M 回菜单。

**编辑器 Play 模式冒烟测试(用反射直接调释放回调,没有真手)**

- [x] 1.1:10 → WEAK/10 → 1 秒 → 60 → HARD/60 → 3 秒 → 分球,两个球落在平台上(y 0.817)。
- [x] 结果显示中暂停 10 秒以上,状态不推进;继续后 1 秒推进。暂停时球 kinematic、抓取关、HexR 关、显示 Start + "Paused"。
- [x] 1.2:只捏一个球不出标牌;两个都捏过 → 标牌 0.3 秒弹出 → 2 秒后 Next → Stage 2。
- [x] Stage 2:球掉进篮子并留在里面,PLACED 计数、n / 3 CORRECT、篮子红 / 绿、答案都正确;One more try 后归位并重新随机。
- [x] Stage 2 暂停中按 Menu → 取消暂停,回到 1.1 第 1 步。分球进行到一半按 Menu → 锁已归还、抓取恢复、球归位。
- [x] 球掉到桌下 0.5 m → 回到原位。
- [ ] 真手抓取 / 抛球 / 暂停时手里有球(需 Meta XR Simulator 或 Link)
- [ ] 手套力度、Quest 实机帧率
