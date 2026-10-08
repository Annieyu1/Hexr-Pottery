# 09 M2:压力 2AFC 玩法 Demo

版本:v0.2 · 2026-10-08 · 分支 `feature/m2-pressure-demo` · 状态:**玩法逻辑待你最终确认(第 9 节)**

目标:先做一个**玩法验证用的 Demo**(灰盒,不做美术),确认"两个物体、反复抓握、放进更强区域"这套玩法对健康人成立、数据可用;之后再做场景美术与世界观。试次逻辑写成**不依赖 Unity 的纯 C# 类**,以后原样搬进 Core。

## 1. 现有 Stage3 代码评审(2026-10-08)

| # | 发现 | 影响 |
|---|---|---|
| 1 | 每个试次的球强度从不改变:`ForceBall.Configure()` 没被调用,强度写死在 Inspector | 每轮相同,可凭记忆位置作答 |
| 2 | 场景里三个球等级是 Strong、Medium、Medium,没有 Weak | 配置错误 |
| 3 | 抓取手指 ring、pinky 关闭 | 不是"整只手"感受 |
| 4 | 结算靠 `OnTriggerEnter`,球一碰区域就算放入,无确认 | 扔、掉落、路过都会误判 |
| 5 | 球带 Rigidbody,松手会滚走 | 滚出桌面,让患者挫败 |
| 6 | 只记录内存里的试次号和正确率,不写文件 | 无反应时间、抓握次数、刺激值、位置 |
| 7 | 状态机只有 Playing → ShowingResult(5 秒)→ 下一轮 | 无开始/暂停/结束 |
| 8 | `FruitItem` 未使用,其强度表与 `ForceBall` 不一致 | 重复代码 |
| 9 | 运行时没有"当前刺激值"出口 | 无法记录 |

结论:**抓取 + 触觉(`Grabbable` + `HandGrabInteractable` + `HexRInteractableHaptics`)直接复用;分拣区域、三球、固定等级那半边换成 2AFC 逻辑。**

## 2. 决定汇总(2026-10-08)

| 项 | 决定 |
|---|---|
| 作答方式 | 把物体放进**单个**"更强"区域,松手后停留确认 |
| 难度 | **不做难度自适应、不做阶梯**。每次会话使用**固定难度**(可在开始前选择) |
| 学习期 | 不做 |
| 试次数 | **没有试次数上限**;靠计时和手动结束 |
| 提示 | 需要,用**手势提示**引导患者抓取物体 |
| 控制功能 | **Start、Pause、Replay、计时**(定义见第 5 节) |
| 物体 | 两个外观完全一致,只靠左右位置区分 |
| 题目 | 固定问"哪个压力更强" |
| 感受 | 整只手(五指 + 手掌)感受抓握压力 |
| 反馈 | 视觉(文字、特效)+ 声音;中性语言;无触觉反馈 |

## 3. 场景布局

```
          [ 更强区域 ]        前方正中,地面有标记
 [底座 L]             [底座 R]
  物体 L               物体 R   外观、大小、材质完全一致
       [ 控制/提示面板 ]       Start · Pause · Replay · 计时 · 提示语
```

- 区域是一块明显的感应垫,离两个底座都近(约 30 cm);
- 控制面板为世界空间 UI,用 Poke 戳按钮(Start、Pause、Replay),按钮要大。

## 4. 一个试次

```
Prepare → Explore → Answer(放入区域) → Confirm(停留) → Feedback → Next
```

1. **Prepare**:两物体回到底座;随机决定哪侧更强;设置两物体 `strength`。
2. **Explore**:患者无限次抓握任意物体,放开,换另一个;无倒计时。
   - 抓住时整只手感受该物体的压力;松手即停;
   - 松手但**不在区域内**:0.5 秒后物体平滑回底座(不靠物理滚动)。
3. **Answer**:患者把认为更强的物体拿到区域内,**松手**。手还抓着时不算作答。
4. **Confirm**:松手后在区域内**停留 0.5 秒**才算作答;停留期间可拿出重选(记为"改主意");区域内已有物体时,另一个进入不算数。
5. **Feedback**:
   - 正确:"对了",柔和上扬音,小光效;
   - 错误:"再试一次",中性短音,**不用蜂鸣和红色**,让**正确的物体短暂发光**作为学习提示;
