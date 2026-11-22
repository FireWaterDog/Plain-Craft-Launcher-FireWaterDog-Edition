# PCLFE Linux Edition

在 Linux 上运行 **Plain Craft Launcher FireWaterDog Edition（PCLFE）** 的官方封装层。

PCLFE 本体是 VB.NET + WPF（.NET Framework 4.8）程序，WPF 只能跑在 Windows 上；本分支不重写界面，而是用 **Wine + .NET Framework 4.8** 把原版程序完整跑起来，并在外面补一层 Linux 原生的安装、依赖、Java、WebView2、自检与打包脚本。

- 界面、功能、插件、皮肤、下载器与 Windows 版**完全一致**，跟着上游发版走
- 只新增 `linux/` 目录，不改动任何 VB 源码，合并上游 `main` 不会冲突
- 支持 x86_64 Linux，X11 / Wayland（XWayland）均可

> 非 PCL 官方发布；PCL 版权归原作者所有。

---

## 一、环境要求

| 项目 | 要求 |
| --- | --- |
| 架构 | x86_64（ARM64 无法运行，Wine 与 .NET 4.8 均为 x86_64） |
| 系统 | 主流发行版（Debian/Ubuntu、Fedora、Arch、openSUSE、Alpine 等） |
| Wine | 8.0 以上，推荐 `wine-staging` 9.x / 10.x |
| winetricks | 必需（用于安装 .NET Framework 4.8） |
| 依赖包 | `cabextract`、`unzip`、`curl`、中文字体（Noto CJK） |
| 磁盘 | 程序与前缀约 2 GB，游戏另计 |
| 显示 | X11，或 Wayland + XWayland；纯 Wayland 无 XWayland 无法启动 |
| 首次初始化 | 约 10–30 分钟（要装 .NET Framework 4.8） |

---

## 二、安装

Linux 版提供四种落地方式，按习惯挑一种即可：

| 形式 | 适用 | 大小 | 特点 |
| --- | --- | --- | --- |
| `pclfe_<版本>_all.deb` | Debian / Ubuntu / Deepin / UOS | 约 2.6 MB | 系统级安装，进应用菜单，`apt remove` 卸载 |
| `pclfe_<版本>+dotnet+wine_all.deb` | 同上，**完全离线** | 约 212 MB | 内置启动器 + .NET 4.8 + 便携 Wine，装完不联网就能跑 |
| `pclfe-<版本>-x86_64.AppImage` | 任意发行版 | 约 3 MB | 单文件免安装，双击即用，不写系统目录 |
| `pclfe-linux-<版本>-offline-net-wine.tar.gz` | 任意发行版 | 约 213 MB | 同上，压缩包形式的全家桶 |
| `pclfe-linux-<版本>-offline.tar.gz` | 任意发行版 | 约 3 MB | 解压装到用户目录，内置启动器 |
| `pclfe-linux-<版本>.tar.gz` | 任意发行版 | 约 70 KB | 同上，但需 `pclfe update` 下载启动器 |

**全家桶包（`+dotnet+wine`）适合谁**：不想折腾 Wine、系统自带的 Wine 太旧、或者目标机器不能联网。安装后首次 `pclfe setup` 会解压内置的 Wine（约 950 MB，1~2 分钟），之后完全离线。

### Debian / Ubuntu（.deb，推荐）

```sh
sudo apt install ./pclfe_<版本>_all.deb     # 自动补齐 wine 等依赖
```

装完在应用菜单里点 **PCLFE**，或终端：

```sh
pclfe setup && pclfe run
```

