# 07 M0:清理与骨架

版本:v0.1 草案 · 2026-10-08 · 状态:**待你确认后执行**(尚未删除任何文件)

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
