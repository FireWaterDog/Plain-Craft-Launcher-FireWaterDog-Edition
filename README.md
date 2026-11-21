# PCLFE · Plain Craft Launcher FireWaterDog Edition

基于 [PCL2（Plain Craft Launcher 2）](https://github.com/Meloong-Git/PCL) 二次开发的 Minecraft 启动器。

> 非 PCL 官方发布；PCL 版权归原作者所有。

- [PCL / PCL2](https://github.com/Meloong-Git/PCL) by 龙腾猫跃
- [EasyTier](https://github.com/EasyTier/EasyTier)

## 平台

| 平台 | 分支 | 说明 |
| --- | --- | --- |
| Windows | `main` | 原生 VB.NET + WPF 程序，下载 `Plain.Craft.Launcher.2.exe` 直接运行 |
| Linux | `Plain-Craft-Launcher-FireWaterDog-Edition-Linux-Edition` | Wine 封装层，界面与功能同 Windows 版 |

### Linux 版

Linux 版不重写界面，而是用 Wine + .NET Framework 4.8 运行同一份程序，并补齐 Linux 原生的安装、依赖、Java、WebView2、自检与打包脚本。适用于 x86_64 Linux（X11 或 Wayland + XWayland）。

```sh
tar xzf pclfe-linux-<版本>.tar.gz
cd pclfe-linux-<版本>
./install.sh
pclfe setup && pclfe update && pclfe run
```

完整说明、兼容性矩阵与排错：[`linux/README.md`](linux/README.md)

从源码安装：

```sh
sh linux/install.sh
sh linux/tests/run-tests.sh      # 冒烟测试，不依赖 Wine
```

Linux 版只新增 `linux/` 目录与独立 CI，**不修改任何 VB / XAML / 工程文件**，可与 `main` 反复合并而不冲突；Windows 版行为完全不受影响。
