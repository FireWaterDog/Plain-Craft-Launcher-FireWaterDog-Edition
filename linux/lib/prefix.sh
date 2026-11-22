#!/usr/bin/env sh
# Wine 前缀（prefix）的创建、组件安装与注册表微调。
# PCLFE 本体是 .NET Framework 4.8 + WPF 程序，因此前缀里必须有 dotnet48。

# winetricks 组件：win10 设定 Windows 版本，dotnet48 是运行前提，
# corefonts/gdiplus 保证界面字体与图标正常，vcrun2019 供部分原生组件使用。
# 默认不装 corefonts：它依赖的几个下载源常年 404，且不影响启动器主体。
# 需要时：export PCLFE_WINETRICKS_VERBS="win10 corefonts gdiplus vcrun2019 dotnet48"
PCLFE_WINETRICKS_VERBS="${PCLFE_WINETRICKS_VERBS:-win10 gdiplus vcrun2019 dotnet48}"
PCLFE_COMPONENTS_STAMP="$PCLFE_STATE_DIR/components.stamp"

WINETRICKS_URLS="https://raw.githubusercontent.com/Winetricks/winetricks/master/src/winetricks https://cdn.jsdelivr.net/gh/Winetricks/winetricks@master/src/winetricks"

prefix_exists() { [ -d "$PCLFE_PREFIX_DIR/drive_c" ]; }

prefix_create() {
    if prefix_exists; then
        dim "Wine 前缀已存在：$PCLFE_PREFIX_DIR"
        return 0
    fi
    mkdir -p "$PCLFE_PREFIX_DIR"
    step "创建 64 位 Wine 前缀"
    _wine=$(wine_bin) || die "未找到 wine，请先安装"
    # 禁用 mscoree/mshtml：避免 wineboot 弹「Wine Mono 安装器」去联网下载。
    # 我们要装的是真正的 .NET Framework 4.8，Mono 反而会冲突。
    WINEPREFIX="$PCLFE_PREFIX_DIR" WINEARCH=win64 WINEDEBUG=-all \
    WINEDLLOVERRIDES="mscoree,mshtml=" \
        "$_wine" wineboot -u >/dev/null 2>&1 || {
        err "创建 Wine 前缀失败；可用 'pclfe prefix reset' 清理后重试"
        return 1
    }
    # 首次 wineboot 会异步拉起服务进程，稍等它落盘。
    sleep 2
    prefix_exists || { err "Wine 前缀创建不完整：$PCLFE_PREFIX_DIR"; return 1; }
    ok "Wine 前缀已创建"
}

# .NET Framework 4.8 是否已装好。Release >= 528040 表示 4.8。
dotnet48_installed() {
    prefix_exists || return 1
    _wine=$(wine_bin) || return 1
    _out=$(WINEPREFIX="$PCLFE_PREFIX_DIR" WINEDEBUG=-all "$_wine" reg query \
        'HKLM\Software\Microsoft\NET Framework Setup\NDP\v4\Full' /v Release 2>/dev/null || true)
    _rel=$(printf '%s' "$_out" | tr -d '\r' | sed -n 's/.*Release[[:space:]]*REG_DWORD[[:space:]]*0x\([0-9a-fA-F]*\).*/\1/p' | head -n 1)
    [ -n "$_rel" ] || return 1
    [ "$((0x$_rel))" -ge 528040 ] 2>/dev/null
}

