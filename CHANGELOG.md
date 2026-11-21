# 0.1.4

- 静态截图默认隐藏鼠标指针与手型光标（`ShowCursor = false`），录屏仍保留光标显示（`ScreenRecordShowCursor = true`）。
- 修复触发截图后按 `Esc` 无法取消的问题：向截图引擎移交前台焦点权限，并增加捕获期间的全局 `Esc` 取消热键与常驻宿主实时 `CANCEL` 指令兜底。
- 截后浮窗在鼠标悬停、打开菜单或拖拽文件期间暂缓 15 秒自动消失，避免误打断操作。
- 修复多显示器混合缩放下截图浮窗未更新目标屏幕信息的问题；修正自定义边框最小窗口尺寸约束，唤醒主窗口时保留最大化状态。
- 搜索/筛选/切换视图前立即提交未保存的标签与备注草稿；统一预览窗顶栏按钮与下拉框矢量箭头样式。
- Hide mouse cursors in static screenshots while keeping cursors visible in screen recordings.
- Fix `Esc` cancellation after triggering capture by granting foreground activation to the capture host and adding an active-capture `Esc` cancel path.
- Defer 15-second floating preview expiry while hovering, dragging, or interacting with a context menu.
- Fix multi-monitor per-DPI preview placement, preserve maximized window state on restore, enforce minimum track sizes with custom chrome, and flush edits before search/navigation.

# 0.1.3

- 截后浮窗每张独立保留 15 秒，到期逐张消失；连续截图不延长已有浮窗的时间。
- 屏幕容量不足时仍先移除最早的浮窗；浮窗消失不删除历史记录，也不清空剪贴板。
- Each capture preview expires independently after 15 seconds. Subsequent captures do not extend earlier previews.
- Screen capacity still evicts the oldest preview first. Expiration preserves archived captures and the clipboard.

# 0.1.2

- 统一矢量图标、窗口边框和菜单样式，简化浮窗操作；快速历史加入搜索提示，标签支持直接添加和移除。
- 修复输入尚未失焦时的保存与同步覆盖问题；选中记录不再重建整页卡片。
- 成功归档后清理已核对的中转文件，视频/GIF 不再额外保存完整 PNG；本地同步复用媒体路径并优先使用硬链接。
- Ask AI 流式回复保留消息标题和复制按钮；Gemini 支持增量输出，Markdown 在回复完成后排版。
- OCR 改为进程内调用，缩略图缓存限制为 32 MiB，资料库每页最多显示 48 条。
- 修复重复启动、浮窗菜单和最大化窗口边界；更新每显示器 DPI 处理。
- 格式化 C#、XAML 和后端源码，更新由真实界面控件生成的中英文展示图。

- Unify vector icons, window chrome, and menus; simplify floating actions and add editable tag chips.
- Save edits before navigation and merge remote fields without discarding changes made during sync.
- Reclaim verified staging files, avoid redundant video/GIF posters, and reuse local media through hard links where supported.
- Preserve message headers and copy actions during streaming; stream Gemini responses and render Markdown on completion.
- Run Windows OCR in process, cap the thumbnail cache at 32 MiB, and limit each library page to 48 records.
- Activate the existing app on relaunch; fix floating menus and maximized bounds, and improve per-monitor placement.
- Format application sources and refresh documentation artwork from the actual UI controls.

# 0.1.1

- 截图引擎后台预热并复用；取消后立即释放任务状态，空闲回收临时图像，退出应用时清理引擎。
- ChatGPT 与 Google Drive 根据已保存的连接显示登录状态，添加其他账号的入口默认收起。
- 截图前只隐藏自动缩略浮窗；保留手动打开的资料库和快速历史，并移除 250 毫秒固定等待。
- 修复取消截图或录屏准备后残留的引擎进程，避免后续截图和退出一直被阻止。
- 更新设置、Ask AI 和托盘菜单的布局与交互；修复搜索文字被裁切和模型名称显示异常。
- 修复中文 AI 对话传输乱码，并为复制回答加入剪贴板占用重试。
- 修复 Windows 下 Google Drive 授权立即失败，以及同一配置路径导致的云同步连接失败。
- 修复未安装 ChatGPT 可选组件时，取消云授权会导致后台退出的问题。
- Fix immediate Google Drive authorization failures and cloud connection failures on Windows.
- Prewarm and reuse the screenshot engine, release task state after cancellation, reclaim transient images while idle, and stop the host with the application.
- Keep the backend running when cancelling authorization without the optional ChatGPT component.
- Release idle capture hosts after cancellation so subsequent captures and application exit remain available.
- Reflect signed-in and connected accounts in settings; collapse additional-account actions by default.
- Hide only automatic capture previews before capture, preserve manually opened windows, and replace the fixed 250 ms delay with a compositor update when needed.
- Redesign settings, Ask AI, and the tray menu; fix clipped search text and selected model labels.
- Preserve Unicode in AI conversations and retry copying answers when the clipboard is temporarily busy.

# 0.1.0

- 区域、窗口、全屏截图及录屏；标注编辑生成新文件。
- 按可用屏幕高度排列截后浮窗，快速历史与长期资料库。
- 连续截图自动复制最新图片，图片与视频完成行为分别设置。
- 平台快捷键、录音设置记忆，Google Drive / WebDAV 双向合并同步。
- OpenAI / Gemini 自带 Key、官方模型列表、单一启用方案。
- 可选 ChatGPT 登录组件，在安装时显示并确认其非商业许可证。
- 应用、托盘和编辑器窗口统一 LumaShot 图标。
