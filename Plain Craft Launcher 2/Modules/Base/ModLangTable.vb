''' <summary>
''' 多语言翻译词典。
''' 
''' 每一行是一条文案：{简体中文原文, 繁體中文, English, 日本語, 한국어}。
''' 空字符串表示该语言暂未翻译，会回退显示中文原文。
''' 界面翻译按原文精确匹配，未收录的文本保持中文，因此可以随时往下面追加行，不会影响任何功能。
''' </summary>
Public Module ModLangTable

    ''' <summary>
    ''' 获取指定语言的翻译词典；简体中文为原文，返回 Nothing。
    ''' </summary>
    Friend Function LangGetTranslationTable(Type As ModLang.LanguageType) As Dictionary(Of String, String)
        Select Case Type
            Case ModLang.LanguageType.TraditionalChinese
                Return BuildTable(1)
            Case ModLang.LanguageType.English
                Return BuildTable(2)
            Case ModLang.LanguageType.Japanese
                Return BuildTable(3)
            Case ModLang.LanguageType.Korean
                Return BuildTable(4)
            Case Else
                Return Nothing
        End Select
    End Function

    Private Function BuildTable(Column As Integer) As Dictionary(Of String, String)
        Dim Result As New Dictionary(Of String, String)(LangEntries.GetLength(0))
        For i As Integer = 0 To LangEntries.GetLength(0) - 1
            Dim Value As String = LangEntries(i, Column)
            If Not String.IsNullOrEmpty(Value) AndAlso Not Result.ContainsKey(LangEntries(i, 0)) Then
                Result(LangEntries(i, 0)) = Value
            End If
        Next
        Return Result
    End Function

    '列顺序：0=简体中文原文，1=繁體中文，2=English，3=日本語，4=한국어
    Private ReadOnly LangEntries As String(,) = {
        {"启动", "啟動", "Launch", "起動", "시작"},
        {"下载", "下載", "Download", "ダウンロード", "다운로드"},
        {"联机", "連線", "Multiplayer", "マルチプレイ", "멀티플레이"},
        {"设置", "設定", "Settings", "設定", "설정"},
        {"更多", "更多", "More", "その他", "더 보기"},
        {"帮助", "說明", "Help", "ヘルプ", "도움말"},
        {"版本选择", "版本選擇", "Select Version", "バージョン選択", "버전 선택"},
        {"版本设置", "版本設定", "Version Settings", "バージョン設定", "버전 설정"},
        {"任务管理", "任務管理", "Tasks", "タスク管理", "작업 관리"},
        {"主页", "首頁", "Home", "ホーム", "홈"},
        {"主页面", "主頁面", "Home page", "ホーム画面", "홈 화면"},
        {"确定", "確定", "OK", "OK", "확인"},
        {"取消", "取消", "Cancel", "キャンセル", "취소"},
        {"关闭", "關閉", "Close", "閉じる", "닫기"},
        {"刷新", "重新整理", "Refresh", "更新", "새로 고침"},
        {"重试", "重試", "Retry", "再試行", "다시 시도"},
        {"删除", "刪除", "Delete", "削除", "삭제"},
        {"复制", "複製", "Copy", "コピー", "복사"},
        {"保存", "儲存", "Save", "保存", "저장"},
        {"导入", "匯入", "Import", "インポート", "가져오기"},
        {"导出", "匯出", "Export", "エクスポート", "내보내기"},
        {"打开文件夹", "開啟資料夾", "Open folder", "フォルダーを開く", "폴더 열기"},
        {"查看来源网站", "檢視來源網站", "View source", "配布元を見る", "출처 사이트 보기"},
        {"查看许可文档", "檢視授權文件", "View license", "ライセンスを見る", "라이선스 보기"},
        {"高级选项", "進階選項", "Advanced options", "詳細オプション", "고급 옵션"},
        {"全部", "全部", "All", "すべて", "전체"},
        {"默认", "預設", "Default", "既定", "기본값"},
        {"自动", "自動", "Auto", "自動", "자동"},
        {"无", "無", "None", "なし", "없음"},
        {"开启", "開啟", "On", "オン", "켜기"},
        {"禁用", "停用", "Disable", "無効", "사용 안 함"},
        {"启用", "啟用", "Enable", "有効", "사용"},
        {"模式", "模式", "Mode", "モード", "모드"},
        {"来源", "來源", "Source", "ソース", "출처"},
        {"标题", "標題", "Title", "タイトル", "제목"},
        {"文本", "文字", "Text", "テキスト", "텍스트"},
        {"图片", "圖片", "Image", "画像", "이미지"},
        {"内容", "內容", "Content", "内容", "내용"},
        {"名称", "名稱", "Name", "名前", "이름"},
        {"版本", "版本", "Version", "バージョン", "버전"},
        {"稍后", "稍後", "Later", "後で", "나중에"},
        {"是", "是", "Yes", "はい", "예"},
        {"否", "否", "No", "いいえ", "아니요"},
        {"提示", "提示", "Notice", "お知らせ", "알림"},
        {"警告", "警告", "Warning", "警告", "경고"},
        {"错误", "錯誤", "Error", "エラー", "오류"},
        {"加载中", "載入中", "Loading", "読み込み中", "불러오는 중"},
        {"正在加载", "正在載入", "Loading", "読み込み中", "불러오는 중"},
        {"正在加载版本列表，请稍候", "正在載入版本清單，請稍候", "Loading version list, please wait", "バージョン一覧を読み込み中です。お待ちください", "버전 목록을 불러오는 중입니다. 잠시만 기다려 주세요"},
        {"正在获取版本列表", "正在取得版本清單", "Fetching version list", "バージョン一覧を取得中", "버전 목록을 가져오는 중"},
        {"正在启动游戏", "正在啟動遊戲", "Launching game", "ゲームを起動中", "게임 실행 중"},
        {"正在下载", "正在下載", "Downloading", "ダウンロード中", "다운로드 중"},
        {"下载完成", "下載完成", "Download complete", "ダウンロード完了", "다운로드 완료"},
        {"下载失败", "下載失敗", "Download failed", "ダウンロード失敗", "다운로드 실패"},
        {"下载速度", "下載速度", "Download speed", "ダウンロード速度", "다운로드 속도"},
        {"下载地址", "下載位址", "Download URL", "ダウンロード URL", "다운로드 주소"},
        {"开始下载", "開始下載", "Start download", "ダウンロード開始", "다운로드 시작"},
        {"原版游戏", "原版遊戲", "Vanilla", "バニラ", "바닐라"},
        {"整合包", "整合包", "Modpacks", "MOD パック", "모드팩"},
        {"资源包", "資源包", "Resource packs", "リソースパック", "리소스 팩"},
        {"数据包", "資料包", "Data packs", "データパック", "데이터 팩"},
        {"光影", "光影", "Shaders", "シェーダー", "셰이더"},
        {"世界", "世界", "Worlds", "ワールド", "월드"},
        {"社区资源", "社群資源", "Community", "コミュニティ", "커뮤니티"},
        {"文件下载源", "檔案下載來源", "File download source", "ファイルのダウンロード元", "파일 다운로드 소스"},
        {"版本列表源", "版本清單來源", "Version list source", "バージョン一覧の取得元", "버전 목록 소스"},
        {"尽量使用镜像源", "盡量使用鏡像來源", "Prefer mirrors", "ミラーを優先", "미러 우선"},
        {"尽量使用官方源", "盡量使用官方來源", "Prefer official sources", "公式を優先", "공식 소스 우선"},
        {"优先使用官方源，在加载缓慢时换用镜像源", "優先使用官方來源，載入緩慢時改用鏡像來源", "Prefer official sources, switch to mirrors when slow", "公式を優先し、遅い場合はミラーに切り替え", "공식 소스 우선, 느릴 때 미러로 전환"},
        {"尽量使用镜像源（可能缺少刚刚更新的版本）", "盡量使用鏡像來源（可能缺少剛更新的版本）", "Prefer mirrors (may miss the newest versions)", "ミラーを優先（最新版が欠ける場合があります）", "미러 우선 (최신 버전이 없을 수 있음)"},
        {"最大线程数", "最大執行緒數", "Max threads", "最大スレッド数", "최대 스레드 수"},
        {"速度限制", "速度限制", "Speed limit", "速度制限", "속도 제한"},
        {"目标文件夹", "目標資料夾", "Target folder", "保存先フォルダー", "대상 폴더"},
        {"界面", "介面", "Interface", "インターフェース", "인터페이스"},
        {"个性化", "個人化", "Personalization", "カスタマイズ", "개인 설정"},
        {"功能隐藏", "功能隱藏", "Hidden features", "機能の非表示", "기능 숨기기"},
        {"其他", "其他", "Other", "その他", "기타"},
        {"关于与鸣谢", "關於與鳴謝", "About & Credits", "このソフトについて", "정보 및 크레딧"},
        {"启动器", "啟動器", "Launcher", "ランチャー", "런처"},
        {"启动选项", "啟動選項", "Launch options", "起動オプション", "실행 옵션"},
        {"启动前执行命令", "啟動前執行命令", "Run command before launch", "起動前にコマンドを実行", "실행 전 명령 실행"},
        {"启动游戏前进行内存优化", "啟動遊戲前進行記憶體最佳化", "Optimize memory before launching", "起動前にメモリを最適化", "실행 전 메모리 최적화"},
        {"内存分配", "記憶體分配", "Memory allocation", "メモリ割り当て", "메모리 할당"},
        {"内存管理", "記憶體管理", "Memory management", "メモリ管理", "메모리 관리"},
        {"已使用内存", "已使用記憶體", "Memory used", "使用メモリ", "사용 중인 메모리"},
        {"Java 列表", "Java 清單", "Java list", "Java 一覧", "Java 목록"},
        {"Java 虚拟机参数", "Java 虛擬機參數", "JVM arguments", "JVM 引数", "JVM 인수"},
        {"游戏窗口标题", "遊戲視窗標題", "Game window title", "ゲームウィンドウのタイトル", "게임 창 제목"},
        {"游戏参数", "遊戲參數", "Game arguments", "ゲーム引数", "게임 인수"},
        {"性能优化", "效能最佳化", "Performance", "パフォーマンス", "성능 최적화"},
        {"检查更新", "檢查更新", "Check for updates", "アップデートを確認", "업데이트 확인"},
        {"启动器更新", "啟動器更新", "Launcher updates", "ランチャーの更新", "런처 업데이트"},
        {"正式版更新提示", "正式版更新提示", "Stable release notifications", "正式版の更新通知", "정식 버전 업데이트 알림"},
        {"匿名数据收集", "匿名資料收集", "Anonymous data collection", "匿名データの収集", "익명 데이터 수집"},
        {"在正版登录时验证 SSL 证书", "正版登入時驗證 SSL 憑證", "Verify SSL certificate on Microsoft login", "Microsoft ログイン時に SSL 証明書を検証", "정품 로그인 시 SSL 인증서 확인"},
        {"使用高性能显卡", "使用高效能顯示卡", "Use high-performance GPU", "高性能 GPU を使用", "고성능 GPU 사용"},
        {"不透明度", "不透明度", "Opacity", "不透明度", "불투명도"},
        {"动画速度", "動畫速度", "Animation speed", "アニメーション速度", "애니메이션 속도"},
        {"标题栏", "標題列", "Title bar", "タイトルバー", "제목 표시줄"},
        {"标题栏居左", "標題列靠左", "Title bar aligned left", "タイトルバーを左寄せ", "제목 표시줄 왼쪽 정렬"},
        {"标题栏文本", "標題列文字", "Title bar text", "タイトルバーのテキスト", "제목 표시줄 텍스트"},
        {"叠加彩色背景", "疊加彩色背景", "Colored background overlay", "カラー背景を重ねる", "컬러 배경 오버레이"},
        {"全局设置", "全域設定", "Global settings", "全体設定", "전역 설정"},
        {"导入设置", "匯入設定", "Import settings", "設定をインポート", "설정 가져오기"},
        {"导出设置", "匯出設定", "Export settings", "設定をエクスポート", "설정 내보내기"},
        {"打开插件目录", "開啟外掛目錄", "Open plugin folder", "プラグインフォルダーを開く", "플러그인 폴더 열기"},
        {"插件目录", "外掛目錄", "Plugin folder", "プラグインフォルダー", "플러그인 폴더"},
        {"插件列表", "外掛清單", "Plugins", "プラグイン一覧", "플러그인 목록"},
        {"重新加载", "重新載入", "Reload", "再読み込み", "다시 불러오기"},
        {"Mod 管理样式", "Mod 清單樣式", "Mod list style", "MOD 一覧の表示形式", "모드 목록 스타일"},
        {"文件名格式", "檔案名稱格式", "File name format", "ファイル名の形式", "파일 이름 형식"},
        {"打开主页文件夹", "開啟首頁資料夾", "Open home folder", "ホームフォルダーを開く", "홈 폴더 열기"},
        {"打开启动器自动开始播放", "開啟啟動器時自動播放", "Play music when the launcher starts", "起動時に自動再生", "런처 시작 시 자동 재생"},
        {"更改图片", "更換圖片", "Change image", "画像を変更", "이미지 변경"},
        {"更改皮肤", "更換面板", "Change theme image", "スキンを変更", "스킨 변경"},
        {"保存皮肤", "儲存面板", "Save theme image", "スキンを保存", "스킨 저장"},
        {"查看教程", "檢視教學", "View tutorial", "チュートリアルを見る", "튜토리얼 보기"},
        {"登录方式", "登入方式", "Sign-in method", "ログイン方法", "로그인 방식"},
        {"正版登录", "正版登入", "Microsoft sign-in", "正規ログイン", "정품 로그인"},
        {"正版", "正版", "Premium", "正規版", "정품"},
        {"离线", "離線", "Offline", "オフライン", "오프라인"},
        {"正版玩家名", "正版玩家名稱", "Premium player name", "プレイヤー名", "정품 플레이어 이름"},
        {"复制识别码", "複製識別碼", "Copy install ID", "識別コードをコピー", "식별 코드 복사"},
        {"启动器公告", "啟動器公告", "Launcher announcement", "ランチャーのお知らせ", "런처 공지"},
        {"刷新主页", "重新整理首頁", "Refresh home", "ホームを更新", "홈 새로 고침"},
        {"刷新背景图片", "重新整理背景圖片", "Refresh background image", "背景画像を更新", "배경 이미지 새로 고침"},
        {"刷新背景音乐", "重新整理背景音樂", "Refresh background music", "BGM を更新", "배경 음악 새로 고침"},
        {"主页预设", "首頁預設", "Home presets", "ホームのプリセット", "홈 프리셋"},
        {"启动进度", "啟動進度", "Launch progress", "起動の進行状況", "실행 진행률"},
        {"当前步骤", "目前步驟", "Current step", "現在の手順", "현재 단계"},
        {"你知道吗", "你知道嗎", "Did you know", "豆知識", "알고 계셨나요"},
        {"启动游戏", "啟動遊戲", "Launch game", "ゲームを起動", "게임 실행"},
        {"更换皮肤", "更換面板", "Change skin", "スキンを変更", "스킨 변경"},
        {"记住密码", "記住密碼", "Remember password", "パスワードを保存", "비밀번호 기억"},
        {"密码", "密碼", "Password", "パスワード", "비밀번호"},
        {"注册账号", "註冊帳號", "Create account", "アカウント登録", "계정 만들기"},
        {"邮箱", "電子郵件", "Email", "メールアドレス", "이메일"},
        {"界面语言", "介面語言", "Interface language", "表示言語", "인터페이스 언어"},
        {"跟随系统", "跟隨系統", "Follow system", "システムに従う", "시스템 설정 따르기"},
        {"简体中文", "簡體中文", "Simplified Chinese", "簡体字中国語", "중국어 간체"},
        {"繁體中文", "繁體中文", "Traditional Chinese", "繁体字中国語", "중국어 번체"},
        {"日本語", "日本語", "Japanese", "日本語", "일본어"},
        {"한국어", "한국어", "Korean", "韓国語", "한국어"},
        {"已是最新版本", "已是最新版本", "Already up to date", "最新バージョンです", "최신 버전입니다"},
        {"检查更新失败", "檢查更新失敗", "Failed to check for updates", "アップデートの確認に失敗しました", "업데이트 확인 실패"},
        {"更新失败", "更新失敗", "Update failed", "アップデートに失敗しました", "업데이트 실패"},
        {"更新完成", "更新完成", "Update complete", "アップデート完了", "업데이트 완료"},
        {"正在下载更新", "正在下載更新", "Downloading update", "アップデートをダウンロード中", "업데이트 다운로드 중"},
        {"立即重启", "立即重新啟動", "Restart now", "今すぐ再起動", "지금 재시작"},
        {"下次启动时自动更新", "下次啟動時自動更新", "Update on next launch", "次回起動時に更新", "다음 실행 시 업데이트"},
        {"立即更新", "立即更新", "Update now", "今すぐ更新", "지금 업데이트"},
        {"内存优化", "記憶體最佳化", "Memory optimization", "メモリ最適化", "메모리 최적화"}
    }

End Module
