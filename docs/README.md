# HexR-Meta 开发文档

面向轻中度中风后患者的**手部触觉感知辨别康复**VR 游戏。
硬件:Meta Quest + Microtube HexR 触觉手套(配合 FlexR 手指运动采集)。
状态:**框架设计阶段**(2026-10-08 起)。

## 文档索引

| 文件 | 内容 | 状态 |
|---|---|---|
| [01-project-overview.md](01-project-overview.md) | 项目目标、约束、已确定的决策 | 草案 v0.1 |
| [02-architecture.md](02-architecture.md) | 技术架构、模块划分、场景流程、代码目录 | 草案 v0.1 |
| [03-paradigms-and-thresholds.md](03-paradigms-and-thresholds.md) | 心理物理范式详解、优缺点对比、JND/阈值入门、选型建议 | 待你选择 |
| [04-game-design.md](04-game-design.md) | 三个小游戏、难度分级、视觉辅助淡出、反馈与防挫败、美术方向 | 草案 v0.1 |
| [05-data-and-clinical.md](05-data-and-clinical.md) | 数据记录方案、数据格式、治疗师界面、临床量表、伦理 | 草案 v0.1 |
| [06-repo-and-workflow.md](06-repo-and-workflow.md) | Git 仓库结构、分支、提交规范、资产管理 | 草案 v0.1 |
| [07-m0-cleanup.md](07-m0-cleanup.md) | **M0 任务定义**与可删除清单 | 待你确认 |
| [08-roadmap.md](08-roadmap.md) | **路线图**:里程碑、分工、决策关口、风险 | 草案 v0.1 |
| [09-m2-demo-plan.md](09-m2-demo-plan.md) | **M2 压力 2AFC Demo**:现有代码评审、玩法规格、脚本清单、预试 | 待你确认 D1–D6 |
| [10-two-phase-sensory-reeducation.md](10-two-phase-sensory-reeducation.md) | **两阶段方案**:学习(日常任务中的被动触觉体验)+ 判断(辨别训练);中风 + SCI;文献已逐条核对 | 草案 v0.1,待商议 Q1–Q9 |
| [11-learning-t1-t2.md](11-learning-t1-t2.md) | **学习阶段 T1 牵手 / T2 弹琴 / T5 吃饭 / T11 拧瓶盖**:场景、运行、参数、换 Blender 模型、数据 | 白模可运行,未上真机 |
| [12-t2-piano-three-levels.md](12-t2-piano-three-levels.md) | **T2 弹琴三个层次**:L1 感受 / L2 学弹(逐级提示)/ L3 测试;校准、按键检测、提示、数据 | 可编译,未上真机 |
| [13-t2-menu.md](13-t2-menu.md) | **T2 菜单**：暖色、小动画；标题 → 选层次 → 选曲 → 演奏 → 完成，治疗师设置在齿轮后（Figma "T2 Piano Menu"） | 可编译，编辑器看过标题页 |
| [OPEN_QUESTIONS.md](OPEN_QUESTIONS.md) | **未决问题清单**(按"向谁确认"分类) | 持续更新 |
| [FUTURE_WORK.md](FUTURE_WORK.md) | 后续工作:咨询治疗师、文献阅读、自适应分级等 | 持续更新 |

## 约定

- 文档语言:中文为主,代码、类名、字段名用英文。
- 每份文档顶部标注版本和状态;决策变化时更新并在 `01-project-overview.md` 的决策记录里留痕。
- 文献引用均为**候选**,写进论文或报告前须逐条核实(见 FUTURE_WORK.md)。
- 标注 `[待确认]` 的内容是我的假设或建议,尚未经你或治疗师确认。
