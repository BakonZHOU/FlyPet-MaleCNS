# FlyPet · 果蝇桌宠

Windows 10/11 x64 独立桌宠，放在 `FlyPet/`，与原来的 `FlyBrain/`、总界面和其他分区分开。双击 `dist/FlyPet.exe` 即可使用；便携 EXE 自带 .NET 运行时，正常运行无需 Python、联网或 GPU。

程序启动后直接放飞苍蝇，不再弹出启动控制中心。需要控制中心时，在右下角托盘图标右键选择“启动菜单”；设置窗口也从托盘进入。托盘菜单还包括投放糖、显示状态条、无敌模式、立即复活、大脑活动图、隐藏和退出。没有左下角控制栏，也没有苍蝇拍切换模式。苍蝇一直把靠近的鼠标当作威胁并躲避；普通桌面点击穿透，直接点到苍蝇时才会击中它。托盘图标悬停文字显示生命与饱腹百分比。

生命降到零后，苍蝇会变成自己绘制的低多边形血污泥与断翅，随机等待 12–40 秒复活；托盘的“立即复活”跳过等待。无敌模式下拍打仍会让它短暂警觉、加速逃跑，但不扣血。被击中后的较小速度增益持续到下一次复活。飞行消耗额外饱腹值；达到饱腹阈值后会逐渐回血。糖需要持续接触并由进食回路保持激活一段时间才会吃完。饱腹度、糖、威胁和休息共同影响感觉输入及飞行欲望。屏幕边缘进入视觉运动回路；真实撞到边界时会物理反弹、不掉血，并产生负面奖励以降低该位置的记忆价值。它会短暂停歇，振翅停止。绘制目标 90 FPS，可在 20–120 FPS 间调整。

外观是自己生成的低多边形 3D 几何与低分辨率贴图效果，有透视、按位置变化的视角、深度排序、动态翅膀和接触阴影。没有使用 Buckshot Roulette 的原始资源。当前默认大小为 90 像素、基础速度为 400 像素/秒；设置中可以继续调高。

苍蝇死亡后，鼠标悬停在泥状残骸上会出现暗色复古选项框：“复活吧我的爱人”和“清理”。白眼果蝇会在初次加载或复活时按概率出现，具有浅色身体、白眼、更高生命和更快速度；出现时托盘会弹出“出金了！是白眼果蝇！”。

## 大脑与真实性