- 程序本体装在 `/usr/lib/pclfe`，入口 `/usr/bin/pclfe`
- 依赖：`Depends` 只有 `ca-certificates` 与 `curl|wget`；`wine`、`winetricks`、`cabextract`、`unzip`、中文字体放在 `Recommends`，`apt install` 默认会一起装，发行版改了包名也不会导致装不上
- 用 `dpkg -i` 而不是 `apt install` 时不会自动补依赖，装完请跑一次 `pclfe doctor` 看缺什么
- 卸载：`sudo apt remove pclfe`（**不会删除** `~/.local/share/pclfe-linux` 里的游戏与存档）
- 带启动器本体的 .deb 首次运行会把 exe 复制到用户目录，之后内置更新照常可用
- 包内使用 `xz` 压缩（而非新版 dpkg 默认的 zstd），Debian 10/11、Ubuntu 18.04+、Deepin/UOS 等老 `dpkg` 也能安装

### AppImage（单文件，免 root）

```sh
chmod +x pclfe-<版本>-x86_64.AppImage
./pclfe-<版本>-x86_64.AppImage
```

首次运行若提示缺少 FUSE：

```sh
sudo apt install libfuse2        # Ubuntu 22.04+
./pclfe-<版本>-x86_64.AppImage --appimage-extract-and-run
```

AppImage 不安装任何系统文件，数据仍在 `~/.local/share/pclfe-linux`。

### 压缩包

有两种包，按需选：

| 包 | 大小 | 特点 |
| --- | --- | --- |
| `pclfe-linux-<版本>.tar.gz` | 约 70 KB | 只含封装层，安装后需 `pclfe update` 下载启动器 |
| `pclfe-linux-<版本>-offline.tar.gz` | 约 3 MB | 内置启动器本体，安装即可启动，无需联网下载 |

```sh
tar xzf pclfe-linux-<版本>[-offline].tar.gz
cd pclfe-linux-<版本>[-offline]
./install.sh
```

安装器会：

1. 复制程序到 `~/.local/share/pclfe-linux`
2. 建立命令 `~/.local/bin/pclfe`
3. 写入桌面项与图标（应用菜单里能看到 PCLFE）
4. 检查依赖，缺失时打印对应发行版的安装命令
5. 询问是否立即初始化 Wine 前缀（可跳过，稍后 `pclfe setup`）

### 从仓库直接安装

```sh
git clone -b Plain-Craft-Launcher-FireWaterDog-Edition-Linux-Edition \
    https://github.com/FireWaterDog/Plain-Craft-Launcher-FireWaterDog-Edition.git
cd Plain-Craft-Launcher-FireWaterDog-Edition
sh linux/install.sh
```

### 安装参数

| 参数 | 说明 |
| --- | --- |
| `--prefix DIR` | 安装目录，默认 `~/.local/share/pclfe-linux` |
| `--bin DIR` | 命令链接目录，默认 `~/.local/bin` |
| `--from PATH` | 同时从本地 `.exe` / `.zip` / 目录安装启动器本体 |
| `--no-deps` | 跳过依赖检查 |
| `--no-setup` | 跳过 Wine 前缀初始化 |

---

## 三、使用

```sh
pclfe setup      # 首次：创建 Wine 前缀、安装 .NET Framework 4.8、WebView2
pclfe update     # 从发布页下载启动器本体，校验 SHA256
pclfe run        # 启动（不带参数时 pclfe 等同于 pclfe run）
```

| 命令 | 作用 |
| --- | --- |
| `pclfe run [参数...]` | 启动启动器，参数原样传给程序 |
| `pclfe setup [--skip-components] [--skip-webview2]` | 初始化/修复运行环境 |
| `pclfe update` | 按 `version.json` 下载并校验启动器 |
| `pclfe install --from PATH` | 从本地文件安装（支持整包 `bin` 目录） |
| `pclfe java list` | 查看已安装的 Windows 版 Java |
| `pclfe java install [21]` | 安装 Java（默认 21），自动写入 `JAVA_HOME` |
| `pclfe java remove 21` | 删除已安装的 Java |
| `pclfe webview2 install` | 安装 WebView2 运行时（正版登录用） |
| `pclfe prefix status` | 查看前缀、.NET 4.8、WebView2 状态 |
| `pclfe prefix reset` | 重建前缀（旧前缀改名保留） |
| `pclfe prefix winecfg` | 打开 winecfg |
| `pclfe prefix winetricks` | 打开 winetricks |
| `pclfe doctor` | 环境自检，逐项给出修复建议 |
| `pclfe config show / set KEY VAL` | 查看/修改配置 |
| `pclfe logs` | 列出运行日志 |
| `pclfe version` | 版本与路径信息 |

