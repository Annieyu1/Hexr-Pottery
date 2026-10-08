# HexR-Meta

面向轻中度中风后患者的**手部触觉感知辨别康复**VR 游戏。
Meta Quest 3 + Microtube HexR 触觉手套(配合 FlexR 手指运动采集)。

> 状态:框架设计阶段,科研版(新加坡 pilot)优先。

## 项目内容

三个独立场景的小游戏,共用一个训练核心框架:

1. 压力力度分辨
2. 振动频率 / pattern 分辨
3. 手指定位分辨

初级采用二选一(2AFC),中级采用三选一找不同(3AFC);视觉辅助逐步淡出,最终仅靠触觉。

## 文档

开发文档在 [`docs/`](docs/README.md):架构、范式与阈值、游戏设计、数据与临床、仓库工作流、未决问题、Future Work。

## 环境

| 项 | 版本 |
|---|---|
| Unity | 6000.6.0f1 |
| Meta XR SDK | `com.meta.xr.sdk.all` 205.0.0 |
| HexR 包 | `com.microtube.hexr` v0.9.3(git 依赖,见 `Packages/manifest.json`) |
| 构建目标 | Android(Meta Quest 3) |

## 打开与运行

1. 用 Unity 6000.6 打开本目录,切换到 Android 平台。
2. 手套蓝牙需要在头显中授予权限:*Meta Quest → Settings → Privacy and Security → App Permission*,把 **Location** 设为 Precise,**Nearby Device** 设为 On。
3. 打开 `Assets/Scenes/Meta.unity`。

## 许可证与来源

本项目起源于 Microtube 的 [HexR 开发者教程](https://github.com/MicrotubeTechnologies/HexR-Developer-Tutorial-Meta-OVR)(MIT)。许可证见 [LICENSE](LICENSE),第三方声明见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。
