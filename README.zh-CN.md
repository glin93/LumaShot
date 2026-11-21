<p align="center">
  <img src="LumaShot-icon.png" width="84" alt="LumaShot" />
</p>

<h1 align="center">LumaShot</h1>

<p align="center">
  <strong>专为 Windows 打造的现代本地优先截图、录屏与视觉资料库工作流。</strong><br />
  即用即走的截后浮窗、离线 OCR 全文检索、原图无损标注、无冲突云同步与流式 Ask AI —— 集速度、颜值与掌控感于一体。
</p>

<p align="center">
  <a href="README.md">English</a> · <strong>简体中文</strong>
  &nbsp;|&nbsp;
  <a href="https://github.com/glin93/LumaShot/releases/tag/v0.1.4">下载 v0.1.4</a> ·
  <a href="CHANGELOG.md">更新日志</a> ·
  <a href="https://github.com/glin93/LumaShot/issues">反馈问题</a>
</p>

![LumaShot 截图资料库](docs/images/library.png)

## 为什么选择 LumaShot

- **即按即截，画面干净** — 预热常驻捕获引擎，按下快捷键毫秒级拉起选区。静态截图自动隐藏箭头与手型等鼠标光标，屏幕录制则自然保留鼠标轨迹；随时按 `Esc` 即可干脆取消。
- **高颜值智能截后浮窗** — 截完图自动停靠在屏幕左侧，悬停浮现半透明暗色胶囊操作栏：一键复制、标注编辑、Ask AI、屏幕贴图，或直接把图片拖入聊天与文档应用。每张浮窗独立 15 秒自动收起，鼠标悬停、打开菜单或拖拽时自动暂停计时。
- **永久资料库与顶部快速历史** — 按 `Ctrl + Shift + H` 呼出顶部快速历史条，按 `Ctrl + Shift + L` 打开完整资料库。内置本地离线 Windows OCR 自动提取图中文字，配合可视化标签胶囊、可搜索备注、收藏夹与可恢复回收站，再多截图也能秒级找回。
- **非破坏式编辑与屏幕贴图** — 每次保存标注都会生成关联原图的新版本，历史原图永不被覆盖；支持将任意截图置顶贴在屏幕前端对照参考。
- **灵活录屏与一键转 GIF** — 支持区域、窗口与全屏录制，独立开关机内声音与麦克风并记忆偏好，支持直接将录屏导出为 GIF 动图。
- **多模态流式 Ask AI** — 使用你自己的 OpenAI 或 Gemini API Key，对任意截图或长录屏（自动均匀抽帧）进行流式对话，Markdown 回答直接保存在对应记录的上下文历史中。
- **本地优先与无冲突云同步** — 截图与历史索引以明晰的本地文件保存，支持一键 ZIP 备份与合并恢复；可选开启 Google Drive 或 WebDAV（NAS / Nextcloud）双向增量同步，凭证通过 Windows DPAPI 本机加密。

## 界面展示

| 截后浮窗 | 快速历史 |
| --- | --- |
| ![截后浮窗与快捷操作](docs/images/floating-previews.png) | ![带实时搜索的顶部快速历史](docs/images/quick-history.png) |

| 设置 | Ask AI |
| --- | --- |
| ![截图、录屏与音频偏好设置](docs/images/settings.png) | ![支持 Markdown 排版的流式截图对话](docs/images/ask-ai.png) |

## 快速上手

1. **捕获** — 按 `Ctrl + Shift + 2` 自由区域截图（默认自动复制到剪贴板）。
2. **流转** — 在左侧截后浮窗中直接复制、编辑标注、问 AI，或按住卡片直接拖进微信/飞书/浏览器等目标窗口。
3. **回溯** — 按 `Ctrl + Shift + H` 调出快速历史，或按 `Ctrl + Shift + L` 进入完整资料库检索标题、标签、备注与图内 OCR 文字。

| 操作 | 默认快捷键 |
| --- | --- |
| 自由截图 | `Ctrl + Shift + 2` |
| 窗口截图 | `Ctrl + Shift + 4` |
| 全屏截图 | `Ctrl + Shift + 3` |
| 自由录屏 | `Ctrl + Shift + 5` |
| 窗口录屏 | `Ctrl + Shift + 6` |
| 全屏录屏 | `Ctrl + Shift + 7` |
| 快速历史 | `Ctrl + Shift + H` |
| 资料库 | `Ctrl + Shift + L` |

所有快捷键均可在**设置**中自由修改。关闭资料库主窗口后程序留在系统托盘持续待命，需要彻底退出时选择**退出 LumaShot** 即可。

## 安装

前往 [Releases](https://github.com/glin93/LumaShot/releases) 下载 **[LumaShot-0.1.4-Setup-x64.exe](https://github.com/glin93/LumaShot/releases/tag/v0.1.4)**。安装包已内置所需全部运行环境，支持 **Windows 10 (22H2+)** 与 **Windows 11 (x64)** 当前用户目录免管理员安装。

> **说明：** v0.1.4 安装包尚未购买代码签名（首次运行时 SmartScreen 可能会提示确认）。安装向导中的 ChatGPT 登录为可选组件并附带独立非商业许可；使用 OpenAI / Gemini API Key 无需勾选该组件。

## 源码构建

LumaShot 采用 C# / WPF 构建原生桌面界面，基于 ShareX 提供捕获与标注能力，FFmpeg 负责媒体处理，Node.js 后端驱动 AI 路由与同步状态机，rclone 负责云端增量传输。

在仓库根目录通过 PowerShell 运行以下命令即可按固定校验值下载依赖并完成构建：

```powershell
./bootstrap-windows.ps1
```

依赖就绪后的日常增量编译：

```powershell
./build.ps1
```

如需构建安装程序（需安装 Inno Setup 7.1.1）：

```powershell
./build-installer.ps1 -Compiler 'C:\path\to\Inno Setup\ISCC.exe'
```

## 文档

- [设置、存储与账号连接](docs/USAGE.md)
- [架构与 macOS 平台边界](ARCHITECTURE.md)
- [测试与验证范围](docs/VALIDATION.md)
- [版本记录](CHANGELOG.md)
- [第三方依赖](THIRD_PARTY.md)与[对应源码](SOURCES.md)

## 许可

LumaShot 自有代码采用 [GPL-3.0-only](LICENSE)。第三方组件保留各自许可证；可选的 [ChatGPT 登录组件](backend/siwc/LICENSE)采用独立的非商业许可证。
