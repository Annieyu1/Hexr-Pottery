# 交接：把折纸茶花园放到 Hub 菜单的牵手页（T1）

2026-10-22 · 上一个会话写的。任务很小：**只改牵手页的立体件，场景里其他东西一律不动。**

## 必须遵守
- **不要运行 `HexRMeta > Learning > Build Hub Menu`，也不要调用 `LearningSceneBuilder.BuildHub()`。** 它会从模板重建整个 `Hub_Menu.unity`，用户手动改过的东西会全部丢掉。
- 动场景前：确认场景没有未保存改动（有就先另存备份，例如 `Assets/Learning/Scenes/Hub_Menu_backup_<日期>.unity`），不要没保存就切换场景。
- 编译、测试用 Unity MCP 自己做；Play 模式里不要截图（之前在 OVRPlugin 里崩过），在编辑模式截图。
- 不改 Blender 文件；不 commit / push，除非用户要求。

## 已有的东西
- 模型：`Assets/Art/Props/Hub/Hub_Garden.fbx`（2 536 三角面），由 `art/scripts/build_hub_garden.py` 生成，说明见 `docs/art/hub-garden-brief.md`。FBX 根物体自带 (-90,0,0) 的轴向修正，要保留。
- 场景：`Assets/Learning/Scenes/Hub_Menu.unity`，目标物体 `PopUpBook/Spread_T1_HandHold/PopUp`（上面有 `PopUpSpread`，下面现在是占位的 `Backdrop_Hinge`、`Hero_Hinge`）。`PopUpBook` 里牵手页已经引用这个 `PopUp`，不用改。
- 同样做法的现成参考：第 4 页书房 `Spread_T11_OpenBottle/PopUp`（`StudyRoom` + 8 个铰链）。代码在 `Assets/Learning/Editor/LearningSceneBuilder.Hub.cs`：`StudyRoomModelPopUp`、`PlaceRoom`（转 180°+45°、缩放、放到纸面拱起的最高点上）、`GroupHinge`（在折线处建铰链、保持世界位置把组挪进去、配 `PopUpHinge`）、`FinishPopUp`、`SwayGroup`。这些方法依赖 `hubW / hubD / hubZ / hubSurface`，只在整场景生成时才设好——**单独调用前要先给它们赋值**（可以从场景里量：书页 `BookOpen/.../Page_R` 的范围，`hubSurface` 用临时 MeshCollider 往下打射线，参考 `HubModelBook`），或者干脆照抄书房 `StudyRoom` 的 Transform。
- 运行时脚本：`Assets/Learning/Scripts/Hub/PopUpHinge.cs`、`PopUpSpread.cs`（`Configure(hinges, sways)`、`SetProgress`）。

## 要做的
1. 删掉 `Spread_T1_HandHold/PopUp` 下的 `Backdrop_Hinge`、`Hero_Hinge`。
2. 把 `Hub_Garden.fbx` 实例化到这个 `PopUp` 下并 Unpack Completely。位置：和书房一样（最简单是复制 `Spread_T11_OpenBottle/PopUp/StudyRoom` 的 localPosition / localRotation / localScale；两者地面都是 0.62 m 见方）。
3. 按下表建铰链（`PopUp` 的子物体，rotation 0），把 `Garden_Paper` 下对应的组移进去（保持世界位置），配 `PopUpHinge`（Open Angle 都是 0）：

| 铰链 | 组 | 折线位置 | Axis | Folded | Start–End | Overshoot | Folded Scale |
|---|---|---|---|---|---|---|---|
| `Stage_Hinge` | `Floor`、`Path` | 草地中心、地面 | (1,0,0) | 0 | 0–0.2 | 0 | (0.05,1,0.05) |
| `Back_Hinge` | `Hinge_Back` | 草地最后的角、地面 | (1,0,0) | -90 | 0.1–0.5 | 1 | (1,1,0.15) |
| `Table_Hinge` | `Table` | 桌脚前沿、地面 | (1,0,0) | -90 | 0.3–0.65 | 1.4 | (1,1,0.1) |
| `Stools_Hinge` | `Stools` | 地面中心 | (1,0,0) | -90 | 0.45–0.75 | 1.4 | (1,1,0.15) |
| `TreeL_Hinge` | `Tree_L` | 树干底 | (0,0,1) | 90 | 0.4–0.75 | 1.2 | (0.15,1,1) |
| `TreeR_Hinge` | `Tree_R` | 树干底 | (0,0,1) | -90 | 0.45–0.8 | 1.2 | (0.15,1,1) |
| `FlowersL_Hinge` | `Flowers_L` | 花丛底 | (0,0,1) | 90 | 0.6–0.9 | 1.5 | (0.15,1,1) |
| `FlowersR_Hinge` | `Flowers_R` | 花丛底 | (0,0,1) | -90 | 0.62–0.95 | 1.5 | (0.15,1,1) |

   `GroupHinge(pop, name, parts, Pivot.Front/Back/Centre, axis, folded, 0, start, end, overshoot, flatScale, hinges, sway, sways)` 可以直接做这一步（Back 对应 `Pivot.Back`，桌子/石凳 `Pivot.Front`，其余 `Pivot.Centre`）；树和花可加摆动 `new SwaySpec(3f, 4f, 0.3f)`。
4. 用 `PopUpSpread.Configure(hinges, sways)` 写入 8 个铰链和摆动，`SetProgress(1)`。
5. 保存场景；编辑模式截图检查（把 `Spread_T2_Piano` 的子物体暂时关掉、`Spread_T1_HandHold` 的子物体打开，截完不要保存这个显示状态——重新打开场景）。
6. Play 测试：`PopUpBook.GoTo(1)` 翻到牵手页，看 `CurrentSpread.popUp.Progress` 到 1；再 `GoTo(0)` 回琴房；看 Console 没有新错误。
7. 用 `git diff --stat` 确认只改了 `Hub_Menu.unity`（以及如果加了代码，`LearningSceneBuilder.Hub.cs`）；在 `docs/14-hub-menu-book.md` 加一小节"花园模型"。

## 完成标准
牵手页显示花园，进度 0→1 时按上表顺序立起来，翻页时折回；其他三页和书、书签、按钮完全没变；场景已保存；没有提交。
