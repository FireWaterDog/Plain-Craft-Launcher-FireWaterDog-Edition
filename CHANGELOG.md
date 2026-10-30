# PCL-FE 修改日志

> 版本规则：每修改一次（发布一次更新），版本号末位 +0.0.0.1；
> 当末位到达 3.0.0.**10** 时进位为 3.0.1.**0**，之后以此类推（3.0.1.10 → 3.0.2.0）。
> 每个版本发布时同步更新本文件和 `version.json` 的 `versionName` / `changelog`。

---

## 3.0.0.0（2026-10-30）

首个 3.x 正式版，已发布至 GitHub Release（tag `PCLFE`，资源 `Plain.Craft.Launcher.2.exe`，sha256 `5bd7d0af…03608b`）。

### 新功能
- **插件系统迁移到 VB.NET**：插件改为 `plugin.dll`（VB.NET 编译），启动器通过 `Assembly.Load` 反射加载，提供 `PluginBase` / `IPluginHost` / `PluginAction` / `IPluginGameEvent` 插件接口。
- **grand 构建工具**：命令行工具，支持 `new`（创建插件工程）/ `build`（编译）/ `pack`（打包 `.pclfePlugin`，未编译或源码更新时自动构建）/ `install`（安装插件）；找不到启动器 exe 时给出可操作提示并在 `bin` 下递归查找。
- **插件编制器**：支持多文件插件工程编辑、打包，自动创建 grand 工程文件；切换文件时正确保存各文件内容。
- **插件管理**：设置页新增插件管理（启停 / 删除 / 查看信息），附带一个内置日志插件。
- **音乐列表示例插件**：加载 `PCL\music` 目录中的 MP3 文件。
- **土豆码解锁**：插件管理入口默认隐藏，使用启动器自带的土豆码输入（设置 → 秘密 → 土豆码）输入 `FireWaterDog` 解锁；插件管理标注 Beta。

### 修复
- 远古版本实际已启动但启动器界面仍停留在"启动中"的问题（改为按 `McInstanceState.Old` 判定兜底，不中断窗口扫描，避免误伤快照版本）。
- 远古版本获取 Java 速度慢的问题。
- 插件更新时保留 `data` 数据目录。
- 其他编译错误修复（`Dir`/`Name` 与 VB 内置函数冲突、`grand.json` 路径反斜杠转义等）。

### 其他
- grand 构建工具从仓库移除，改为本地保留（加入 `.gitignore`）。