### 数据目录

```
~/.local/share/pclfe-linux/
├── app/          # 启动器本体；默认游戏目录 .minecraft 与 PCL/ 设置、日志都在这里
├── prefix/       # Wine 前缀（.NET 4.8、WebView2、%APPDATA%\Meloong 下的账号与 Java 列表）
├── jre/          # pclfe java install 装的 Windows 版 JRE
├── logs/         # 每次运行的日志
├── cache/        # 下载缓存
├── state/        # 组件安装标记
└── .pclfe-home   # 安装标记：让 <home>/bin/pclfe 反推出自定义安装位置
~/.config/pclfe-linux/config.env   # 配置
```

游戏文件默认在 `~/.local/share/pclfe-linux/app/.minecraft`。想放到别处，在启动器「设置 → 启动 → 游戏目录」里改即可。

备份/迁移时把整个 `~/.local/share/pclfe-linux` 打包带走即可，账号、Java 列表、游戏与存档都在里面。

### 配置项

`pclfe config set KEY VALUE`，或直接编辑 `~/.config/pclfe-linux/config.env`。

| 键 | 默认 | 说明 |
| --- | --- | --- |
| `PCLFE_DPI` | `96` | 界面缩放；96=100%，120=125%，144=150% |
| `PCLFE_DXVK` | `0` | 设为 `1` 用 DXVK（先 `winetricks -q dxvk`） |
| `PCLFE_ESYNC` | `1` | 启用 esync/fsync，降低 CPU 占用 |
| `PCLFE_RENDERER` | `auto` | `auto` 跟随 `PCLFE_DXVK`；`vulkan` 强制 DXVK；`gl` 强制 WineD3D |
| `PCLFE_WINEDEBUG` | `-all` | Wine 调试输出；排查时改 `warn+all` |
| `PCLFE_UPDATE_MANIFEST` | raw + jsDelivr 两路 | 更新清单地址，可用空格分隔多个来源依次回退 |

环境变量优先级高于配置文件，例如 `PCLFE_DPI=120 pclfe run`。

---

## 四、工作原理

### 1. Wine 前缀

`pclfe setup` 创建 64 位前缀，再用 `winetricks` 安装：

| 组件 | 用途 |
| --- | --- |
| `win10` | 模拟 Windows 10，.NET 4.8 与 WebView2 的要求 |
| `dotnet48` | PCLFE 的运行时，缺了直接打不开 |
| `corefonts` / `gdiplus` | 字体与图标渲染 |
| `vcrun2019` | 部分原生组件依赖 |

随后写入注册表：DPI 缩放、关闭崩溃对话框、把浏览器委派给宿主机的 `xdg-open`。

### 2. Java 桥接

**不能直接用 Linux 版 Java。** PCLFE 在 Wine 里通过 `CreateProcess` 调 `java.exe`，Wine 无法可靠执行 Linux ELF 程序，所以必须给它 Windows 版 `java.exe`。

`pclfe java install 21` 会：

1. 从 Adoptium 取 **Windows x64 JRE**（失败时回退清华镜像）
2. 校验 SHA256 后解压到 `~/.local/share/pclfe-linux/jre/temurin-21`
3. 启动时把 `JAVA_HOME` 设为该目录的 `Z:\...` 形式

PCL 的 Java 扫描会读取 `JAVA_HOME` 环境变量，因此能自动收录这个 Java。你也可以在启动器里让它自己下载（会落在前缀的 `.minecraft\runtime`，同样是 Windows 版 Java）。

