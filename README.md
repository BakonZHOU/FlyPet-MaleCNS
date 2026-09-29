# FlyPet · 果蝇 / 广东双马尾桌宠

一只会躲避鼠标、寻找糖粒、记住碰撞位置的桌面果蝇。可在果蝇与“广东双马尾”外观之间切换，并从系统托盘/菜单栏控制。

## 下载与安装

从 [Releases](../../releases) 下载对应文件；不需要 Python、GPU 或额外运行时。

| 你的设备 | 下载文件 | 安装方式 |
| --- | --- | --- |
| Windows 10/11，Intel/AMD 电脑 | `FlyPet-*-windows-x64.zip` | 解压后双击 `FlyPet.exe` |
| Mac（M1/M2/M3/M4） | `FlyPet-*-macOS-arm64.dmg` | 打开 DMG，将应用拖到“应用程序” |
| Intel Mac | `FlyPet-*-macOS-x86_64.dmg` | 打开 DMG，将应用拖到“应用程序” |
| 不确定 Mac 芯片 | `FlyPet-*-macOS-arm64-x86_64.dmg` | 通用版，兼容两种 Mac |

macOS 首次打开如出现安全提示，请在 Finder 中按住 Control 点按应用，再选择“打开”。发布包采用 ad-hoc 签名；尚未使用 Apple Developer ID 公证。

## 主要功能

- 鼠标靠近时逃逸，桌面边缘会反弹并形成负面位置记忆。
- 再次接近记忆边缘时会提前转向和减速；小窝/菜单会显示边缘经验、成功回避和累计撞击。
- 通过托盘/菜单栏投糖、暂停、复活、召回和切换外观。
- 果蝇与广东双马尾两套动态外观，包含稀有个体和可调尺寸。
- Windows 版提供大脑活动图、神经连接证据和本地命令控制。

![蟑螂皮肤的奔跑与飞行动画](windows/Assets/cockroach-preview.png)

## 开源代码结构

| 目录/文件 | 内容 |
| --- | --- |
| `windows/` | Windows 桌宠、神经回路、资源和 PowerShell 打包脚本 |
| `macos/` | macOS 原生 AppKit 前端、共享脑引擎、图标和 DMG 打包脚本 |
| `releases/latest/` | 本地当前版本的 ZIP/DMG 安装包；不放编译缓存或源码 |
| `windows/Assets/circuit.json` | 内置的 MaleCNS 回路数据 |
| `windows/tools/` | 回路提取、来源核对与冒烟测试脚本 |
| `.github/workflows/build.yml` | Windows/macOS 自动构建、架构验证和标签发布 |

## 本地打包

Windows：`./package.ps1 windows-x64`（也支持 `windows-arm64`）。macOS：`./package.sh macos-arm64`、`./package.sh macos-x64` 或 `./package.sh macos-universal`。所有安装包均写入 `releases/latest/`，临时编译文件统一写入 `.build/`；完整的 Mac 构建说明见 [macOS 说明](macos/README.md)。

## 大脑与真实性