内置回路从本机 MaleCNS v1.0 数据导出，共 16,711 个神经元、2,075,126 条非零有符号连接，约为完整神经元数量的十分之一。[MaleCNS 数据下载页](https://male-cns.janelia.org/download/)提供神经元注释与连接权重；本程序内嵌提取结果，不需要运行时访问原始大文件。除了原有 LC4/LPLC2 视觉、DNp01 逃逸、飞行/转向/翅肌、甜味 GRN 和 MN9 回路，现在还包含视觉运动、嗅觉、蘑菇体 Kenyon Cell/MBON、DAN/PAM/PPL 奖励、中央复合体导航、下行与运动神经元以及它们的强连接伙伴。

糖会产生随距离衰减的嗅觉场，方向性嗅觉活动参与转向。每块糖只能触发一次进食；进食产生正奖励，撞击边缘产生负奖励。位置由一组真实蘑菇体神经元做稀疏编码，奖励调制其可塑记忆值，因此重复在相近位置投糖会提高该区域的回忆优先级。多个撞击位置共同形成局部排斥方向，再经视觉运动、导航和下行转向输出改变飞行方向。记忆只属于当前这只果蝇；死亡复活、立即复活或重新启动程序都会从空白记忆开始。

每个神经元在 1 ms 时间步按带突触电流、膜电位、阈值、不应期和传输延迟的简化 LIF 方程更新；放电沿内置真实拓扑传播。视觉/甜味感觉群接受人工编码的外部电流，运动群不接受直接写入的行为命令。输出群的放电率参与飞行、转向、逃逸和进食的身体解码。翅肌群对实际飞行读出更有用；MN9 在这个缩减回路的默认条件下不稳定放电，因此进食量由甜味 GRN 活动调制。

**这不是 Google 提供的“完整果蝇大脑模型”，也不是已经验证的生物电生理重建。** 数据文件托管地址含 Google Cloud Storage，并不表示 Google 训练或验证了桌宠的大脑。感觉编码、饥饿与休息、受伤兴奋、身体动力学、血量、随机复活和屏幕碰撞仍是明确编写的程序规则；连接符号来自原 `FlyBrain` 缓存的递质假设，突触强度经过截断与归一化。不能宣称所有行为都从连接组自行涌现。

“大脑活动图”显示原始注释给出的胞体 XY/XZ 投影：每个点是一个 bodyId，亮度来自本程序实时计算的放电率。点击点可看类型、侧别、递质、坐标、膜电位、放电率及最强输入/输出；下面保留最近 60 秒的群组活动热图。可以在图中开关突触传播、边缘感觉输入和神经转向读出，观察行为差异。切断突触后，感觉群仍可能放电，但正常的运动群输出和飞行会消失。“神经连接证据”另提供同随机种子、同刺激下的消融对照。这些操作证明软件的计算依赖已加载的边，不能证明它和真实苍蝇的功能完全一致。

## 自己核对数据，而不是相信测试报告

`tools/verify_source.py` 独立于提取器和模拟器。它先把本机原始胞体注释文件算出 SHA-256，与 `FlyBrain/flybrain/data/manifest.json` 中官方下载文件记录比较；再核对全部 16,711 个 bodyId、类型、侧别、胞体坐标和全部 2,075,126 条有符号连接，与本机 `FlyBrain/cache` 中的编译矩阵逐一比较。加 `--hash-weights` 还会计算约 1.1 GB 原始连接权重文件 SHA-256，便于和[官方来源](https://male-cns.janelia.org/download/)比对。

```powershell
cd 'E:\Workspace2\project2(brain)'
& .\FlyBrain\.venv\Scripts\python.exe .\FlyPet\tools\verify_source.py --hash-weights
```

也可直接打开 `FlyPet/Assets/circuit.json` 查任一节点 `bodyId` 和任一边 `[突触前索引, 突触后索引, 有符号计数]`，在活动图点同一节点看实时电压/放电率；阅读 `Brain.cs` 的 `SetInput`、`Step` 及 `Simulation.cs` 的 `Update` 可看到刺激如何变成神经活动再到动作。哈希、原始文件及独立脚本比本程序自己生成的测试 JSON 更容易复核，但仍不能验证生物学行为预测；那需要真实神经记录与损毁实验。

## 设置与开发

设置文件在 `%LOCALAPPDATA%\FlyPet\settings.json`，可在设置窗口修改并从托盘重新加载。主要自定义项包括 `FramesPerSecond`（默认 90）、`PetSize`（默认 90）、`FlightSpeed`（默认 400）、`HungerPerMinute`（默认 0.8）、`FlightFullnessCostPerSecond`（默认 0.008）、`SugarEatingSeconds`（默认 1.8 秒）、`SatiatedThreshold`（默认 85）、`SatiatedRegenPerSecond`（默认每秒 1.5）、`AlarmSeconds`、`AlbinoChance`（默认 0.1）、`AlbinoSpeedMultiplier`、`NeuralGain`、`SensoryGain`、`EdgeSensing`、`NeuralSteering`、`RestEnabled`、`Invincible`、`ShowMeters`（默认关闭）、复活时间、糖的感知距离以及显示器索引。默认从 65 饱腹出生，即使按持续飞行的上限估算也能活约一小时；登录 Windows 自动启动可在设置中开启，默认关闭。高级使用者可把符合 `Assets/circuit.json` 格式的自定义回路放到设置目录作为覆盖文件；移走后恢复内置回路。

现有实例可以接收本地命名管道命令，例如 `dist/FlyPet.exe --command brain-map`、`--command revive`、`--command invincible on`、`--command status --output <路径>`。完整命令还包括 `show`、`hide`、`menu`、`settings`、`evidence`、`audit`、`pause`、`resume`、`normal`、`sugar-mode`、`drop x y`、`swat x y`、`recall`、`clear`、`reload`、`exit`。`swatter` 仅作为旧脚本兼容别名，不会显示苍蝇拍模式；`swat x y` 仍可用于自动化测试点击。`status.json` 定期写到设置目录。

重新编译：安装 .NET 8 SDK，在 `FlyPet/` 运行 `./build.ps1`，会生成独立便携的 `dist/FlyPet.exe`。若需重新从原始缓存提取回路，先运行 `..\FlyBrain\.venv\Scripts\python.exe tools\extract_circuit.py`。`dotnet bin/Release/net8.0-windows/FlyPet.dll --self-test test-results` 可做逻辑、学习、碰撞、外观和纯计算性能检查。当前本机 Release 自测覆盖神经飞行、逃逸转向、渐进进食、饱腹回血、边缘负面记忆和新个体记忆清空；纯模拟与几何绘制性能也纳入检查。这是计算基准，不等同于 Windows 桌面实际 FPS。运行时 `status.json` 提供实测 FPS、嗅觉、奖励、记忆置信和碰撞次数。每次只在选定显示器工作区域内生活；安全桌面、锁屏与独占全屏不保证置顶。错误日志在 `%LOCALAPPDATA%\FlyPet\error.log`。