### 3. 微软正版登录

PCLFE 的正版登录走「授权码流 + 内置浏览器（WebView2）」。Wine 下这条链路的可用性取决于 WebView2 运行时：

- **可以执行** `pclfe webview2 install` 尝试安装（内部先试离线安装包，再试在线引导器）
- **仍然失败**时：`pclfe doctor` 会明确标出；此时只能使用离线登录
- 备选：把 `PCLFE_WEBVIEW2_FOLDER` 指向一份解压好的 WebView2 固定版本运行时（Windows 路径形式，如 `Z:\opt\webview2`）

WebView2 能否跑起来跟 Wine 版本、显卡驱动强相关，本项目不做保证，只保证「失败可诊断、不影响离线登录与启动」。

### 4. 下载与更新

下载全部由 PCL 自己完成（含 BMCLAPI 镜像切换），封装层不代理游戏文件。启动器本体的更新由 `pclfe update` 负责：读取 `version.json` 的 `downloadUrl` 与 `sha256`，下载后强校验，校验不过直接拒绝安装。

启动器内置的自动更新在 Wine 下同样可用（本质是替换 exe），两条路不冲突。

---

## 五、兼容性矩阵

| 功能 | 状态 | 说明 |
| --- | --- | --- |
| 启动器界面 | ✅ 可用 | Wine 下渲染正常，高 DPI 见配置 |
| 版本安装 / 下载 | ✅ 可用 | 由 PCL 自身下载器完成 |
| 离线登录 | ✅ 可用 | 推荐默认使用 |
| 微软正版登录 | ⚠️ 取决于 WebView2 | 见上文第 3 节 |
| 第三方登录（Authlib） | ✅ 可用 | 纯 HTTP 请求 |
| Mod / 整合包管理 | ✅ 可用 | 文件操作在 Wine 内完成 |
| 皮肤（离线/WebP） | ✅ 可用 | 内置 32/64 位原生库 |
| 游戏启动 | ✅ 可用 | 使用 Windows 版 Java |
| 自动更新 | ✅ 可用 | 也可用 `pclfe update` |
| 联机（EasyTier） | ⚠️ 未验证 | 依赖原生组件，可能需要额外 DLL |
| 插件系统 | ⚠️ 部分 | 依赖 Windows 专有 API 的插件可能失败 |

---

## 六、常见问题

**启动时报 `wine: Bad EXE format` 或直接没反应**

```sh
pclfe doctor            # 先看 .NET 4.8 是否真的装上了
pclfe prefix reset      # 前缀损坏时重建（旧前缀会改名保留）
pclfe setup
```

**界面全是方块 / 乱码**

缺中文字体。Debian/Ubuntu：`sudo apt install fonts-noto-cjk`；Arch：`sudo pacman -S noto-fonts-cjk`。

**找不到 `.minecraft` / 中文路径失效**

Wine 在非 UTF-8 locale 下会破坏中文路径。启动脚本已强制 `LANG=C.UTF-8`；若你手工 `wine` 启动，请自行设置。另外确认 `locale -a` 里有 `C.UTF-8` 或 `zh_CN.UTF-8`。

**登录页一片空白**

WebView2 没跑起来，执行 `pclfe webview2 install`；仍失败就改用离线登录，`pclfe doctor` 会给出结论。

**游戏没有声音 / 爆音**

封装层已设 `PULSE_LATENCY_MSEC=60`。仍有问题可试 `PULSE_LATENCY_MSEC=120 pclfe run`，或检查 PipeWire 的 PulseAudio 兼容层是否在跑。

**游戏窗口黑屏 / 花屏**

换渲染路径：`pclfe config set PCLFE_DXVK 1`，并先 `pclfe prefix winetricks` 安装 `dxvk`。NVIDIA 用户还可尝试 `PROTON_ENABLE_NVAPI=1`。

