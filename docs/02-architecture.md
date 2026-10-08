# 02 技术架构

版本:v0.1 草案 · 2026-10-08

## 1. 设计原则

1. **游戏与"训练科学"分离**:刺激生成、自适应、试次记录、数据导出由核心层负责;三个小游戏只负责"如何呈现和交互"。换范式、加游戏不动核心。
2. **手套可替换**:所有触觉输出经 `IHapticDevice`,真实手套和模拟手套同一接口。没有手套时在编辑器/Meta XR Simulator 里也能完整跑流程。
3. **配置驱动**:难度、刺激范围、试次数、视觉辅助级别全部来自配置(ScriptableObject + JSON),治疗师/研究者能改,不用改代码。
4. **数据优先**:每个试次先写数据,再做表现。任何游戏都必须通过同一个 `TrialRunner`,保证数据格式统一。
5. **单人维护**:少即是多。先做能支持三个游戏的最小抽象,不提前设计用不到的扩展点。

## 2. 分层

```
┌────────────────────────────────────────────────────┐
│ Games (Pressure / Vibration / FingerLocalization)  │  场景、交互、美术、反馈表现
├────────────────────────────────────────────────────┤
│ Training Core                                      │
│  SessionManager · TrialRunner · Stimulus           │
│  AdaptiveProcedure (Staircase / Fixed) · Scoring   │
│  DifficultyProfile · FeedbackDirector              │
├──────────────────────┬─────────────────────────────┤
│ Haptics              │ Data                        │
│  IHapticDevice       │  DataLogger (CSV + JSON)    │
│  HexRGloveDevice     │  SessionMetadata            │
│  SimulatedGlove      │  Export                     │
├──────────────────────┴─────────────────────────────┤
│ Platform: Meta XR SDK (hand tracking), HexR package│
└────────────────────────────────────────────────────┘
        旁路:Therapist(配置、查看、导出),依赖 Core 与 Data
```

## 3. 核心类型(草案)

| 类型 | 职责 |
|---|---|
| `IHapticDevice` | `SetPressure(finger, level)`、`SetVibration(finger, freq, amp)`、`Stop(finger)`、`ReadPressure()`、`IsConnected` |
| `HexRGloveDevice` | 封装 `HaptGloveHandler` / HexR 的 API |
| `SimulatedGlove` | 不连手套时用:在场景里用颜色/数值显示每指输出,并写日志 |
| `Stimulus` | 数据结构:`Channel`(Pressure/Vibration/Location)、`Finger[]`、数值参数、时长 |
| `StimulusSet` | 一个试次内的多个候选刺激(含"标准刺激"与"比较刺激"、正确答案索引) |
| `IAdaptiveProcedure` | `NextTrial()`、`Report(correct)`、`Threshold`、`IsFinished`。实现:`StaircaseProcedure`、`FixedLevelProcedure` |
| `TrialRunner` | 一个试次的状态机:呈现 → 探索 → 作答 → 判定 → 反馈 → 记录 |
| `SessionManager` | 跨场景持久;持有受试者 ID、手别、配置、`DataLogger`,管理三个游戏的顺序与计时 |
| `DifficultyProfile` | ScriptableObject:每个难度级的刺激范围、差值、辅助级别、试次数 |
| `FeedbackDirector` | 统一的反馈出口:文字、特效、声音;文案集中管理(便于本地化) |
| `DataLogger` | 逐试次写 CSV;会话元数据写 JSON;崩溃安全(逐行 flush) |

## 4. 场景流程

```
Boot ──► Hub ──► Game1_Pressure ─┐
          ▲      Game2_Vibration ├─► (结束回 Hub)
          │      Game3_Finger   ─┘
          └─ Therapist(隐藏入口:设置、查看、导出)
```

- `Boot`:初始化 `SessionManager`(`DontDestroyOnLoad`)、检测手套连接、加载配置。
- `Hub`:选择受试者/游戏/难度;"30 分钟会话模式"按顺序自动串起三个游戏,每个计时 10 分钟。
- 每个游戏一个独立 Scene,共用 Core 预制体(`TrialRunner`、`FeedbackDirector` 等)。
- 治疗师入口避免被患者误触(长按/特定手势/PIN)`[待确认]`。

## 5. Unity 项目目录(建议)

```
Assets/
  _Project/
    Core/            训练核心(asmdef: HexRMeta.Core)
    Haptics/         触觉抽象与实现(asmdef: HexRMeta.Haptics)
    Data/            日志与导出(asmdef: HexRMeta.Data)
    Therapist/       治疗师界面(asmdef: HexRMeta.Therapist)
    Games/
      Pressure/      Scripts · Scenes · Prefabs · Art
      Vibration/
      Finger/
    Shared/          共用 UI、音效、材质、字体、本地化
    Config/          DifficultyProfile 等 ScriptableObject
    Scenes/          Boot · Hub
  _Legacy/           官方教程与旧原型(参考用,稳定后删除)
  (Oculus / XR / Settings / TextMesh Pro 等第三方与工具目录保持原位)
```