webview2_prefix_check() {
    # WebView2 运行时通常装在 Program Files (x86) 下，版本号目录不固定。
    ls "$PCLFE_PREFIX_DIR/drive_c/Program Files (x86)/Microsoft/EdgeWebView/Application"/*/msedgewebview2.exe \
        >/dev/null 2>&1
}

prefix_components_ready() {
    [ -r "$PCLFE_COMPONENTS_STAMP" ] && dotnet48_installed
}

# dotnet48 的安装器是 32 位程序；只有 64 位 Wine 时基本装不上。
wine32_hint() {
    # 内置的 wow64 版 Wine 自带 32 位支持，无需系统 i386
    wine_vendor_bin >/dev/null 2>&1 && return 0
    case "$(uname -m)" in
        x86_64) ;;
        *) return 0 ;;
    esac
    for _d in /usr/lib/wine/i386-unix /usr/lib/i386-linux-gnu/wine /usr/lib32/wine; do
        [ -d "$_d" ] && return 0
    done
    warn "未检测到 32 位 Wine；.NET 4.8 的安装器是 32 位程序，很可能失败"
    dim "Debian/Ubuntu：sudo dpkg --add-architecture i386 && sudo apt update && sudo apt install -y wine32:i386"
    dim "Fedora：sudo dnf install wine-core.i686"
    return 1
}

# 发行版自带的 winetricks 常年不更新（例如 2018 版连 dotnet48 这个 verb 都没有），
# 优先使用我们下载到数据目录的最新版。
winetricks_bin() {
    if [ -x "$PCLFE_HOME/bin/winetricks" ]; then
        printf '%s\n' "$PCLFE_HOME/bin/winetricks"
        return 0
    fi
    first_of winetricks
}

winetricks_has_dotnet48() {
    _wb=$(winetricks_bin) || return 1
    "$_wb" list-all 2>/dev/null | awk '{print $1}' | grep -qx 'dotnet48'
}

# 必要时下载最新 winetricks 到 $PCLFE_HOME/bin
winetricks_ensure() {
    if winetricks_has_dotnet48; then
        return 0
    fi
    _wb=$(winetricks_bin) || true
    [ -n "${_wb:-}" ] && warn "当前 winetricks（$("$_wb" --version 2>/dev/null | head -n 1)）缺少 dotnet48，改用最新版"

    deps_require_net
    mkdir -p "$PCLFE_HOME/bin" 2>/dev/null || true
    for _u in $WINETRICKS_URLS; do
        dim "下载 winetricks：$_u"
        if download "$_u" "$PCLFE_HOME/bin/winetricks" 90 >&2; then
            chmod +x "$PCLFE_HOME/bin/winetricks" 2>/dev/null || true
            if winetricks_has_dotnet48; then
                ok "已安装 winetricks（$("$PCLFE_HOME/bin/winetricks" --version 2>/dev/null | head -n 1)）"
                return 0
            fi
        fi
    done
    return 1
}

# 直接运行 .NET 4.8 离线安装包（不依赖 winetricks）。
# 适用于：发行版自带 winetricks 过旧且无法联网升级。
prefix_install_dotnet_direct() {
    _installer=$1
    [ -f "$_installer" ] || return 1
    _wine=$(wine_bin) || return 1

    step "直接运行 .NET 4.8 安装包（绕过 winetricks）"
    dim "安装包：$_installer"
    dim "无窗口安装，通常需要 3~10 分钟，请勿中断。"

    WINEPREFIX="$PCLFE_PREFIX_DIR" WINEARCH=win64 \
    WINEDLLOVERRIDES="mscoree,mshtml=" \
        "$_wine" "$_installer" /q /norestart >/dev/null 2>&1 || true

    # 等 wine 后台进程把注册表写完
    WINEPREFIX="$PCLFE_PREFIX_DIR" "$_wine" wineserver -w >/dev/null 2>&1 || true

    if dotnet48_installed; then
        date '+%Y-%m-%d %H:%M:%S' > "$PCLFE_COMPONENTS_STAMP"
        ok ".NET Framework 4.8 安装完成（直装模式）"
        return 0
    fi
    err "直装 .NET 4.8 未成功"
    return 1
}

prefix_install_components() {
    prefix_create || return 1

    if prefix_components_ready; then
        ok ".NET Framework 4.8 等组件已就绪"
        return 0
    fi

    # 安装包里自带 .NET 离线安装包时直接用它，完全不联网
    if [ -z "${PCLFE_DOTNET_INSTALLER:-}" ]; then
        if _b=$(dotnet_installer_bundled); then
            PCLFE_DOTNET_INSTALLER=$_b
            dim "使用包内自带的 .NET 4.8 安装包：$_b"
        fi
    fi

    have winetricks || have curl || warn "未找到 winetricks 且没有 curl，可能无法准备组件"

    # Wine 太老时 .NET 4.8 基本装不上，先把话说在前面。
    _wmaj=$(wine_major 2>/dev/null || echo 0)
    case "$_wmaj" in
        ''|*[!0-9]*) _wmaj=0 ;;
    esac
    if [ "$_wmaj" -lt 6 ]; then
        warn "Wine 版本偏旧（$(wine_version 2>/dev/null)），.NET Framework 4.8 很可能装不上"
        dim "建议 Wine ≥ 7：Debian/Ubuntu 可用 https://wiki.winehq.org/ 的官方源升级"
    fi

    wine32_hint || true

    if ! winetricks_ensure; then
        if [ -n "${PCLFE_DOTNET_INSTALLER:-}" ] && [ -f "$PCLFE_DOTNET_INSTALLER" ]; then
            warn "无法准备支持 dotnet48 的 winetricks，改用直装模式"
            prefix_install_dotnet_direct "$PCLFE_DOTNET_INSTALLER" || return 1
            return 0
        fi
        die "无法准备支持 dotnet48 的 winetricks；可用 --dotnet-installer=<离线包> 走直装模式"
    fi

    # 允许喂本地离线安装包：winetricks 会优先用缓存，省掉它自己的慢速下载。
    if [ -n "${PCLFE_DOTNET_INSTALLER:-}" ]; then
        if [ -f "$PCLFE_DOTNET_INSTALLER" ]; then
            _wt_cache="${XDG_CACHE_HOME:-$HOME/.cache}/winetricks"
            _name=$(basename "$PCLFE_DOTNET_INSTALLER")
            # winetricks 认的是小写文件名，官方下载页给的是 NDP48-x86-x64-AllOS-ENU.exe
            case "$_name" in
                [Nn][Dd][Pp]48*.exe|[Nn][Dd][Pp]48*.EXE) _alias='ndp48-x86-x64-allos-enu.exe' ;;
                *) _alias='' ;;
            esac
            mkdir -p "$_wt_cache/dotnet48" 2>/dev/null || true
            for _dir in "$_wt_cache/dotnet48" "$_wt_cache"; do
                cp -f "$PCLFE_DOTNET_INSTALLER" "$_dir/$_name" 2>/dev/null || true
                [ -n "$_alias" ] && cp -f "$PCLFE_DOTNET_INSTALLER" "$_dir/$_alias" 2>/dev/null || true
            done
            dim "已放入 winetricks 缓存：$_wt_cache/dotnet48/${_alias:-$_name}"
        else
            warn "PCLFE_DOTNET_INSTALLER 指向的文件不存在：$PCLFE_DOTNET_INSTALLER"
        fi
    fi

    _wt=$(winetricks_bin) || die "未找到 winetricks"
    step "安装 Wine 组件（首次约 10~30 分钟）"
    dim "组件：$PCLFE_WINETRICKS_VERBS"
    dim "详细输出会同时写入：$PCLFE_LOG_DIR/setup-*.log"

    _setup_log="$PCLFE_LOG_DIR/setup-$(timestamp).log"
    _status="$PCLFE_CACHE_DIR/.setup-status"
    _dotnet_ok=0

    # 逐项安装：只有 dotnet48 失败才致命，其余（字体、运行库）失败只警告，
    # 免得一个 404 的字体源把整条链带崩。
    for _verb in $PCLFE_WINETRICKS_VERBS; do
        log ""
        step "组件 $_verb"
        {
            # shellcheck disable=SC2086
            WINEPREFIX="$PCLFE_PREFIX_DIR" WINEARCH=win64 \
            WINEDLLOVERRIDES="mscoree,mshtml=" W_OPT_UNATTENDED=1 \
                "$_wt" -q "$_verb" 2>&1
            printf '%s' "$?" > "$_status"
        } | tee -a "$_setup_log"

        _rc=$(cat "$_status" 2>/dev/null || echo 1)
        if [ "$_rc" != "0" ]; then
            case "$_verb" in
                dotnet48)
                    err "dotnet48 安装失败"
                    dim "日志：$_setup_log"
                    ;;
                *)
                    warn "$_verb 安装失败（非致命，继续）"
                    ;;
            esac
        fi
        [ "$_verb" = "dotnet48" ] && [ "$_rc" = "0" ] && _dotnet_ok=1
    done

    if dotnet48_installed; then
        date '+%Y-%m-%d %H:%M:%S' > "$PCLFE_COMPONENTS_STAMP"
        ok ".NET Framework 4.8 安装完成"
        return 0
    fi

    # winetricks 没搞定，但有离线包时再试一次直装
    if [ -n "${PCLFE_DOTNET_INSTALLER:-}" ] && [ -f "$PCLFE_DOTNET_INSTALLER" ]; then
        warn "winetricks 未能装上 .NET 4.8，尝试直装模式"
        if prefix_install_dotnet_direct "$PCLFE_DOTNET_INSTALLER"; then
            return 0
        fi
    fi

    err "未检测到 .NET Framework 4.8，启动器无法运行"
    wine32_hint || true
    dim "日志：$_setup_log"
    dim "常见原因：Wine 版本过旧、缺 32 位支持、或无法访问微软下载服务器"
    return 1
}

# 注册表微调：DPI 缩放、崩溃对话框、浏览器委派（正版登录的设备码流程会打开系统浏览器）。
prefix_tweaks() {
    prefix_exists || return 0
    _wine=$(wine_bin) || return 1

    _reg() {
        (
            export WINEPREFIX="$PCLFE_PREFIX_DIR" WINEDEBUG=-all
            "$_wine" reg add "$@" >/dev/null 2>&1
        ) || true
    }

    _reg 'HKCU\Control Panel\Desktop' /v LogPixels /t REG_DWORD /d "$PCLFE_DPI" /f
    _reg 'HKCU\Control Panel\Desktop' /v Win8DpiScaling /t REG_DWORD /d 1 /f
    _reg 'HKCU\Software\Wine\WineBrowser' /v Browsers /t REG_SZ /d 'xdg-open' /f
    _reg 'HKCU\Software\Wine\WineDbg' /v ShowCrashDialog /t REG_DWORD /d 0 /f
}

prefix_reset() {
    if [ -d "$PCLFE_PREFIX_DIR" ]; then
        _bak="$PCLFE_PREFIX_DIR.bak-$(timestamp)"
        mv "$PCLFE_PREFIX_DIR" "$_bak"
        ok "已把旧前缀移动到：$_bak"
    fi
    rm -f "$PCLFE_COMPONENTS_STAMP"
    prefix_create
}

prefix_status() {
    if prefix_exists; then
        log "前缀目录：$PCLFE_PREFIX_DIR"
        if dotnet48_installed; then
            log "  .NET Framework 4.8：已安装"
        else
            log "  .NET Framework 4.8：未检测到"
        fi
        if webview2_prefix_check; then
            log "  WebView2 运行时：已安装"
        else
            log "  WebView2 运行时：未安装（仅影响内置浏览器登录，见 pclfe doctor）"
        fi
    else
        log "前缀尚未创建，运行 'pclfe setup' 初始化"
    fi
}