内置回路从本机 MaleCNS v1.0 数据导出，共 16,711 个神经元、2,075,126 条非零有符号连接，约为完整神经元数量的十分之一。[MaleCNS 数据下载页](https://male-cns.janelia.org/download/)提供神经元注释与连接权重；本程序内嵌提取结果，不需要运行时访问原始大文件。Windows 与 macOS 都调用同一份 `Brain.cs` / `Simulation.cs`：macOS 的 AppKit 前端通过内置、免额外安装的自包含引擎进程驱动桌宠，而非只随包附带 JSON。除了原有 LC4/LPLC2 视觉、DNp01 逃逸、飞行/转向/翅肌、甜味 GRN 和 MN9 回路，现在还包含视觉运动、嗅觉、蘑菇体 Kenyon Cell/MBON、DAN/PAM/PPL 奖励、中央复合体导航、下行与运动神经元以及它们的强连接伙伴。

糖会产生随距离衰减的嗅觉场，方向性嗅觉活动参与转向。每块糖只能触发一次进食；进食产生正奖励，撞击边缘产生负奖励。位置由一组真实蘑菇体神经元做稀疏编码，奖励调制其可塑记忆值，因此重复在相近位置投糖会提高该区域的回忆优先级。每次撞击默认强化约 300 像素直径内的位置负价值，并增强边缘感觉到导航群的可塑增益。再次接近时，程序根据速度估算预计碰撞时间，把记忆风险和向内方向送入视觉运动及中央复合体导航群；只有这些群产生实际放电，才会增强转向并制动。成功提前转开会激活奖励群并继续巩固回避经验。状态栏会依次显示“强化危险记忆”“回忆危险，提前转向”“成功避开记忆边缘”，让学习过程可见。记忆只属于当前这只果蝇；死亡复活、立即复活或重新启动程序都会从空白记忆开始。

在 1920×1080 的快速压力测试中，随机撞击覆盖整圈记忆的中位数为 40 次；完成覆盖后，从四边 24 个位置、0–180 px/s 朝边缘接近，碰撞由 24 次降到 2 次。这个结果是确定条件下的软件验收，不代表永不碰撞：新位置、极端速度、鼠标逃逸和屏幕角落仍可能造成碰撞。

每个神经元在 1 ms 时间步按带突触电流、膜电位、阈值、不应期和传输延迟的简化 LIF 方程更新；放电沿内置真实拓扑传播。视觉/甜味感觉群接受人工编码的外部电流，运动群不接受直接写入的行为命令。输出群的放电率参与飞行、转向、逃逸和进食的身体解码。翅肌群对实际飞行读出更有用；MN9 在这个缩减回路的默认条件下不稳定放电，因此进食量由甜味 GRN 活动调制。

**这不是 Google 提供的“完整果蝇大脑模型”，也不是已经验证的生物电生理重建。** 数据文件托管地址含 Google Cloud Storage，并不表示 Google 训练或验证了桌宠的大脑。感觉编码、饥饿与休息、受伤兴奋、身体动力学、血量、随机复活和屏幕碰撞仍是明确编写的程序规则；连接符号来自原 `FlyBrain` 缓存的递质假设，突触强度经过截断与归一化。不能宣称所有行为都从连接组自行涌现。

“大脑活动图”显示原始注释给出的胞体 XY/XZ 投影：每个点是一个 bodyId，亮度来自本程序实时计算的放电率。点击点可看类型、侧别、递质、坐标、膜电位、放电率及最强输入/输出；下面保留最近 60 秒的群组活动热图。可以在图中开关突触传播、边缘感觉输入和神经转向读出，观察行为差异。切断突触后，感觉群仍可能放电，但正常的运动群输出和飞行会消失。“神经连接证据”另提供同随机种子、同刺激下的消融对照。这些操作证明软件的计算依赖已加载的边，不能证明它和真实苍蝇的功能完全一致。

## 自己核对数据，而不是相信测试报告

`windows/tools/verify_source.py` 独立于提取器和模拟器。它先把本机原始胞体注释文件算出 SHA-256，与 `FlyBrain/flybrain/data/manifest.json` 中官方下载文件记录比较；再核对全部 16,711 个 bodyId、类型、侧别、胞体坐标和全部 2,075,126 条有符号连接，与本机 `FlyBrain/cache` 中的编译矩阵逐一比较。加 `--hash-weights` 还会计算约 1.1 GB 原始连接权重文件 SHA-256，便于和[官方来源](https://male-cns.janelia.org/download/)比对。

独立来源核对需要把本项目放在已下载 MaleCNS/FlyBrain 原始缓存的工作区旁，再运行：

```powershell
python .\windows\tools\verify_source.py --hash-weights
```

也可直接打开 `windows/Assets/circuit.json` 查任一节点 `bodyId` 和任一边 `[突触前索引, 突触后索引, 有符号计数]`，在活动图点同一节点看实时电压/放电率；阅读 `windows/Brain.cs` 的 `SetInput`、`Step` 及 `windows/Simulation.cs` 的 `Update` 可看到刺激如何变成神经活动再到动作。哈希、原始文件及独立脚本比本程序自己生成的测试 JSON 更容易复核，但仍不能验证生物学行为预测；那需要真实神经记录与损毁实验。

## 设置与开发

设置文件在 `%LOCALAPPDATA%\FlyPet\settings.json`，可在设置窗口修改并从托盘重新加载。设置窗口可调大小，顶部按“外观 / 生存 / 行为 / 神经 / 系统”分类，当前栏目会高亮。外观包括两种皮肤、尺寸和稀有概率；生存包括饱腹消耗、饥饿失血与复活时间。尺寸范围为 60–800 px。“恢复默认”会保存并应用默认值。高级使用者可把符合 `windows/Assets/circuit.json` 格式的自定义回路放到设置目录作为覆盖文件；移走后恢复内置回路。

现有实例可以接收本地命名管道命令，例如 `dist/FlyPet.exe --command brain-map`、`--command revive`、`--command skin cockroach`、`--command skin fly`、`--command invincible on`、`--command status --output <路径>`。完整命令还包括 `show`、`hide`、`menu`、`settings`、`evidence`、`audit`、`pause`、`resume`、`normal`、`sugar-mode`、`drop x y`、`swat x y`、`recall`、`clear`、`reload`、`exit`。`swatter` 仅作为旧脚本兼容别名，不会显示苍蝇拍模式；`swat x y` 仍可用于自动化测试点击。`status.json` 定期写到设置目录。

重新编译：安装 .NET 8 SDK，在 `FlyPet/` 运行 `./build.ps1`，会生成独立便携的 `dist/FlyPet.exe`。若需重新从原始缓存提取回路，先运行 `..\FlyBrain\.venv\Scripts\python.exe tools\extract_circuit.py`。`dotnet bin/Release/net8.0-windows/FlyPet.dll --self-test test-results` 可做逻辑、学习、碰撞、外观和纯计算性能检查。当前本机 Release 自测覆盖神经飞行、逃逸转向、渐进进食、饱腹回血、边缘负面记忆和新个体记忆清空；纯模拟与几何绘制性能也纳入检查。这是计算基准，不等同于 Windows 桌面实际 FPS。运行时 `status.json` 提供实测 FPS、嗅觉、奖励、记忆置信和碰撞次数。每次只在选定显示器工作区域内生活；安全桌面、锁屏与独占全屏不保证置顶。错误日志在 `%LOCALAPPDATA%\FlyPet\error.log`。
