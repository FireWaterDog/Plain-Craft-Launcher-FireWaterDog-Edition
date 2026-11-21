#!/usr/bin/env sh
# 环境自检：pclfe doctor。
# 输出每一项的结论与可操作的修复建议；致命问题返回 1。

DIAG_FATAL=0
DIAG_WARN=0

_d_ok()   { printf '  %s✓%s %s\n' "$C_GREEN" "$C_RESET" "$*"; }
_d_warn() { printf '  %s!%s %s\n' "$C_YELLOW" "$C_RESET" "$*"; DIAG_WARN=$((DIAG_WARN + 1)); }
_d_bad()  { printf '  %s✗%s %s\n' "$C_RED" "$C_RESET" "$*"; DIAG_FATAL=$((DIAG_FATAL + 1)); }
_d_hint() { printf '      %s→ %s%s\n' "$C_DIM" "$*" "$C_RESET"; }

diag_run() {
    DIAG_FATAL=0
    DIAG_WARN=0

    log "${C_CYAN}PCLFE Linux Edition 环境自检${C_RESET}"
    log "$(date '+%Y-%m-%d %H:%M:%S')"
    log ""

    # ---------- 系统 ----------
    log "${C_CYAN}[系统]${C_RESET}"
    _os=$(os_id)
    _arch=$(wine_arch)
    _desk=${XDG_CURRENT_DESKTOP:-${DESKTOP_SESSION:-未知}}
    _d_ok "发行版 $_os，架构 $_arch，桌面 $_desk"
    case "$_arch" in
        x86_64) ;;
        aarch64) _d_bad "ARM64 平台：PCLFE 依赖的 Wine 与 .NET Framework 4.8 均为 x86_64，无法运行";;
        *) _d_warn "非 x86_64 架构，可能无法运行";;
    esac
    if [ -n "${WAYLAND_DISPLAY:-}" ] && [ -z "${DISPLAY:-}" ]; then
        _d_warn "当前是纯 Wayland 会话，Wine 需要 XWayland"
        _d_hint "安装 xwayland，或改用 X11 会话登录"
    fi

    # ---------- Wine ----------
    log ""
    log "${C_CYAN}[Wine]${C_RESET}"
    if _wine=$(wine_bin); then
        _wver=$("$_wine" --version 2>/dev/null | tr -d '\r')
        _d_ok "wine：${_wver:-未知}"
    else
        _d_bad "未安装 wine"
        _d_hint "参考 'pclfe setup' 输出的安装命令"
    fi

    if have winetricks; then
        _d_ok "winetricks：$(winetricks --version 2>/dev/null | head -n 1)"
    else
        _d_bad "未安装 winetricks（无法安装 .NET Framework 4.8）"
    fi

    if prefix_exists; then
        _d_ok "Wine 前缀：$PCLFE_PREFIX_DIR"
        if dotnet48_installed; then
            _d_ok ".NET Framework 4.8：已安装"
        else
            _d_bad ".NET Framework 4.8：未安装（启动器无法运行）"
            _d_hint "pclfe setup"
        fi
        if webview2_installed; then
            _d_ok "WebView2 运行时：$(webview2_version)"
        elif [ -n "${PCLFE_WEBVIEW2_FOLDER:-}" ]; then
            _d_warn "WebView2：使用外部目录 $PCLFE_WEBVIEW2_FOLDER"
        else
            _d_warn "WebView2：未安装（微软正版登录不可用，离线登录正常）"
            _d_hint "pclfe webview2 install"
        fi
    else
        _d_bad "Wine 前缀未创建"
        _d_hint "pclfe setup"
    fi

    # ---------- 启动器 ----------
    log ""
    log "${C_CYAN}[启动器]${C_RESET}"
    if _exe=$(runtime_exe); then
        _d_ok "程序：$_exe（$(human_size "$_exe")）"
        _man=$(app_manifest 2>/dev/null || true)
        if [ -n "$_man" ]; then
            _want=$(json_str "$_man" sha256 2>/dev/null || true)
            _have=$(sha256_of "$_exe" 2>/dev/null || true)
            if [ -n "$_want" ] && [ -n "$_have" ]; then
                if [ "$_want" = "$_have" ]; then
                    _d_ok "SHA256 与版本清单一致（$(app_current_version)）"
                else
                    _d_warn "SHA256 与版本清单不一致（本地构建或已修改）"
                    _d_hint "需要官方版本时执行：pclfe update"
                fi
            fi
        else
            _d_warn "缺少 version.json，无法校验版本"
        fi
    else
        _d_bad "未安装启动器程序"
        _d_hint "pclfe update   或   pclfe install --from <文件>"
    fi

    # ---------- Java ----------
    log ""
    log "${C_CYAN}[Java]${C_RESET}"
    _jlist=$(java_installed_list)
    if [ -n "$_jlist" ]; then
        printf '%s\n' "$_jlist" | while IFS= read -r _j; do
            _d_ok "已安装：$_j"
        done
    else
        _d_warn "未通过 pclfe 安装 Windows 版 Java"
        _d_hint "pclfe java install 21（PCL 也会自行下载，可忽略本项）"
    fi
    if [ -n "${JAVA_HOME:-}" ]; then
        _d_ok "JAVA_HOME=$JAVA_HOME"
    fi
    _pclrt="$PCLFE_PREFIX_DIR/drive_c/users/$(whoami)/AppData/Roaming/.minecraft/runtime"
    if [ -d "$_pclrt" ]; then
        _d_ok "PCL 自带的 Java 目录存在：.minecraft\\runtime"
    fi

    # ---------- 字体 ----------
    log ""
    log "${C_CYAN}[字体]${C_RESET}"
    if have fc-list; then
        _cjk=$(fc-list :lang=zh 2>/dev/null | wc -l | tr -d ' ')
        if [ "${_cjk:-0}" -gt 0 ]; then
            _d_ok "中文字体：${_cjk} 个"
        else
            _d_warn "未检测到中文字体，界面可能显示为方块"
            _d_hint "安装 fonts-noto-cjk（Debian/Ubuntu）或 noto-fonts-cjk（Arch）"
        fi
    else
        _d_warn "缺少 fc-list（fontconfig），无法检查字体"
    fi

    # ---------- 图形 ----------
    log ""
    log "${C_CYAN}[图形]${C_RESET}"
    if have glxinfo; then
        _gl=$(glxinfo -B 2>/dev/null | sed -n 's/.*OpenGL renderer string:[[:space:]]*//p' | head -n 1)
        if [ -n "$_gl" ]; then
            _d_ok "OpenGL：$_gl"
        else
            _d_warn "无法读取 OpenGL 信息"
        fi
    else
        _d_warn "未安装 glxinfo，跳过 OpenGL 检查"
        _d_hint "mesa-utils（Debian/Ubuntu）"
    fi
    if dxvk_enabled; then
        _dxvk="$PCLFE_PREFIX_DIR/drive_c/windows/system32/d3d11.dll"
        if [ -f "$_dxvk" ]; then
            _d_ok "DXVK：已启用"
        else
            _d_warn "渲染器已指向 DXVK，但前缀内没有 DXVK"
            _d_hint "pclfe prefix winetricks 然后安装 dxvk"
        fi
    fi

    # ---------- 音频 ----------
    log ""
    log "${C_CYAN}[音频]${C_RESET}"
    if have pactl; then
        if pactl info >/dev/null 2>&1; then
            _d_ok "PulseAudio/PipeWire 可用"
        else
            _d_warn "PulseAudio 未响应，游戏可能没有声音"
        fi
    else
        _d_warn "未安装 pactl（pulseaudio-utils），无法检查音频"
    fi

    # ---------- 其他 ----------
    log ""
    log "${C_CYAN}[其他]${C_RESET}"
    if have xdg-open; then
        _d_ok "xdg-open 可用（登录页与帮助链接会调用系统浏览器）"
    else
        _d_warn "缺少 xdg-open，内置浏览器打不开外链"
    fi
    _free=$(df -Pk "$PCLFE_HOME" 2>/dev/null | awk 'NR==2 {print int($4/1024/1024)}')
    if [ -n "${_free:-}" ]; then
        if [ "$_free" -lt 2 ]; then
            _d_warn "数据目录剩余空间 ${_free}GB，安装游戏可能不够"
        else
            _d_ok "数据目录剩余空间 ${_free}GB"
        fi
    fi
    log ""

    # ---------- 汇总 ----------
    if [ "$DIAG_FATAL" -gt 0 ]; then
        err "发现 $DIAG_FATAL 个致命问题、$DIAG_WARN 个警告"
        dim "致命问题修复后重新运行：pclfe doctor"
        return 1
    elif [ "$DIAG_WARN" -gt 0 ]; then
        warn "未发现致命问题，有 $DIAG_WARN 个警告"
        return 0
    else
        ok "环境检查全部通过"
        return 0
    fi
}
