#!/usr/bin/env sh
# Windows 版 Java 运行时（JRE）的安装与桥接。
#
# 为什么不用系统自带的 Linux Java：
#   PCLFE 本体运行在 Wine 里，它通过 CreateProcess 调用 java.exe，
#   Wine 无法可靠地执行 Linux ELF 程序，因此必须提供 Windows 版 java.exe。
#   安装后通过 Z: 盘路径写入 JAVA_HOME，PCL 的 Java 扫描会把它收进列表。

PCLFE_ADOPTIUM_API="${PCLFE_ADOPTIUM_API:-https://api.adoptium.net/v3/assets/latest}"
PCLFE_ADOPTIUM_MIRROR="${PCLFE_ADOPTIUM_MIRROR:-https://mirrors.tuna.tsinghua.edu.cn/Adoptium}"

java_dir_for() { printf '%s/temurin-%s\n' "$PCLFE_JRE_DIR" "$1"; }

java_exe_of() { printf '%s/bin/java.exe\n' "$1"; }

java_installed_list() {
    [ -d "$PCLFE_JRE_DIR" ] || return 0
    for _d in "$PCLFE_JRE_DIR"/*; do
        [ -d "$_d" ] || continue
        [ -f "$(java_exe_of "$_d")" ] || continue
        printf '%s\n' "$_d"
    done
}

# 打印第一个可用 JRE 的 Windows 路径（供 JAVA_HOME 使用）。
java_export_home() {
    _first=$(java_installed_list | head -n 1)
    [ -n "$_first" ] || return 1
    win_path "$_first"
}

# 解析 Adoptium API 或镜像站，输出 "下载地址<TAB>sha256"。
java_resolve_url() {
    _major=$1
    _use_mirror=${2:-auto}
    _api_json="$PCLFE_CACHE_DIR/adoptium-$_major.json"

    if [ "$_use_mirror" != "tuna" ]; then
        _api="$PCLFE_ADOPTIUM_API/$_major/hotspot?architecture=x64&image_type=jre&os=windows&vendor=eclipse"
        if download "$_api" "$_api_json" 60; then
            _resolved=$(python3 - "$_api_json" <<'PY' 2>/dev/null || true
import json, sys
try:
    data = json.load(open(sys.argv[1], encoding="utf-8"))
except Exception:
    sys.exit(1)
for item in data:
    pkg = (item.get("binary") or {}).get("package") or {}
    link, checksum = pkg.get("link"), pkg.get("checksum") or ""
    if link and link.endswith(".zip"):
        print(f"{link}\t{checksum}")
        break
PY
)
            if [ -z "$_resolved" ] && ! have python3; then
                _link=$(sed -n 's/.*"link"[[:space:]]*:[[:space:]]*"\([^"]*\.zip\)".*/\1/p' "$_api_json" | head -n 1)
                _sum=$(sed -n 's/.*"checksum"[[:space:]]*:[[:space:]]*"\([0-9a-fA-F]\{64\}\)".*/\1/p' "$_api_json" | head -n 1)
                if [ -n "$_link" ]; then
                    _resolved=$(printf '%s\t%s\n' "$_link" "$_sum")
                fi
            fi
            if [ -n "$_resolved" ]; then
                printf '%s\n' "$_resolved"
                return 0
            fi
        fi
        [ "$_use_mirror" = "adoptium" ] && return 1
    fi

    # 镜像回退：解析目录列表，取版本号最大的 zip。
    _mirror_url="$PCLFE_ADOPTIUM_MIRROR/$_major/jre/x64/windows/"
    _listing="$PCLFE_CACHE_DIR/adoptium-mirror-$_major.html"
    download "$_mirror_url" "$_listing" 60 || return 1
    _zip=$(grep -o 'OpenJDK[0-9][^"<>]*\.zip' "$_listing" | sort -u | sort -V | tail -n 1)
    [ -n "$_zip" ] || return 1
    printf '%s%s\t\n' "$_mirror_url" "$_zip"
}