6. **Next**:约 2 秒后下一试次(暂停中则等待)。

### 刺激
- 基准强度 `S` 在 {25, 30, 35} 中随机,比较强度 `C = S + Δ`;较强的是 `C`,**左右随机,连续同侧不超过 3 次**;
- 单位:HexR `strength`(可用范围 10–60),`C ≤ 60`;
- **Δ 固定**:从下表选一档作为本次会话的难度(开始前选,会话内不变)。

| 难度档 | Δ | Weber 比(S=30) |
|---|---|---|
| 1 | 25 | 83% |
| 2 | 18 | 60% |
| 3 | 12 | 40% |
| 4 | 8 | 27% |
| 5 | 5 | 17% |
| 6 | 3 | 10% |

> 数值为占位,真实范围靠健康人预试确定。

### 手势提示(引导抓取)
目的:让不熟悉的患者知道"要拿起物体来感受"。
- **何时出现**:会话第一个试次开始时;之后任一试次中**若 N 秒(默认 10)没有任何抓取**则出现;一旦抓取即消失;
- **形式(Demo)**:物体上方一个循环播放的**抓握手势动画图标** + 物体轮廓脉冲发光 + 文字"试试拿起它";
- **形式(之后可选)**:3D 幽灵手做抓握动画(需要美术资源,放到美术阶段);
- 提示出现次数记录到数据里。

## 5. 会话流程与控制

### 状态机
```
Idle(开始界面) ──Start──► Playing ◄──Resume── Paused
                              │  ▲                │
                              │  └────Pause───────┘
                  时间到 / 点 End
                              ▼
                           Ended(汇总) ──Replay──► Idle(新会话)
```

| 控制 | 行为 |
|---|---|
| **Start** | 开始界面显示"开始"按钮;点击后创建新会话(新会话 ID、新 CSV),计时开始,进入第一个试次 |
| **Pause** | 暂停计时;**两物体回底座且不可抓取,手套压力立即停止**;面板显示"继续 / 重新开始 / 结束";暂停期间不产生试次 |
| **Resume** | 恢复计时,当前试次**重新开始**(重新 Prepare,避免半途状态) |
| **计时** | 面板显示**已用时间**(正计时)。可配置时间上限(默认 10 分钟,0 = 不限),到时进入 Ended。**只计 Playing 状态时间**,暂停不计 |
| **End** | 暂停面板上的"结束",进入 Ended |
| **Replay** | 在 Ended 汇总界面点击:关闭当前会话文件,开始**新的一次会话**(回到 Idle,可重新选难度)。**[待确认:是否还需要"重做当前试次"]** |

### 结束汇总
显示:会话用时、完成试次数、总正确率、难度档。数据已自动写文件。

## 6. 异常处理
- 物体掉出桌面或离底座超过阈值:自动回底座;
- 抓握中手套断连:暂停试次,提示,重连后**重做该试次**,记录事件;
- 两手同时抓两个物体:允许,但手套只在戴的那只手上有感觉。

## 7. 记录的数据

### 每个试次一行(`trials.csv`)
| 字段 | 说明 |
|---|---|
| `session_id`, `trial_index` | 定位 |
| `difficulty`, `S`, `C`, `delta`, `strength_left`, `strength_right` | 刺激 |
| `stronger_side` | 较强一侧 |
| `chosen_side`, `correct`, `changed_mind_count` | 反应 |
| `t_trial_start`, `t_first_grab`, `t_answer`, `rt_ms` | 时间(RT = 首次抓握到作答) |
| `grabs_left`, `grabs_right`, `hold_ms_left`, `hold_ms_right` | 探索 |
| `hint_shown`, `auto_returned`, `disconnected` | 异常与提示 |

### 每次会话(`session.json`)
`session_id`、日期、受试者 ID、手别、难度档、`app_version`、总用时(仅 Playing)、暂停次数、结束原因(时间到 / 手动结束)、`schema_version`。

## 8. 需要写的脚本(我负责)

放在 `Assets/Stage3/Scripts/` 迭代,稳定后迁入 `_Project/Games/Pressure`。

