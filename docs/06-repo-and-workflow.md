# 06 Git 仓库与工作流

版本:v0.1 草案 · 2026-10-08

## 1. 现状

- Unity 项目:`/Users/xuzihan/Documents/Lab/Vibration/HexR-Developer-Tutorial-Meta-OVR`
- 远程:`origin` = `Annieyu1/HEXR-Meta`;`upstream` = Microtube 官方教程仓库
- 当前分支:`feature/meta-stage3`(已推送)
- 文档目录:`/Users/xuzihan/Documents/HEXR-Meta/docs`(目前不在 git 里)

## 2. 决策:单一仓库(monorepo),Unity 项目在仓库根

不拆多个仓库,因为:单人开发,代码、配置、文档需要同版本管理。

```
HEXR-Meta/                 ← 仓库根 = Unity 项目根
  Assets/ Packages/ ProjectSettings/
  docs/                    ← 本套文档(从 Documents/HEXR-Meta/docs 迁入)
  tools/                   ← 数据分析脚本(Python/R)、导出转换工具
  data-samples/            ← 仅放匿名的示例/假数据,用于测试;真实数据绝不入库
  ASSET_CREDITS.md         ← 第三方美术/音效来源与许可证
  LICENSE  THIRD-PARTY-NOTICES.md  README.md
```

`docs/`、`tools/` 在仓库根而不在 `Assets/` 内,Unity 不会导入它们。

## 3. 仓库整理(2026-10-08 已执行)

1. ✅ 移除 `upstream` 远程;
2. ✅ `main` 已快进到最新(原 `origin/main` 即官方教程 `latest_version` 同一提交,无分叉),`feature/meta-stage3` 内容全部并入;
3. ✅ 删除本地多余的 `latest_version` 分支;
4. ✅ 保留 `LICENSE`(MIT)和 `THIRD-PARTY-NOTICES.md`——代码来源于 Microtube 教程,许可证要求保留;
5. ✅ 重写根 `README.md`、`docs/` 迁入仓库(见提交记录);
6. 远程 `origin/feature/meta-stage3` 暂保留;之后每个功能开新的 `feature/*` 分支。

## 4. 分支策略(单人,保持简单)

| 分支 | 用途 |
|---|---|
| `main` | 随时可构建、可演示的稳定版本;pilot 用的版本从这里打 tag |
| `develop`(可选) | 日常集成;单人可省略,直接在 feature 上做、合并到 main |
| `feature/<主题>` | 单个功能,如 `feature/core-trial-runner`、`feature/game-pressure` |
| `release/pilot-x.y` | pilot 冻结分支,只修 bug |

- 提交粒度:一个提交一件事;提交信息用英文祈使句,标题 ≤ 72 字符,必要时正文写 why。
- **tag**:`v0.x.y`,pilot 使用的构建必须有 tag,并与 `session.json` 里的 `app_version` 对应。
- 当前 `feature/meta-stage3` 先合并到 `main`(需要先确认 `main` 的状态)。

## 5. Unity 仓库的特别注意

| 事项 | 做法 |
|---|---|
| .meta 文件 | 必须入库;`ProjectSettings/VersionControlSettings` 保持 *Visible Meta Files*(之前被 Unity 改过,已还原) |
| 序列化 | 确认 *Force Text*(默认) |
| 大文件 | 模型、纹理、音频体积大;**建议用 Git LFS**(`*.fbx *.glb *.png *.psd *.wav *.mp3 *.unity?` 视情况)。**启用 LFS 需要在添加大文件之前配置**,否则历史会膨胀 |
| 合并冲突 | 场景/预制体是文本 YAML 但极易冲突;单人开发影响小;仍建议**场景拆小、逻辑放预制体/ScriptableObject** |
| `.gitignore` | 已含 Library/Temp/Logs/UserSettings 与 `.DS_Store`、`.vscode`、`*.slnx`;补:`*.apk`、`*.aab`、构建输出 |
| 密钥 | 不提交签名 keystore 与密码;放仓库外 |
| 数据 | 真实受试者数据**永不入库** |
| 备份 | GitHub 之外再做一份本地备份(如 Time Machine / 外置盘) |

## 6. 构建与发布

- 构建目标:Android(Quest),IL2CPP,ARM64。
- 构建版本号:`Application.version` = 语义化版本;额外写入 git 短 hash(构建脚本注入)。
- pilot 流程:`main` → 打 tag → 构建 APK → 在设备上用 `adb install` 安装 → 记录版本到研究日志。
- 之后可考虑:命令行构建脚本(`tools/build.sh`),避免手点。

## 7. 提交前检查清单(轻量)
- [ ] 能在编辑器里无错误编译
- [ ] 没有无用的 `.meta` 或误改的 ProjectSettings
- [ ] 新增第三方资产已登记到 `ASSET_CREDITS.md`
- [ ] 没有受试者数据、密钥
