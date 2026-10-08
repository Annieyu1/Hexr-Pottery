# 07 M0:清理与骨架

版本:v0.2 · 2026-10-08 · 状态:**暂不执行删除**(2026-10-08 用户决定);URP 迁移待决定

## 1. M0 的目标

把一个"教程拷贝"变成一个**干净、能在 Quest 3 + 手套上构建运行、结构符合 02 的工程**。M0 **不写训练核心**(那是 M1),只做以下事:

| 编号 | 任务 | 产出 |
|---|---|---|
| M0-1 | 清理不用的教程内容(第 3 节) | 仓库体积和编译时间下降,没有无关场景 |
| M0-2 | 建立 `Assets/_Project/` 目录与 asmdef 骨架(02 第 5 节),把 Stage3 迁入 `Games/Pressure` | 目录与命名空间就位,Stage3 能编译 |
| M0-3 | **渲染管线决定**:内置管线 → URP(第 4 节) | 管线确定,后面美术不返工 |
| M0-4 | 跑通 Meta XR Simulator,编辑器内能进入场景并抓取 | 无头显也可测试 |
| M0-5 | 建 `Boot`、`Hub` 两个空场景并设为构建场景 | 场景流程骨架 |
| M0-6 | 打一个 Quest 3 的 Android 构建,真机+手套验证:连接、抓球、有压力 | 基线构建,确认管线没被清理弄坏 |

完成标准:**真机能抓球并感受压力;编辑器能用模拟器抓球;仓库干净;文档同步。**

## 2. 教程里值得保留的东西

| 保留项 | 作用 |
|---|---|
| `HexR Main (OVR)` 预制体 | HexR 手套管理(连接、蓝牙、菜单) |
| `Pressure Controller` 预制体 | 每只手的压力控制器 |
| `OVRCameraRig` + `OVRInteraction` + `OVRHands`、手部合成预制体 | Meta 手部追踪与交互 |
| `HandGrabInteractor` / `HandPokeInteractor` | 抓取和戳按钮(UI 用 Poke) |
| **球的结构:`Grabbable` + `HandGrabInteractable` + `HexRInteractableHaptics`** | 游戏 1 的核心交互,见 02 第 7.1 节 |
| `BasicGrabPickupAudio` / `BasicGrabReleaseAudio` | 抓放音效(后期可换) |
| `[BuildingBlock] Passthrough` | 透视模式,是否使用待定 |
| `Assets/Plugins/Android/AndroidManifest.xml`、`Assets/XR/`、`Assets/Resources/`(Meta/OVR 设置)、`Assets/Settings/Build Profiles`、`Assets/TextMesh Pro`、`Assets/Oculus` | 平台与 SDK 配置,不要动 |
| `Assets/Stage3/*` | 游戏 1 原型 |

## 3. 可以删除的清单

我用依赖分析核对过:**`Meta.unity` 和 `Stage3` 对 `Assets/Tutorial` 的引用为 0**。

### 3.1 可以直接删(内容已无用)

| 项 | 大小 | 理由 |
|---|---|---|
| `Assets/Tutorial/`(Hospital 53 MB、Fountain 17 MB、Effects 5 MB) | **76 MB** | 仅被场景 2、3 使用 |
| `Assets/Scenes/2.Hospital Tutorial.unity`(+.meta) | 0.4 MB | 医院教程场景 |
| `Assets/Scenes/3.Water Effects.unity`(+.meta) | 0.3 MB | 喷泉教程场景 |
| `ProjectSettings/EditorBuildSettings.asset` 中上述两个场景条目 | — | 删场景后必须同步,否则构建报错 |
| `Assets/Stage3/Scripts/Stage3Controller.cs.txt`(+.meta) | — | 旧版本,不参与编译,已被 `ForceStageController` 取代 |
| `Assets/Plugins/Android/Editor/OculusManifestBTFixer.cs` | — | 整个文件被注释掉,是死代码 |

### 3.2 暂时保留,M2 之后再删

| 项 | 为何先留 |
|---|---|
| `Assets/Scenes/1.unity`(原 Basic Tutorial) | 里面有 Torch、Key、BigRedButton 的演示,Poke 按钮做 UI 时可参考;Git 历史里永远找得回,到游戏 1 重建完成后删 |
| `Meta.unity` 里的 `InfoFrames`(Foam/Key)、`TorchInfoFrame`、`RoomEnvironment` | 都是教程装饰;场景重建时一并去掉,不建议现在在原场景里零碎删 |
| `Meta.unity` 里的 3 个 Cesta(篮子)区域、`ZoneWeak/Medium/Strong` | 分拣原型,2AFC 不再用,但在重构游戏 1 之前保留作为参考 |

### 3.3 可考虑精简(风险较高,单独分支试,编译通过再合并)

