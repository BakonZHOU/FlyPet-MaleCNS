# 本地安装包

`latest/` 只存放当前版本的安装包，不参与源码提交：

- `FlyPet-<版本>-windows-x64.zip`
- `FlyPet-<版本>-macOS-arm64.dmg`
- `FlyPet-<版本>-macOS-x86_64.dmg`
- `FlyPet-<版本>-macOS-arm64-x86_64.dmg`

Windows 使用 `./package.ps1 windows-x64` 或 `windows-arm64`；macOS 使用 `./package.sh macos-arm64`、`macos-x64` 或 `macos-universal`。临时构建文件只写入根目录 `.build/`。
