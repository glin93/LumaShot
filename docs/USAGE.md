# Settings, storage, and connections / 设置、存储与连接

## Capture preferences / 截图设置

Image and video actions are configured independently: automatic copy, then open nothing, the library, or the file location. Images copy by default; videos do not. Neither opens a window automatically by default. Microphone recording defaults to off and system audio to on. Preferences persist across restarts.

图片、视频分别设置自动复制和完成后的打开位置：不打开、资料库或文件夹。默认复制图片、不复制视频，均不自动打开窗口。麦克风默认关闭，机内声音默认开启。设置会记忆。

Shortcuts use one configuration per platform, shared across that platform's devices. Windows and macOS bindings remain separate when settings sync.

快捷键按平台保存；同平台设备共享一套配置，Windows 与 macOS 配置相互独立。

## Library / 资料库

The default location is `%LOCALAPPDATA%\LumaShot\Library`. Each saved edit creates a new file and record. Each floating preview disappears after 15 seconds independently; later captures do not extend it. When the screen is full, the oldest preview is removed first. Dismissing a preview does not remove its history record or clear the clipboard. The library supports ZIP backup and merge restore.

默认位置为 `%LOCALAPPDATA%\LumaShot\Library`。每次保存编辑都会创建新文件与记录。浮窗每张独立保留 15 秒，后续截图不延长已有浮窗的时间；屏幕放不下时先移除最早的一张。浮窗消失不删除历史，也不清空剪贴板。资料库支持 ZIP 备份和合并恢复。

```powershell
# Use a separate library / 指定独立资料库
./LumaShot.exe --data 'D:\MyCaptures'

# Create synthetic demo images in an empty library / 在空资料库生成示例图片
./LumaShot.exe --demo --data 'D:\LumaShot-demo'
```

Use **Settings → Library → Organize storage** to reclaim verified staging and redundant files. Original captures, trash records, and files referenced by the backup index are retained.

可在**设置 → 资料库 → 整理存储**清理已核对的中转文件和重复副本。原图、回收站记录及备份索引仍引用的文件会保留。

## AI connections / AI 连接

Add an OpenAI or Gemini key in **Settings → AI services**, then select a model from the account's supported image-model list. Multiple profiles can be saved; exactly one is active. The floating preview shows the AI action when an active connection has a model selected. Media is sent when you submit a question.

在**设置 → AI 服务**中添加 OpenAI 或 Gemini Key，再从账号支持的图像模型列表中选择模型。可保存多个方案，同时启用一个；启用方案并选择模型后，浮窗显示问 AI。提交问题时才发送所选媒体。

ChatGPT sign-in requires the optional installer component and an account that supports token sharing. Its license is shown when selecting the component. Its implementation uses the official DevKit with PKCE, state/nonce verification, encrypted credential storage, and token rotation.

ChatGPT 登录需要安装可选组件，账号需支持 token sharing。安装时展示组件许可。登录实现基于官方 DevKit，使用 PKCE、state/nonce 校验、加密凭证存储与令牌轮换。

For video questions, LumaShot samples timestamped JPEG frames: up to 10 minutes, 24 frames, a 1,600-pixel longest edge, and 18 MiB of encoded image data. These are application limits. Original video and audio are not uploaded; events between sampled frames may be missed.

视频问答使用带时间戳的 JPEG 采样画面：最长 10 分钟、最多 24 帧、每帧最长边 1600 像素、编码后图片总量不超过 18 MiB。这是应用的处理上限。原始视频与音频不上传，采样帧之间的事件可能遗漏。

## Cloud sync / 云同步

Connect Google Drive through browser OAuth, or enter an HTTPS WebDAV address and credentials. Enabled connections merge the library automatically every minute. Media, metadata, OCR, AI conversations, and capture settings are included. Trash state syncs; cloud objects are not physically removed automatically. Pausing or disconnecting keeps existing remote files.

Google Drive 通过浏览器 OAuth 连接；WebDAV 使用 HTTPS 地址和账号凭据。启用后每分钟自动合并媒体、元数据、OCR、AI 对话与截图设置。回收站状态会同步，云端对象不自动物理删除。暂停或断开连接会保留已有远程文件。

Google Drive uses `drive.file` scope. Cloud content is not end-to-end encrypted. Credentials are stored separately in `%LOCALAPPDATA%\LumaShot\Connections`, protected with Windows DPAPI, and excluded from sync and backups. A new device needs its own sign-in or API keys. Uninstalling retains the library and connection storage.

Google Drive 使用 `drive.file` 授权范围。云端内容未做端到端加密。凭据单独保存在 `%LOCALAPPDATA%\LumaShot\Connections`，由 Windows DPAPI 保护，不参与同步或备份；新设备需重新登录或输入 Key。卸载保留资料库和连接配置。