| 脚本 | 类型 | 职责 |
|---|---|---|
| `PressureTrial` | 纯 C# | 一个试次的数据 |
| `TrialGenerator` | 纯 C# | 按难度档产生试次(随机 S、左右随机、同侧限制) |
| `SessionClock` | 纯 C# | 只在 Playing 状态累计时间,支持暂停/恢复/上限 |
| `GameFlow` | 纯 C# | Idle / Playing / Paused / Ended 状态机 |
| `PressureObject` | MonoBehaviour | 设置 `strength`;监听抓取/松开;上报次数与时长;松手回位 |
| `AnswerZone` | MonoBehaviour | 检测物体在区域内且已松手,停留确认,上报答案;支持改主意 |
| `HintController` | MonoBehaviour | 空闲计时,显示/隐藏手势提示与物体发光 |
| `ControlPanel` | MonoBehaviour | Start/Pause/Resume/Replay/End 按钮、计时显示、汇总页 |
| `PressureGameController` | MonoBehaviour | 把以上串起来,驱动试次循环与反馈 |
| `TrialLogger` | 纯 C# + 文件 | 逐试次 CSV、会话 JSON,逐行 flush |

弃用:`ForceStageController`、`ForceSortingZone`、`ForceBall`、`FruitItem`(Git 历史里保留)。

## 9. 待你最终确认

| # | 问题 | 我的默认 |
|---|---|---|
| 1 | **Replay** 含义:是"结束后重新开一次会话"(我的理解),还是还需要"重做当前试次"? | 重新开一次会话;不做重做试次 |
| 2 | **计时**:正计时显示已用时间,并可设上限(默认 10 分钟,到点自动结束)。你要"不设上限、只手动结束",还是"设上限"? | 默认设 10 分钟,可改为 0 不限 |
| 3 | **手势提示**:Demo 用"抓握手势动画图标 + 物体发光 + 文字"(2D),3D 幽灵手放到美术阶段。可以吗? | 可以 |
| 4 | 提示触发:首个试次必出 + 10 秒无抓取出现。可以吗? | 可以 |
| 5 | 固定难度:开始前在 Start 界面选 1–6 档,还是暂时写在 Inspector? | 先写在 Inspector,Hub 阶段再做界面 |
| 6 | 错误后让正确物体短暂发光(算视觉线索) | 保留 |
| 7 | 区域停留确认 0.5 秒 | 保留 |

## 10. 你在 Unity 里要做的(手动更省力)

1. 复制 `Meta.unity` 为 `Pressure_Demo.unity`(保留原场景作参考);
2. 删掉三个球、三个区域(Zone、Cesta)、`StageController`;
3. 建两个底座 + 两个一模一样的物体:各挂 `Grabbable`、`HandGrabInteractable`、`HexRInteractableHaptics`(参照现有球),**勾选 ring、pinky**,材质相同;
4. 建"更强区域"感应垫(带 Trigger Collider,标记清楚);
5. 建世界空间 UI 面板:TMP 文本(提示、计时、反馈、汇总)和 Start/Pause/Resume/Replay/End 的可戳按钮(参考 `1.unity` 的 BigRedButton 与 Meta 的 Poke 示例);
6. 准备手势提示图标(2D 动画或几张图序列),放在物体上方;
7. 脚本写好后,把引用拖到控制器上(我给出每个引用该拖什么的清单)。

## 11. 健康人预试(Demo 完成后)

1. 3–5 位健康志愿者,单手戴手套;
2. 每人在 2–3 个难度档各做一轮(各 10 个试次以上),记录正确率;
3. 找出:哪一档开始接近随机(50%)?3 个 `strength` 单位的差能否区分?
4. 体验:抓握是否顺手、区域是否好放、有没有视觉线索、提示是否合适、暂停恢复是否顺畅;
5. 结果填回难度表并更新 03、05。

## 12. 验收标准

