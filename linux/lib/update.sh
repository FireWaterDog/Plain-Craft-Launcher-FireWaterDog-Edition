#!/usr/bin/env sh
# 启动器本体的获取与更新：从发布页下载、从本地文件/目录安装、校验 SHA256。

# 本地已有的版本清单（安装时复制到 $PCLFE_HOME/version.json）。
app_manifest() {
    for _m in "$PCLFE_HOME/version.json" "$PCLFE_APP_DIR/version.json"; do
        [ -r "$_m" ] && { printf '%s\n' "$_m"; return 0; }
    done
    return 1
}

app_current_version() {
    _m=$(app_manifest 2>/dev/null || true)
    if [ -n "$_m" ]; then
        _v=$(json_str "$_m" versionName 2>/dev/null || true)
        [ -n "$_v" ] && { printf '%s\n' "$_v"; return 0; }
    fi
    printf '未知\n'
}

# 拉取远端清单到缓存，成功时输出文件路径（只输出路径，进度信息走 stderr）。
update_fetch_manifest() {
    deps_require_net
    _dest="$PCLFE_CACHE_DIR/version.json"
    download "$PCLFE_UPDATE_MANIFEST" "$_dest" 60 >&2 || return 1
    printf '%s\n' "$_dest"
}

# 备份现有程序文件（只备份程序本体，不含 .minecraft 与存档）。
app_backup() {
    [ -d "$PCLFE_APP_DIR" ] || return 0
    _has_exe=''
    for _f in "$PCLFE_APP_DIR"/*.exe; do
        [ -f "$_f" ] && _has_exe=1 && break
    done
    [ -n "$_has_exe" ] || return 0
    _bak="$PCLFE_HOME/backup-$(timestamp)"
    mkdir -p "$_bak"
    # 只复制顶层文件：exe、dll、config、pdb；跳过 .minecraft、PCL 等目录。
    for _f in "$PCLFE_APP_DIR"/*; do
        [ -f "$_f" ] || continue
        cp -a "$_f" "$_bak/" 2>/dev/null || true
    done
    info "已备份旧程序到：$_bak"
    # 只保留最近 3 份
    ls -dt "$PCLFE_HOME"/backup-* 2>/dev/null | tail -n +4 | while IFS= read -r _old; do
        rm -rf "$_old"
    done
}

# 校验并安装一个 exe 到应用目录。
app_install_exe() {
    _src=$1
    _expected=${2:-}
    [ -f "$_src" ] || die "文件不存在：$_src"

    if [ -n "$_expected" ]; then
        _actual=$(sha256_of "$_src" || true)
        if [ -z "$_actual" ]; then
            warn "缺少 sha256 工具，跳过校验"
        elif [ "$_actual" != "$_expected" ]; then
            die "SHA256 不匹配，拒绝安装（期望 $_expected，实际 $_actual）"
        else
            ok "SHA256 校验通过"
        fi
    fi

    mkdir -p "$PCLFE_APP_DIR"
    app_backup
    cp -f "$_src" "$PCLFE_APP_DIR/$PCLFE_EXE_NAME" || die "写入应用目录失败"
    chmod +x "$PCLFE_APP_DIR/$PCLFE_EXE_NAME" 2>/dev/null || true
    ok "已安装：$PCLFE_APP_DIR/$PCLFE_EXE_NAME"
}

# 从 zip / 目录 / exe 安装。
app_install_from() {
    _src=$1
    [ -e "$_src" ] || die "路径不存在：$_src"

    if [ -d "$_src" ]; then
        step "从目录安装：$_src"
        app_backup
        mkdir -p "$PCLFE_APP_DIR"
        cp -a "$_src/." "$PCLFE_APP_DIR/" || die "复制失败"
        ok "已安装到 $PCLFE_APP_DIR"
        return 0
    fi

    case "$_src" in
        *.zip)
            step "从压缩包安装：$_src"
            _tmp="$PCLFE_CACHE_DIR/unpack-$(timestamp)"
            rm -rf "$_tmp"; mkdir -p "$_tmp"
            unzip -q "$_src" -d "$_tmp" || die "解压失败：$_src"
            _exe=$(find "$_tmp" -maxdepth 3 -type f -iname '*.exe' | head -n 1)
            [ -n "$_exe" ] || die "压缩包内没有 exe"
            _root=$(dirname "$_exe")
            app_backup
            mkdir -p "$PCLFE_APP_DIR"
            cp -a "$_root/." "$PCLFE_APP_DIR/" || die "复制失败"
            ok "已安装到 $PCLFE_APP_DIR"
            rm -rf "$_tmp"
            ;;
        *.exe)
            app_install_exe "$_src"
            ;;
        *)
            die "无法识别的文件类型：$_src（支持 .exe、.zip 或目录）"
            ;;
    esac
}

update_apply() {
    step "获取版本信息"
    dim "$PCLFE_UPDATE_MANIFEST"
    if ! _manifest=$(update_fetch_manifest); then
        die "无法获取版本信息，请检查网络或 PCLFE_UPDATE_MANIFEST"
    fi
    _ver=$(json_str "$_manifest" versionName || true)
    _url=$(json_str "$_manifest" downloadUrl || true)
    _sum=$(json_str "$_manifest" sha256 || true)
    _note=$(json_str "$_manifest" changelog || true)

    [ -n "$_url" ] || die "版本信息里没有 downloadUrl"
    log ""
    log "远端版本：${_ver:-未知}"
    [ -n "$_note" ] && log "更新说明：$_note"
    log "当前版本：$(app_current_version)"
    log ""

    _file="$PCLFE_CACHE_DIR/$(basename "$_url")"
    step "下载启动器"
    dim "$_url"
    download "$_url" "$_file" 1800 || die "下载失败"

    app_install_exe "$_file" "$_sum"
    cp -f "$_manifest" "$PCLFE_HOME/version.json" 2>/dev/null || true
    ok "更新完成，当前版本：${_ver:-未知}"
    dim "运行 'pclfe run' 启动"
}