**Java 列表里没有 Java**

```sh
pclfe java install 21
pclfe run
```

然后在启动器「设置 → 启动 → Java」里选中 `Z:\...\jre\temurin-21`。

**下载很慢**

在启动器「设置 → 启动 → 下载源」切到 BMCLAPI；`pclfe update` 也可以换清单镜像：`pclfe config set PCLFE_UPDATE_MANIFEST <url>`。

**`pclfe update` 提示「无法获取版本信息」**

清单默认按 `raw.githubusercontent.com` → `cdn.jsdelivr.net` 两路回退，两者都被网络环境阻断时会失败。此时用能访问的地址替换即可，支持空格分隔多个来源：

```sh
pclfe config set PCLFE_UPDATE_MANIFEST "https://你的镜像/version.json https://raw.githubusercontent.com/..."
```

注意 `downloadUrl` 指向 GitHub Release 资产，若直链也不通，可先用 `pclfe install --from` 安装本地下载好的 exe。

**`dpkg` 报 `unknown compression for member 'control.tar.zst'`**

下载到的是早期用 zstd 打的包，老版本 `dpkg`（Debian 11、Ubuntu 20.04、Deepin/UOS 等）读不了。重新下载当前版本的 `.deb` 即可，现在的包统一用 `xz` 压缩。

**双击应用菜单里的 PCLFE 没反应**

1. **首次运行必须先初始化**，直接双击是打不开的（要建 Wine 前缀 + 装 .NET 4.8）。在终端执行一次：

   ```sh
   pclfe setup
   ```

2. 新版在未初始化时会**弹窗询问是否打开终端**继续。如果连弹窗都没有，说明系统缺对话框工具：

   ```sh
   sudo apt install zenity        # KDE 可用 kdialog
   ```

3. 看日志确认原因：`~/.local/share/pclfe-linux/logs/gui.log`

4. 环境是否齐全：`pclfe doctor`

**双击 AppImage 没反应**

AppImage 需要可执行位：

```sh
chmod +x pclfe-<版本>-x86_64.AppImage
```

部分文件管理器（Nautilus 等）默认不允许运行可执行文件，需在「首选项 → 行为」里允许，或直接在终端 `./pclfe-*.AppImage`。缺 FUSE 时用 `--appimage-extract-and-run`。

**`pclfe setup` 卡在 corefonts / andale32.exe 下载失败**

旧版 winetricks 的字体下载源（`mirrors.kernel.org/gentoo`）早已 404。从 3.0.1.2 起默认**不再安装 corefonts**，并且逐项安装组件——只有 `dotnet48` 失败才算致命。确实需要 Arial 等字体时：

```sh
PCLFE_WINETRICKS_VERBS="win10 corefonts gdiplus vcrun2019 dotnet48" pclfe setup
```

**`pclfe setup` 提示 winetricks 版本过旧 / 没有 dotnet48**

发行版自带的 winetricks 常年不更新（Debian 10 带的是 2018 版，连 `dotnet48` 这个 verb 都没有）。`pclfe setup` 会自动把最新版下载到 `~/.local/share/pclfe-linux/bin/winetricks` 并使用；也可以手动装：

```sh
mkdir -p ~/.local/bin
curl -fsSL https://raw.githubusercontent.com/Winetricks/winetricks/master/src/winetricks -o ~/.local/bin/winetricks
chmod +x ~/.local/bin/winetricks
```

**Wine 版本太旧**

`.NET Framework 4.8` 需要较新的 Wine（建议 **≥ 7**；Wine 4.x 基本装不上）。检查：

```sh
wine --version
```

