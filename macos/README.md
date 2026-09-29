# FlyPet for macOS

原生 AppKit 桌宠前端，支持 macOS 13 及以上系统。菜单栏图标使用 18×18 template image，会自动适应浅色/深色主题；Finder 应用图标由 16–1024 px 全套位图生成并打包为 `FlyPet.icns`。

```bash
./package.sh macos-arm64
./package.sh macos-x64
./package.sh macos-universal
```

输出位于 `dist/`。GitHub Actions 同时生成 Apple Silicon、Intel 和 Universal 2 三种 DMG。当前公开构建使用 ad-hoc 签名，尚未使用付费 Apple Developer ID 公证；若 Gatekeeper 阻止首次运行，请在 Finder 中右键应用并选择“打开”。