| 项 | 说明 |
|---|---|
| `com.unity.visualscripting`、`com.unity.multiplayer.center`、`com.unity.collab-proxy`、`com.unity.timeline`、`com.unity.performance.profile-analyzer`、`com.unity.testtools.codecoverage` | 目前项目没用到,删了减少导入和编译 |
| `com.unity.feature.development` | 是 IDE 套件(Rider/VS),按你实际用的 IDE 保留一个 |
| `com.unity.postprocessing` | 迁 URP 后不需要(URP 自带 Volume) |
| `com.meta.xr.sdk.all` → 只留 `core` + `interaction` + `interaction.ovr`(必要时加 `platform`) | `all` 会带入 voice、audio、haptics、mrutilitykit 等整套;能显著减少体积。**HexR 对 Meta 包的依赖没有文档,要逐个试。** |
| OpenXR 相关(`com.unity.xr.openxr`、`OpenXR Loader`、`HexR Main (Open XR)`) | 你走 OVR 路线可以去掉,**但先确认 Meta XR Simulator 不依赖 OpenXR 再删** |

## 4. 渲染管线:建议在 M0 迁到 URP

现状:`GraphicsSettings` 里没有自定义渲染管线 → **内置(Built-in)管线**。

| | Built-in | URP |
|---|---|---|
| Quest 官方推荐 | 不再主推 | **推荐**(性能、特性) |
| 卡通 / 低多边形着色 | 可以,资源少 | 资源多(Shader Graph、toon 着色器) |
| 后处理 | 旧版 Post Processing | 内置 Volume |
| 迁移成本 | — | 现在迁移小(几乎没有自己的材质);**美术堆起来之后迁移很痛** |

由于你要大量找/改美术资产,**越早迁越省事**。建议 M0 做,材质统一转 URP。需要你确认(见 OPEN_QUESTIONS Q16)。

### 4.1 迁移 URP 可能遇到的问题(针对本项目)

本项目 `Meta.unity` 与 Stage3 可达的材质里,**几乎全是 Meta SDK 自带的自定义着色器**(OculusHand、UIStyleShader、RoundedPanelMesh、room/LightmapPBR/StencilSky 等示例着色器),没有太多 Standard 材质,所以"满屏粉红"的风险比一般项目小。但仍有这些点要逐项验证:

| 风险 | 表现 | 处理 |
|---|---|---|
| 自定义着色器不兼容 URP | 手/UI 不显示或粉红 | Meta 的 Hand/UI 着色器官方支持 URP,**需真机确认**;示例着色器(room、LightmapPBR、StencilSky、MRDeskShadow)属于教程环境,本来就要替换,出问题可忽略 |
| **透视(Passthrough)变黑** | 开了透视却是黑背景 | 常见原因:相机背景不是透明、URP 开了 HDR。需设相机 Clear=Solid Color、Alpha=0,URP Asset 关 HDR |
| 后处理包冲突 | `com.unity.postprocessing`(v2)在 URP 不生效 | 移除该包,改用 URP Volume(Quest 上也尽量少用) |
| 画质设置不适合 Quest | 帧率下降、发热 | URP Asset 建议:关 HDR、MSAA 4x、关 Depth/Opaque Texture、阴影级联 1 且距离短、不开 Soft Shadow |
| 材质外观变化 | Standard→URP/Lit 后光照、金属感、光滑度不同 | 用 Render Pipeline Converter 转换,再手调;从现在起统一用 URP/Lit 或 Unlit |
| 粒子/特效材质 | 旧粒子着色器粉红 | 换 URP 粒子着色器(教程特效在 `Tutorial/Art/Effects`,与我们无关) |
| glTFast 运行时加载 | 构建后模型粉红 | 只在编辑器内导入则无事;若运行时加载 glTF,需确保着色器被打进包 |
| TextMeshPro | 一般无问题 | 确认字体材质用 TMP 的 URP 兼容着色器 |
| 首次构建慢 | 着色器编译变体多 | 一次性成本 |
| 相机栈 | 多相机叠加时 XR 异常 | 尽量单相机 |

触觉、抓取、数据记录与渲染管线**无关**,迁移不会影响手套功能。

**预期工作量**:在专用分支 `chore/urp` 上,先用 `Meta.unity` 作为测试场景,按"编辑器 → Meta XR Simulator → Quest 真机"三级验证,约半天到一天。出问题可整支回退,不影响 `main`。

**建议验收清单**
- [ ] 编辑器 Game 视图:手、球、UI 文字正常
- [ ] Meta XR Simulator:能抓球
- [ ] Quest 3 真机:手模型可见、透视(如使用)正常、UI 文字清晰、帧率稳定(目标 72/90 Hz)
- [ ] 手套:抓球有压力
- [ ] Console 无着色器报错

## 5. 执行顺序(确认后)

1. 新建分支 `chore/m0-cleanup`;
2. 3.1 全部删除 + 同步构建设置,提交;
3. 迁 URP、建 `_Project` 骨架、迁 Stage3,分别提交;
4. 配置 Meta XR Simulator,验证;
5. 3.3 在单独分支试删包;
6. 真机构建验证,合并到 `main`。

## 6. 风险与注意

- 在 Unity 编辑器外删文件时,必须**连同 `.meta` 一起删**,否则会留下孤儿 `.meta` 或丢失 GUID。
- 删场景后 `EditorBuildSettings` 必须同步。
- 迁 URP 后,Meta 的部分手部/透视材质可能需要重新指定着色器,需真机确认手的显示正常。
- 每一步后重新打开 Unity 看 Console 有无报错再提交。