- 每个模块一个 **asmdef**,依赖单向(Games → Core → Haptics/Data),加快编译并防止循环依赖。
- 命名空间:`HexRMeta.Core`、`HexRMeta.Haptics`、`HexRMeta.Games.Pressure` 等。

## 6. 配置与数据文件

| 文件 | 位置 | 内容 |
|---|---|---|
| `DifficultyProfile.asset` | `Assets/_Project/Config` | 每游戏每级的刺激参数,编辑器内调 |
| `study_config.json` | `persistentDataPath/config` | 科研版运行参数:试次数、时长、辅助级别、手别、是否记录运动数据 |
| `sessions/<id>/trials.csv` | `persistentDataPath/data` | 逐试次数据 |
| `sessions/<id>/session.json` | 同上 | 会话元数据、校准值、版本信息 |
| `sessions/<id>/motion.csv` | 同上(可选) | FlexR/手部运动低频采样 |

Quest 上 `persistentDataPath` 为 `/sdcard/Android/data/<包名>/files`,可通过 USB(adb / Quest 文件管理)导出;治疗师界面内置导出入口 `[待确认]`。

## 7. 触觉输出的关键工程问题

1. **刺激必须可复现**:每次呈现的刺激(目标值与实际写给手套的值)都记录。
2. **硬件分辨率与线性度**:气压档位 0.1–1.0 与真实压力、主观感受并非线性,需做**校准**(实测或用 `GetAirPressure()` 回读)。校准结果存入 `session.json`。`[待确认:公司能否提供压力-档位标定数据]`
3. **延迟与上升时间**:气动触觉有爬升时间,试次时序要给足,并记录实际起止。
4. **刺激期间的手部姿势**:手指弯曲会改变气囊压力;FlexR 数据可用来标注/剔除异常试次。
5. **蓝牙连接稳定性**:断连要暂停试次、提示、恢复后重做该试次,并记录事件。
6. **避免触觉反馈干扰**:当前不做触觉反馈(决策),保证被测刺激的纯净。

## 7.1 复用教程的 Interactable(2026-10-08)

教程里的"可抓取 + 有触觉"物体已经满足游戏 1 的需要,**不另造轮子**:

```
物体
 ├─ Grabbable            (Meta Interaction SDK:可被抓取/移动)
 ├─ HandGrabInteractable (Meta Interaction SDK:手抓取判定)
 └─ HexRInteractableHaptics (HexR:抓取时给手套施加压力)
      fireOn = Select,  followGrip = false,  strength = 10~60
      thumb/index/middle/ring/pinky/palm 全开 → 整只手感受
```

- `followGrip = false`:压力由**物体**决定,不随患者握力变化,这才是被测刺激。
- 需把 ring、pinky、palm 都打开才是"整只手"。
- 我们的代码只做"配置层":`PressureObject`(取代 `ForceBall`)在每个试次把 `strength` 设为刺激值,并向 `TrialRunner` 报告"被抓取/被松开/抓取次数/时长"。
- 触觉输出走 `HexRInteractableHaptics`,而非自己调 `HaptGloveHandler`;`IHapticDevice` 保留给游戏 2、3(振动、单指定位)以及模拟手套。

## 8. 测试策略

- **编辑器内**:`SimulatedGlove` + 键盘/鼠标或 Meta XR Simulator 完成全部流程;单元测试覆盖 `StaircaseProcedure`、`DataLogger`、刺激生成(Unity Test Framework 已在依赖里)。
- **真机**:Quest + 真实手套,按清单做回归(连接、每指输出、10 分钟持续运行、数据导出)。
- **健康人预试**:先用健康志愿者验证流程和难度范围,再进入患者。

## 9. 下一步工程里程碑(建议顺序)

1. M0 清理:去掉 upstream、归档旧文件、建立目录与 asmdef 骨架。
2. M1 核心最小闭环:`IHapticDevice` + `SimulatedGlove` + `TrialRunner` + `DataLogger`,用空场景跑通一个试次。
3. M2 游戏 1(压力):把 Stage3 迁入框架,换成选定范式。
4. M3 会话流程:Boot / Hub / 30 分钟串联 / 数据导出。
5. M4 游戏 2、3。
6. M5 治疗师界面一期(会话内配置 + 导出)。
7. M6 美术与反馈打磨;健康人预试;pilot 准备。