java_install() {
    _major=${1:-21}
    _source=${2:-auto}
    case "$_major" in
        ''|*[!0-9]*) die "Java 主版本号必须是数字，例如 8、17、21" ;;
    esac
    _target=$(java_dir_for "$_major")
    if [ -f "$(java_exe_of "$_target")" ]; then
        ok "Java $_major 已安装：$_target"
        return 0
    fi
    deps_require_net

    step "查找 Java $_major（Windows x64 JRE）"
    _resolved=$(java_resolve_url "$_major" "$_source" || true)
    [ -n "$_resolved" ] || die "无法获取 Java $_major 的下载地址；可稍后重试，或设置 PCLFE_ADOPTIUM_MIRROR 指向可用镜像"

    _url=$(printf '%s' "$_resolved" | cut -f1)
    _sum=$(printf '%s' "$_resolved" | cut -f2)
    dim "下载地址：$_url"

    _zip="$PCLFE_CACHE_DIR/$(basename "$_url")"
    step "下载 Java $_major"
    if ! download "$_url" "$_zip" 900; then
        warn "直连下载失败，改用镜像重试"
        _mirror_resolved=$(java_resolve_url "$_major" tuna || true)
        if [ -z "$_mirror_resolved" ]; then
            die "下载失败，且镜像也无法解析：$_url"
        fi
        _url=$(printf '%s' "$_mirror_resolved" | cut -f1)
        _sum=''   # 镜像目录不提供校验值
        _zip="$PCLFE_CACHE_DIR/$(basename "$_url")"
        dim "镜像地址：$_url"
        download "$_url" "$_zip" 900 || die "镜像下载也失败：$_url"
    fi

    if [ -n "$_sum" ]; then
        _actual=$(sha256_of "$_zip" || true)
        if [ -z "$_actual" ]; then
            warn "没有可用的 sha256 工具，跳过校验"
        elif [ "$_actual" != "$_sum" ]; then
            rm -f "$_zip"
            die "校验失败（期望 $_sum，实际 $_actual），已删除下载文件"
        else
            ok "校验通过"
        fi
    else
        warn "镜像未提供校验值，跳过 sha256 校验"
    fi

    step "解压 Java $_major"
    _tmp="$PCLFE_CACHE_DIR/java-$_major.tmp"
    rm -rf "$_tmp"
    mkdir -p "$_tmp"
    unzip -q "$_zip" -d "$_tmp" || die "解压失败：$_zip"
    _inner=$(find "$_tmp" -maxdepth 4 -type f -name java.exe | head -n 1)
    [ -n "$_inner" ] || die "压缩包内未找到 bin/java.exe"
    _inner_dir=$(dirname "$(dirname "$_inner")")

    rm -rf "$_target"
    mkdir -p "$PCLFE_JRE_DIR"
    mv "$_inner_dir" "$_target" || die "安装到 $_target 失败"
    rm -rf "$_tmp"

    [ -f "$(java_exe_of "$_target")" ] || die "安装后未找到 java.exe"
    ok "Java $_major 已安装：$_target"
    dim "启动时会把 JAVA_HOME 设为：$(win_path "$_target")"
}

java_remove() {
    _major=$1
    _target=$(java_dir_for "$_major")
    [ -d "$_target" ] || die "未安装 Java $_major"
    rm -rf "$_target"
    ok "已删除 $_target"
}

java_status() {
    _list=$(java_installed_list)
    if [ -z "$_list" ]; then
        log "尚未安装 Java。可执行：pclfe java install 21"
        log "PCL 也会自行下载 Java 到 Wine 前缀内的 .minecraft\\runtime 目录。"
        return 0
    fi
    log "已安装的 Windows 版 Java："
    printf '%s\n' "$_list" | while IFS= read -r _d; do
        printf '  - %s\n' "$_d"
    done
    log ""
    log "JAVA_HOME（供 PCL 扫描）：$(java_export_home || echo '无')"
}