老发行版（Debian 10、Ubuntu 18.04 等）自带的 Wine 太旧，建议升级系统或改用 [WineHQ 官方源](https://wiki.winehq.org/) 安装新版。

**`pclfe setup` 卡在 dotnet48 / 下载失败**

`.NET Framework 4.8` 的安装包有 110 MB+，winetricks 直连微软 CDN 在国内经常超时。可以先用浏览器下好离线包，再喂给 `pclfe`：

```sh
# 官方离线安装包（英文版，winetricks 认的就是这个）
# https://go.microsoft.com/fwlink/?linkid=2088631
pclfe setup --dotnet-installer=~/下载/NDP48-x86-x64-AllOS-ENU.exe
```

它会自动放进 `~/.cache/winetricks/dotnet48/` 并按 winetricks 认的文件名放一份，跳过联网下载那一步。

如果仍然失败，看日志：

```sh
tail -40 "$(ls -t ~/.local/share/pclfe-linux/logs/setup-*.log | head -1)"
```

**为什么不能直接装 Linux 版 .NET？**

PCL2 是 VB.NET + **WPF** 程序，目标框架 `net48`（.NET Framework 4.8），不是 .NET 8/9。WPF 只有 Windows 实现，Linux 上的 .NET 运行时无法运行它，所以必须在 Wine 里装 Windows 版的 .NET Framework 4.8。微软的 `dotnet-install.sh` 装的是跨平台运行时，帮不上忙。

**界面太小 / 太大**

`pclfe config set PCLFE_DPI 120`（125%），`144` 为 150%。

**怎么彻底卸载**

```sh
cd linux && ./uninstall.sh --purge
```

`--purge` 会删掉数据目录（含游戏与存档），不加只删程序与快捷方式。

---

## 七、开发

```sh
sh linux/tests/run-tests.sh     # 冒烟测试：语法、命令分发、配置读写、路径转换、安装流程
sh linux/build-release.sh       # 打包 pclfe-linux-<版本>.tar.gz + SHA256
sh linux/build-release.sh . --with-exe /path/to/Plain.Craft.Launcher.2.exe   # 离线包
sh linux/build-deb.sh           # 构建 pclfe_<版本>_all.deb（需 dpkg-deb）
sh linux/build-appimage.sh      # 构建 pclfe-<版本>-x86_64.AppImage（需 mksquashfs）
```

测试不依赖 Wine，可在 Windows 的 Git Bash 或任意 Linux 上跑；CI 见 `.github/workflows/linux-package.yml`。

### 分支约定

本分支（`Plain-Craft-Launcher-FireWaterDog-Edition-Linux-Edition`）只允许改动：

- `linux/` 下的全部内容
- `.github/workflows/linux-package.yml`
- `README.md` 的 Linux 章节、`CHANGELOG.md` 的 Linux 条目

**不修改任何 `.vb` / `.xaml` / `.csproj` / `.sln`**，保证与上游 `main` 反复合并无冲突。Windows 版行为不受影响。

### 目录结构

```
linux/
├── bin/pclfe              # 入口与命令分发
├── lib/
│   ├── common.sh          # 路径、日志、下载、路径转换、配置
│   ├── deps.sh            # 依赖探测与发行版安装建议
│   ├── prefix.sh          # Wine 前缀与 winetricks 组件
│   ├── java.sh            # Windows 版 JRE 安装与 JAVA_HOME 桥接
│   ├── webview2.sh        # WebView2 运行时
│   ├── update.sh          # 启动器下载/更新/本地安装
│   ├── runtime.sh         # 启动与环境变量
│   ├── gui.sh             # 桌面弹窗、终端拉起（双击启动时的提示）
│   └── diag.sh            # 环境自检
├── install.sh             # 安装器
├── uninstall.sh           # 卸载器
├── build-release.sh       # 发布打包（tar.gz / 离线包）
├── build-deb.sh           # .deb 打包（系统级安装）
├── build-appimage.sh      # AppImage 打包（单文件应用）
├── packaging/icons/       # 图标（从 Images/icon.ico 提取的 256px PNG）
├── tests/run-tests.sh     # 冒烟测试
└── README.md
```