- [ ] Start → 多个试次 → Pause/Resume → End → 汇总 → Replay 全流程在 Quest 3 + 手套上跑通,无需编辑器;
- [ ] 暂停时手套压力立即停止;
- [ ] 计时只计 Playing 时间;
- [ ] 每试次一行 CSV,字段齐全,可导出打开;
- [ ] 左右随机,同侧不超过 3 次;两物体外观完全一致(截图对比);
- [ ] 松手后物体回位,不滚落;只有松手并停留才算作答;
- [ ] 手势提示按规则出现和消失;
- [ ] 健康人预试至少 3 人,得出有效难度范围。

## 13. 之后
Demo 玩法确认 → 纯 C# 类迁入 Core(M1 雏形)→ 场景美术与世界观 → 3AFC 中级 → 治疗师界面与会话流程(M3)。

## 14. 脚本框架(2026-10-08 已写,未在 Unity 中编译验证)

位置:`Assets/PressureDemo/Scripts/`,命名空间 `HexRMeta.Pressure`,不改场景、不动 Stage3 旧脚本。

### 逻辑层(纯 C#,以后可搬进 Core)
| 文件 | 职责 |
|---|---|
| `PressureSettings.cs` | 全部可调参数(难度档、强度表、计时、停留确认、提示等待、受试者 ID、手别)+ `Side` 枚举 |
| `PressureTrial.cs` | 一个试次的刺激与反应数据 |
| `TrialGenerator.cs` | 固定难度下随机 S、左右随机、同侧不超过 3 次 |
| `SessionClock.cs` | 只累计 Playing 时间,上限 10 分钟,`Format()` 显示 mm:ss |
| `GameFlow.cs` | Idle / Playing / Paused / Ended 状态机,拒绝非法跳转 |
| `TrialLogger.cs` | 逐试次 CSV(逐行 flush)+ 会话 JSON,存 `persistentDataPath/pressure_sessions/` |

### Unity 层
| 文件 | 挂在哪 | 职责 |
|---|---|---|
| `PressureObject.cs` | 两个物体各一个 | 设置压力;抓取/松开事件;抓握次数与时长;松手回底座;锁定时手套静音 |
| `AnswerZone.cs` | "更强"感应垫(Trigger) | 松手后停留确认才算作答;拿出重选计"改主意" |
| `HintController.cs` | 提示物体 | 首个试次必出,10 秒无抓取出现,抓取即消失 |
| `ControlPanel.cs` | 控制面板 | Start/Pause/Resume/End/Replay 按钮钩子、计时、提示语、汇总 |
| `FeedbackPresenter.cs` | 任意物体 | 文字 + 音效 + 特效;错误后让正确物体发光 |
| `PressureGameController.cs` | 场景里一个空物体 | 总控:串起以上所有,驱动试次循环、暂停/恢复/结束、写数据 |

### 接线清单(你在 Unity 里做,之后)
1. **每个物体**(左、右):挂 `PressureObject`(同物体上必须已有 `Grabbable`、`HexRInteractableHaptics`);`Side` 选 Left/Right;`Interactables` 拖入它的 `HandGrabInteractable` 子物体;`Highlight Visual` 可拖一个发光子物体(可空);
2. **感应垫**:加 Collider 并勾 Is Trigger,挂 `AnswerZone`;`Dwell Indicator` 可空;
3. **提示物体**:挂 `HintController`,`Hint Visuals` 拖入图标/发光/文字,`Pulse Targets` 可空;
4. **控制面板**:挂 `ControlPanel`,四个状态面板和三处文字拖入;**每个按钮的戳事件**(Poke 的 UnityEvent 或 UI Button.onClick)分别连到 `OnStartPressed / OnPausePressed / OnResumePressed / OnEndPressed / OnReplayPressed`;
5. **反馈**:挂 `FeedbackPresenter`,文字、AudioSource、音效、粒子可逐项拖(都可空);
6. **总控**:空物体挂 `PressureGameController`,把左右物体、感应垫、提示、面板、反馈拖入;`Settings` 里设受试者 ID、手别、难度档(1–6)。

### 已知缺口(框架里留了位置)
- 手套断连处理:`PressureTrial.disconnected` 已预留,还没接 HexR 连接事件;
- 暂停面板里的"重新开始"按钮:目前由 Replay 流程覆盖(结束 → Replay);
- 单元测试(`TrialGenerator`、`SessionClock`、`GameFlow`)还没写。
