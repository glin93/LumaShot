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
