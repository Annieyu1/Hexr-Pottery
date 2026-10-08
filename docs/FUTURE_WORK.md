# Future Work

版本:v0.1 · 2026-10-08 · 持续更新

分三块:**(A) 咨询治疗师**,**(B) 文献阅读**,**(C) 后续开发**。

---

## A. 咨询治疗师(用于制作游戏任务)

目的:游戏任务不由开发者单方面决定,须由治疗师确认临床合理性。

### A1. 建议的咨询形式
1. **第一轮访谈(1 小时)**:介绍三个游戏概念原型,收集反馈;重点议题见下。
2. **原型体验**:让治疗师亲自戴上手套体验,而不是只看视频。
3. **与 1–2 名患者的可用性测试**(在伦理允许的前提下),观察操作困难与情绪反应。
4. **迭代**:每个里程碑后同步一次,收集修改意见。

### A2. 咨询议题清单(与 OPEN_QUESTIONS 的 T 系列对应)
- 感知维度:压力、频率/pattern、手指定位是否合适?还需要什么?
- **训练时长**:30 分钟(3 × 10)是否合适?单任务 10 分钟?是否需要休息?
- **任务形态**:找不同 / 二选一 / 排序 / 匹配,哪种对目标患者最可行?
- **认知负荷**:对记忆、注意、失语、偏侧忽略患者的适应性
- **交互**:抓握宽松度、替代动作(轻触/悬停)、手部抖动容忍度
- **视觉辅助**:怎么引入、何时撤除、如何度量"依赖度"
- **反馈与防挫败**:措辞、声音、成功率目标、连续失败的处理
- **数据价值**:哪些指标对治疗决策有用?治疗师想看什么图表?
- **治疗师界面**:形态、功能、使用时机(患者训练时 vs 训练后)
- **双手方案**:健侧参照、双侧同步的临床意义
- **入排标准与禁忌**:疼痛、皮肤、痉挛、认知障碍
- **会话安排**:频次(每周几次)、周期(几周)、与常规治疗的关系
- **测评对齐**:与 FMA-UE、Brunnstrom、感觉评估量表的对接时点

### A3. 咨询记录模板
`日期 · 对象(职称/机构) · 议题 · 结论 · 对设计的影响 · 待跟进`
→ 记录放在 `docs/consultations/YYYY-MM-DD-<姓名缩写>.md`(创建时再建目录)。

---

## B. 文献阅读(用于确定任务设计与参数)

> ⚠️ 以下是我凭记忆列出的**候选**文献,**可能有错误或不完整**。使用前请逐条核实(作者、年份、期刊、结论),不要直接引用。

### B1. 阅读目标
1. 中风后躯体感觉缺损的发生率、恢复规律;
2. **感觉辨别训练**的有效性证据(训练内容、剂量、结局);
3. VR / 触觉手套 / 触觉反馈用于上肢康复的研究;
4. 触觉心理物理学测量:压力、振动频率、手指定位的 JND 经验值与方法;
5. 针对患者的**游戏化**与依从性、防挫败设计;
6. 双手/镜像/健侧参照的感觉训练是否有证据;
7. 感觉评估量表(NSA、单丝、两点辨别等)的选择与信效度。

### B2. 候选文献(待核实)
| 主题 | 候选 |
|---|---|
| 感觉辨别训练 | Carey LM, Matyas TA, Oke LE (1993). *Sensory loss in stroke patients: effective training of tactile and proprioceptive discrimination.* Arch Phys Med Rehabil. |
| SENSe 随机对照 | Carey LM, Macdonell R, Matyas TA (2011). *SENSe: Study of the Effectiveness of Neurorehabilitation on Sensation.* Neurorehabil Neural Repair. |
| 中风后感觉缺损 | Carey LM 及合作者关于中风后上肢感觉缺损频率与预后的系列工作;Connell LA, Lincoln NB, Radford KA (2008) *Somatosensory impairment after stroke*(Clin Rehabil) |
| 阶梯法 | Levitt H (1971). *Transformed up-down methods in psychoacoustics.* J Acoust Soc Am. |
| 自适应方法综述 | Leek MR (2001). *Adaptive procedures in psychophysical research.* Percept Psychophys. |
| 心理物理学教材 | Kingdom FAA, Prins N. *Psychophysics: A Practical Introduction.* |
| QUEST | Watson AB, Pelli DG (1983). *QUEST: a Bayesian adaptive psychometric method.* Percept Psychophys. |
| 感觉量表 | Lincoln NB 等 *Nottingham Sensory Assessment*;Stolk-Hornsveld F 等 *Erasmus MC modifications* |
| 运动评估 | Fugl-Meyer AR (1975);Brunnstrom S (1966) |

### B3. 检索建议
- 关键词:*stroke, somatosensory, tactile discrimination training, haptic glove, virtual reality, sensory retraining, psychophysics, JND, Weber fraction, vibrotactile frequency discrimination*
- 数据库:PubMed、Scopus、Google Scholar;关注近 5 年综述与系统评价。
- 阅读笔记模板:`文献 · 人群 · 干预(剂量/形式) · 结局 · 对本项目的启示 · 可信度`
- 笔记放在 `docs/literature/`(创建时再建目录),并维护参考文献表。

### B4. 阅读后需要回答的设计问题
- 感觉训练的有效剂量(每次时长、频次、周期)是什么?
- 辨别任务的难度应维持在什么正确率区间?
- 各维度人群阈值的合理范围(决定刺激范围)
- 触觉反馈与视觉辅助的证据
- 我们的设计与已有研究相比的新颖性(科研价值)

---

## C. 后续开发(已决定推迟的项目)

| 项目 | 说明 | 依赖 |
|---|---|---|
| **自适应能力分级** | 根据表现自动升降难度、自动决定撤除视觉辅助 | 先有稳定的手动分级和 pilot 数据 |
| 局域网治疗师伴侣应用 | 治疗师在平板/电脑上实时看/控 | 一期治疗师反馈 |
| 触觉反馈实验 | 比较有/无触觉反馈对学习和依从性的影响 | 基础版稳定 |
| 双手/健侧参照模式 | 评估临床价值 | 治疗师意见、文献 |
| 转化版 | 裁剪参数、预设方案、更简单的界面 | 科研版完成 |
| 多语言 | 马来语、泰米尔语等 | 场景需要 |
| 贝叶斯阈值估计(QUEST) | 更少试次估阈值 | 阶梯法验证后 |
| 在线数据同步 | 云存储与远程随访 | 伦理与合规 |
| 医疗器械合规准备 | 质量管理、可追溯性、风险管理 | 转化立项 |
| 运动数据分析 | 利用 FlexR 数据评估动作质量,与触觉表现关联 | FlexR API |
| 命令行构建与自动化测试 | 构建脚本、CI | 稳定后 |
