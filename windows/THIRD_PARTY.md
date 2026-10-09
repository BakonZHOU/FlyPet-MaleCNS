# 数据与依赖来源

- **MaleCNS v1.0**：HHMI Janelia FlyEM / Google Research，数据入口 https://male-cns.janelia.org/download/ 。程序包含从本地既有 MaleCNS 衍生缓存提取的小型连接子图。神经元 ID、类型及连接结构来自该数据；本项目的桌宠模型不是数据提供方的官方模拟器。数据使用与再分发请遵守原发布方条款并保留来源。
- **FlyBrain**：https://github.com/HEREISCB/flybrain ，工作区既有版本提交 `c7878b9ee273564186acf8cbdef96289335efa5e`。用于上游缓存生成与甜味神经元索引。内置连接符号含其缓存中的神经递质假设。本项目未把其整个模拟器复制或作为运行依赖。
- **Microsoft .NET / Windows Forms**：https://github.com/dotnet/runtime 和 https://github.com/dotnet/winforms ，MIT。便携包内含 Windows x64 .NET 8 运行时。许可和第三方声明见随附运行时声明文件。
- **Vosk API 与 vosk-model-small-cn-0.22**：https://alphacephei.com/vosk/ ，Apache-2.0。语音模型由用户运行安装脚本后下载到本机；语音数据不会发送到网络服务。
- **NAudio**：https://github.com/naudio/NAudio ，MIT。用于读取 Windows 默认麦克风。
- 苍蝇模型、颗粒色块、托盘图形、界面和行为代码为本次独立实现。未使用 Buckshot Roulette 的游戏模型、纹理、音效或商标资产。

工程参考：

- Win32 分层窗口与鼠标穿透：https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features
- .NET 自包含单文件发布：https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview
