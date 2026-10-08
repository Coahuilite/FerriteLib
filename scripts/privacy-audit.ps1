# Privacy gate adapted from the series' Mwah implementation (2026-10-08).
# Independent checks: tracked text/names, messages/tags and identities,
# reachable historical text (-FullHistory), binary metadata, runtime anonymity.
# Full history runs automatically in pre-push and CI. -PrePush adds optional
# release-oriented mechanical checks; ordinary version-branch pushes use the hook.
# Repository-specific accepted history debt starts empty. Never copy another
# repository's exemptions or treat scanner findings as authorization to rewrite.
[CmdletBinding()]
param(
    [switch]$FullHistory,
    [switch]$PrePush
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    $newLine = [Environment]::NewLine
    $failures = New-Object 'System.Collections.Generic.List[string]'
    $knownHits = New-Object 'System.Collections.Generic.List[string]'

    function Add-Failure([string]$What, [string]$Detail) {
        $script:failures.Add(($What + $newLine + $Detail))
    }

    function Add-KnownHit([string]$Detail) {
        $script:knownHits.Add($Detail)
    }

    # 原生命令的 stderr 在某些宿主下会变成终止性错误；判定只依赖 $LASTEXITCODE，故调用期间降级 EAP。
    function Invoke-Git {
        param([string[]]$Arguments)
        $previous = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $lines = @(& git @Arguments 2>$null)
            $code = $LASTEXITCODE
        }
        finally {
            $ErrorActionPreference = $previous
        }
        return [pscustomobject]@{ ExitCode = $code; Lines = @($lines | ForEach-Object { [string]$_ }) }
    }

    # 逗号包裹：PowerShell 函数返回会解包数组，1 元素/0 元素时调用方 .Count 会在 StrictMode 下炸。
    function Select-Unique([string[]]$Values) {
        return ,@($Values | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)
    }

    # 绝对路径（2026-10-05 校准放宽）：旧形钉死 (Users|WorkSpace)，看不见其它盘符目录
    # （游戏安装目录、Steam 库）、UNC 与 /home/。现在扫"任意盘符 + 分隔符"，守卫字符要求
    # 盘符前不是字母数字/斜杠/冒号/反斜杠，挡掉 URL scheme 尾部字母的误报。两方言各一份：
    # git grep 走 POSIX ERE（无 lookbehind，用负向类；git 自带引擎不认嵌套 [:space:]
    # 写法，实测 128），PowerShell 侧（vector2/vector4）走 .NET 负向断言。{1,2} 保留：
    # 历史 blob 实测存在 JSON 双反斜杠形态（转义在冒号之后，两份都盖得住）。
    $pathPattern = '(^|[^A-Za-z0-9/:\\])[A-Za-z]:[\\/]{1,2}'
    $pathPatternNet = '(?<![A-Za-z0-9/:\\])[A-Za-z]:[\\/]{1,2}'
    # UNC 与 /home/ 用拼接构造：脚本源码里不得出现它们匹配的目标串（校准实锤：字面量形态
    # 的 UNC 模式让 vector1 逮住脚本自己；私钥标记同理，见下）。
    $bs = [string][char]92
    # A negative punctuation class accepts Unicode host/share names even when
    # Git's POSIX alnum class is ASCII-only. Exclude regex delimiters so source
    # patterns are not classified as network paths.
    $uncComponent = '[^][(){}[:space:]' + ($bs * 2) + '/"''|<>?*:;=,+]+'
    $uncPattern = '(' + ($bs * 4) + $uncComponent + '[' + $bs + '/]' + $uncComponent + '|' + ($bs * 4) + '[?.]' + ($bs * 2) + '(UNC' + ($bs * 2) + '|[A-Za-z]:))'
    $posixHomePattern = '(^|[^A-Za-z0-9/:])' + '/ho' + 'me/[A-Za-z0-9._-]+'
    # 私钥标记由拼接构造，本脚本才不会在 vector1 里命中自己。
    $credPatterns = [ordered]@{
        'github-classic-pat'  = 'ghp_[A-Za-z0-9]{36}'
        'github-fine-grained' = 'github_pat_[A-Za-z0-9_]{20,}'
        'openai-style-key'    = 'sk-[A-Za-z0-9]{20,}'
        'aws-access-key'      = 'AKIA[0-9A-Z]{16}'
        'private-key-block'   = ('-----' + 'BEGIN ([A-Z0-9]+ )*PRIVATE KEY-----')
    }
    # 内容里的真实邮箱也是个人面（身份向量只管 author/committer，管不到 About.xml 写死邮箱）。
    # 白名单：GitHub noreply 与 RFC 2606 example 域。
    $emailPattern = '[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}'
    $emailAllowlist = 'users\.noreply\.github\.com|noreply@github\.com|example\.com|example\.net|example\.org'
    $publishedFileIdValuePattern = '<PublishedFileId>[0-9]+'

    # 二进制面（校准盲区 1）：git grep -I 永远跳过二进制，而 PNG 的 XMP/tEXt 与 DLL 的
    # 嵌入字符串正是实测有货的位置（ModIcon.png 内 Affinity Photo XMP 块由 -FullHistory
    # 之外的手工校准首次发现）。vector4 直接按字节扫 tracked 二进制，并报出可读元数据
    # 字段供人工过目；像素内容（截图里的任务栏/ID/存档名）脚本看不见，必须人工目检。
    $binaryExtPattern = '\.(dll|png|jpg|jpeg|gif|tga|bmp|zip|pdb|ttf|otf|mp3|ogg|wav)$'

    # Repository-specific history exceptions require a separate recorded decision.
    $knownHistoryDebt = @()

    function Test-KnownHistoryDebt([string]$PatternLabel, [string]$RepoPath) {
        foreach ($entry in $script:knownHistoryDebt) {
            if ($entry.Pattern -eq $PatternLabel -and $entry.Path -eq $RepoPath) { return $true }
        }
        return $false
    }

    # ---- vector1：工作树（已跟踪文本；二进制归 vector4 的结构化解析） ----
    $vector1Checks = [ordered]@{
        'personal-path'           = $pathPattern
        'unc-path'                = $uncPattern
        'posix-home'              = $posixHomePattern
        'published-file-id-value' = $publishedFileIdValuePattern
    }
    foreach ($name in $credPatterns.Keys) { $vector1Checks["credential '$name'"] = $credPatterns[$name] }

    foreach ($label in $vector1Checks.Keys) {
        # -e 是必须的：私钥标记与 UNC 以 '-'/'\' 开头，会被当成选项（git exit 129）。
        # -I：跳过二进制——压缩图像流里随机出现"守卫+字母+冒号+斜杠"四字节组，-a 必误报
        # （校准实锤：Preview.png 的 IDAT 流命中假路径）。二进制由 vector4 按结构扫。
        $result = Invoke-Git @('grep', '-l', '-I', '-E', '-e', $vector1Checks[$label], '--', '.')
        if ($result.ExitCode -ne 0 -and $result.ExitCode -ne 1) {
            Add-Failure "vector1 scan failed for $label (git exit $($result.ExitCode))" ''
            continue
        }
        $hits = Select-Unique $result.Lines
        if ($hits.Count -gt 0) { Add-Failure "vector1 working-tree $label" ($hits -join $newLine) }
    }
    $trackedIdFile = Select-Unique ((Invoke-Git @('ls-files', '--', 'About/PublishedFileId.txt')).Lines)
    if ($trackedIdFile.Count -gt 0) { Add-Failure 'vector1 PublishedFileId.txt is tracked' ($trackedIdFile -join $newLine) }
    # 文件名本身（校准盲区 4）：内容干净不代表"含个人用户名的文件名"没漏（示例刻意不写真名，见 vector5）。
    $allTracked = (Invoke-Git @('ls-files')).Lines
    $badNames = Select-Unique @($allTracked | Where-Object {
        $_ -match '[A-Za-z]:' -or $_ -match '(?i)(^|/)Users?/' -or $_ -match '(?i)workspace' -or $_ -match '(?i)\\\\'
    })
    if ($badNames.Count -gt 0) { Add-Failure 'vector1 tracked filename leaks a local path' ($badNames -join $newLine) }
    # 内容邮箱（校准盲区 3）：逐命中行过白名单。
    $emailHits = @((Invoke-Git @('grep', '-n', '-I', '-E', '-e', $emailPattern, '--', '.')).Lines |
        Where-Object { $_ -notmatch $emailAllowlist })
    if ($emailHits.Count -gt 0) { Add-Failure 'vector1 email address in tracked text' (($emailHits | ForEach-Object { ($_ -split ':', 3)[0..1] -join ':' }) -join $newLine) }
    Write-Host 'vector1 (working tree): scanned'

    # ---- vector2：提交信息（subject + body + annotated tag 消息，校准盲区 5） ----
    $messageText = ((Invoke-Git @('log', '--all', '--format=%s%n%b')).Lines -join $newLine)
    $tagMessages = ((Invoke-Git @('for-each-ref', 'refs/tags', '--format=%(contents)')).Lines -join $newLine)
    $messageText = $messageText + $newLine + $tagMessages
    $vector2Checks = [ordered]@{
        'personal-path'           = $pathPatternNet
        'published-file-id-value' = $publishedFileIdValuePattern
    }
    foreach ($name in $credPatterns.Keys) { $vector2Checks["credential '$name'"] = $credPatterns[$name] }

    foreach ($label in $vector2Checks.Keys) {
        $matches = @([regex]::Matches($messageText, $vector2Checks[$label]))
        if ($matches.Count -gt 0) {
            $samples = Select-Unique @($matches | ForEach-Object { $_.Value.Substring(0, [Math]::Min(48, $_.Value.Length)) })
            Add-Failure "vector2 commit messages $label (x $($matches.Count))" 'Matched content withheld; inspect the referenced revision locally.'
        }
    }
    Write-Host 'vector2 (commit messages): scanned'

    # ---- 身份面：author 与 committer 分开收集（合并格式串会把两个身份塞进一行，单身份正则看不见） ----
    $authors = Select-Unique ((Invoke-Git @('log', '--all', '--format=%an <%ae>')).Lines)
    $committers = Select-Unique ((Invoke-Git @('log', '--all', '--format=%cn <%ce>')).Lines)
    $identities = Select-Unique ($authors + $committers)
    $noreplyPattern = '^[^<]+ <[0-9]+\+[^@]+@users\.noreply\.github\.com>$'
    $botPattern = '^GitHub <noreply@github\.com>$'
    $badIdentities = @($identities | Where-Object { $_ -notmatch $noreplyPattern -and $_ -notmatch $botPattern })
    if ($identities.Count -eq 0) { Add-Failure 'identity scan found no commits' '' }
    if ($badIdentities.Count -gt 0) {
        Add-Failure "identity: $($badIdentities.Count) non-noreply identity/identities" 'Identity values withheld; inspect git author/committer locally.'
    }
    Write-Host "identity: $($identities.Count) unique identity/identities, all noreply"

    # ---- vector3：历史 blob（-FullHistory） ----
    if ($FullHistory) {
        $revs = @((Invoke-Git @('rev-list', '--all')).Lines | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        Write-Host "vector3 (historical blobs): scanning $($revs.Count) revisions"
        $vector3Checks = [ordered]@{
            'personal-path'           = $pathPattern
            'unc-path'                = $uncPattern
            'posix-home'              = $posixHomePattern
            'published-file-id-value' = $publishedFileIdValuePattern
        }
        foreach ($name in $credPatterns.Keys) { $vector3Checks["credential '$name'"] = $credPatterns[$name] }

        foreach ($label in $vector3Checks.Keys) {
            $result = Invoke-Git (@('grep', '-l', '-I', '-E', '-e', $vector3Checks[$label]) + $revs)
            if ($result.ExitCode -ne 0 -and $result.ExitCode -ne 1) {
                Add-Failure "vector3 scan failed for $label (git exit $($result.ExitCode))" ''
                continue
            }
            $paths = Select-Unique @($result.Lines | ForEach-Object { ($_ -split ':', 2)[1] })
            $unknown = New-Object 'System.Collections.Generic.List[string]'
            foreach ($path in $paths) {
                if (Test-KnownHistoryDebt $label $path) { Add-KnownHit "$label : $path" }
                else { $unknown.Add($path) }
            }
            if ($unknown.Count -gt 0) { Add-Failure "vector3 historical $label" (($unknown | Sort-Object -Unique) -join $newLine) }
        }
        Write-Host "vector3 (historical blobs): scanned; known-debt files accepted: $($knownHits.Count)"
        # 历史二进制：-I 跳过（同上，误报问题）；泄漏一旦入史即归 vector3 台账管，
        # 而当前工作树的二进制由 vector4 结构化复核 —— 发布前两者都跑，覆盖面闭合。
    }
    else {
        Write-Host 'vector3 (historical blobs): SKIPPED (pass -FullHistory for the pre-push check)'
    }

    # ---- vector4：二进制元数据（工作树；git grep -I 的盲区，校准盲区 1） ----
    # 不做"整文件字节扫"：压缩图像流里随机出现"字母:斜杠"四字节组，误报率与文件体积成正比
    # （校准实测：Preview.png 的 IDAT 流命中两条假路径）。改为**结构化解析**：
    # PNG 只读元数据块（tEXt/iTXt/zTXt/eXIf，首个 IDAT 前），DLL/其余只读字符串表
    # （ASCII 与 UTF-16LE 连续可打印段）——泄漏真会藏的位置，噪音进不来。
    function Get-PngMetadataText([byte[]]$bytes) {
        $sb = New-Object Text.StringBuilder
        $names = New-Object 'System.Collections.Generic.List[string]'
        $i = 8
        while ($i + 12 -le $bytes.Length) {
            $len = ([int]$bytes[$i] -shl 24) -bor ([int]$bytes[$i+1] -shl 16) -bor ([int]$bytes[$i+2] -shl 8) -bor [int]$bytes[$i+3]
            if ($len -lt 0 -or $i + 12 + $len -gt $bytes.Length) { break }
            $type = [Text.Encoding]::ASCII.GetString($bytes, $i + 4, 4)
            if ($type -eq 'IDAT') { break }
            if ($type -in @('tEXt', 'iTXt', 'zTXt', 'eXIf')) {
                [void]$names.Add($type)
                [void]$sb.AppendLine($type + ': ' + [Text.Encoding]::GetEncoding('latin1').GetString($bytes, $i + 8, [Math]::Min($len, 4096)))
            }
            $i += 12 + $len
        }
        return [pscustomobject]@{ Text = $sb.ToString(); Names = $names }
    }
    function Get-PrintableStrings([byte[]]$bytes, [int]$min = 6) {
        $out = New-Object 'System.Collections.Generic.List[string]'
        foreach ($encoding in @([Text.Encoding]::ASCII, [Text.Encoding]::Unicode)) {
            $text = $encoding.GetString($bytes, 0, $bytes.Length - ($bytes.Length % 2))
            $sb = New-Object Text.StringBuilder
            foreach ($ch in $text.ToCharArray()) {
                if ([int]$ch -ge 32 -and [int]$ch -le 126) { [void]$sb.Append($ch) }
                else { if ($sb.Length -ge $min) { $out.Add($sb.ToString()) }; [void]$sb.Clear() }
            }
            if ($sb.Length -ge $min) { $out.Add($sb.ToString()) }
        }
        return $out
    }
    $binaries = @($allTracked | Where-Object { $_ -match $binaryExtPattern })
    $binaryChunks = New-Object 'System.Collections.Generic.List[string]'
    foreach ($b in $binaries) {
        # A deletion in the working tree is not readable metadata; history is scanned separately.
        if (-not (Test-Path -LiteralPath (Join-Path $root $b))) { continue }
        $bytes = [IO.File]::ReadAllBytes((Join-Path $root ($b -replace '/', [IO.Path]::DirectorySeparatorChar)))
        $regions = New-Object 'System.Collections.Generic.List[string]'
        if ($b -match '\.png$') {
            $meta = Get-PngMetadataText $bytes
            if ($meta.Text) { $regions.Add($meta.Text) }
            if ($meta.Names.Count -gt 0) {
                $binaryChunks.Add("$b : metadata chunks: $((($meta.Names | Sort-Object -Unique) -join ', '))")
            }
        }
        else {
            foreach ($s in Get-PrintableStrings $bytes) { $regions.Add($s) }
        }
        foreach ($region in $regions) {
            foreach ($p in @($pathPatternNet, $emailPattern, $credPatterns.Values)) {
                foreach ($m in [regex]::Matches($region, $p)) {
                    if ($p -eq $emailPattern -and $m.Value -match $emailAllowlist) { continue }
                    Add-Failure "vector4 binary metadata $b" 'Matched metadata withheld; inspect this file locally.'
                }
            }
        }
    }
    if ($binaries.Count -gt 0) {
        Write-Host "vector4 (binary metadata): scanned $($binaries.Count) tracked binary file(s)"
        foreach ($c in ($binaryChunks | Sort-Object -Unique)) { Write-Host "  [chunk] $c" }
        Write-Host '  REMINDER: pixel content of images (taskbars, Steam IDs, save names, watermarks)'
        Write-Host '            is beyond any scanner - eyeball every tracked image before pushing.'
    }
    else {
        Write-Host 'vector4 (binary metadata): no tracked binaries'
    }

    # ---- vector5：扫描器自匿名（2026-10-05 校准）----
    # 维护隐私的表不得自身成为隐私：扫描器源码、台账、注释里不得存任何真实个人值。
    # 本门从运行时环境派生 token（用户名、机器名）—— 源码只引用 $env 变量、绝不写字面量 ——
    # 再反查全部已跟踪文本：个人 token 以独立词出现即判定"有人把隐私写进了仓库"（可能就是本脚本）。
    # 结构性保证：个人值只活在跑门的这台机器上，从不落进门的文本或仓库。
    # 只在真正的托管 runner 上跳过：GitHub/Azure runner 的用户名是通用构建账号（常是 "admin"、
    # "runner" 这类高频词，反查必误报），且那不是维护者的秘密。本地即便 shell 设了泛 CI=true
    # 也要跑 —— 那台机器的 $env:USERNAME 恰恰就是本门要护住的真值。
    if ($env:GITHUB_ACTIONS -eq 'true' -or $env:TF_BUILD -eq 'true') {
        Write-Host 'vector5 (scanner anonymity): SKIPPED on hosted runner (runner identity is not the maintainer secret)'
    }
    else {
        $anonTokens = @($env:USERNAME, $env:COMPUTERNAME) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique
        $anonLeaks = New-Object 'System.Collections.Generic.List[string]'
        foreach ($tok in $anonTokens) {
            # -F 定值搜（避开 ERE 转义地狱），再在 PowerShell 侧用 .NET 词边界精判，
            # 挡掉 interface/safety 这类长词里的巧合子串，只逮独立个人词。
            $raw = (Invoke-Git @('grep', '-n', '-I', '-F', '-e', $tok, '--', '.')).Lines
            $pattern = '(?<![A-Za-z0-9])' + [regex]::Escape($tok) + '(?![A-Za-z0-9])'
            foreach ($line in $raw) {
                $content = (@($line -split ':', 3))[-1]
                if ([regex]::IsMatch($content, $pattern)) { $anonLeaks.Add($line) }
            }
        }
        if ($anonLeaks.Count -gt 0) {
            Add-Failure 'vector5 anonymity: a runtime personal token leaked into tracked files' (($anonLeaks | ForEach-Object { ($_ -split ':', 3)[0..1] -join ':' }) -join $newLine)
        }
        Write-Host "vector5 (scanner anonymity): checked $($anonTokens.Count) runtime-derived token(s), zero literals stored"
    }
    # ---- -PrePush：推送前机械自检 ----
    if ($PrePush) {
        $status = (Invoke-Git @('status', '--porcelain', '--untracked-files=normal')).Lines
        if ($status.Count -gt 0) { Add-Failure 'pre-push: working tree is not clean' ($status -join $newLine) }

        $mainRef = Invoke-Git @('rev-parse', '--verify', '--quiet', 'refs/heads/main')
        if ($mainRef.ExitCode -ne 0) { Add-Failure 'pre-push: no local main branch' '' }

        $branch = (Invoke-Git @('rev-parse', '--abbrev-ref', 'HEAD')).Lines
        $head = (Invoke-Git @('rev-parse', '--short', 'HEAD')).Lines
        Write-Host "pre-push: HEAD = $($branch -join '') @ $($head -join '')"

        $upstream = Invoke-Git @('rev-parse', '--abbrev-ref', '--symbolic-full-name', '@{u}')
        if ($upstream.ExitCode -eq 0 -and $upstream.Lines.Count -gt 0) {
            $ahead = (Invoke-Git @('log', '--oneline', ($upstream.Lines[0] + '..HEAD'))).Lines
            Write-Host "pre-push: commits ahead of $($upstream.Lines[0]): $($ahead.Count)"
            foreach ($line in $ahead) { Write-Host "  $line" }
        }
        else {
            Write-Host 'pre-push: no upstream configured; report all local commits manually'
        }

        $tags = Select-Unique ((Invoke-Git @('tag', '--list')).Lines)
        Write-Host "pre-push: local tags = $($tags.Count)"
        if ($tags.Count -gt 0) {
            Write-Host ('  ' + ($tags -join ', '))
            Write-Host '  note: pushing a tag triggers Release CI; confirm the tag set is deliberate'
        }
        Write-Host 'pre-push: mechanical self-check ran'
    }

    if ($knownHits.Count -gt 0) {
        Write-Host ''
        Write-Host 'KNOWN HISTORY DEBT (accepted, not failing this run):'
        foreach ($hit in ($knownHits | Sort-Object -Unique)) { Write-Host "  [known-debt] $hit" }
        Write-Host '  decision: history/tag rewrite requires separate maintainer authorization (AGENTS.md: append-only)'
    }

    if ($failures.Count -gt 0) {
        Write-Host ''
        Write-Host 'PRIVACY AUDIT FAILED:'
        foreach ($failure in $failures) { Write-Host "- $failure" }
        exit 1
    }

    Write-Host ''
    Write-Host 'PRIVACY AUDIT CLEAN'
    exit 0
}
finally {
    Pop-Location
}
