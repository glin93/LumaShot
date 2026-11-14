# Validation scope / 验证范围

The reports in [`validation/`](../validation/) cover the following checks through v0.1.1:

- 12 library, backup, preference, and shortcut checks.
- Four process-fixture regressions for cancelled selectors, including repeated cancellation, an untitled active window, cancellation before the first poll, and recovery after a nonzero exit. Run with `--lifecycle-test --data <test-directory>`.
- Reusable-host integration tests cover 30 captures across two bursts, 14 selector cancellation paths, idle memory reclamation with a prewarmed replacement, command dispatch after replacement, concurrent request rejection, child-process crashes, recovery, and application shutdown. Run `--resident-test --data <test-directory>`. Desktop load and interaction can affect timings. See `validation/resident-capture.txt` for both transient and settled memory figures.
- Four layout checks for Chinese search text, passwords, selected model labels, and scrollbars; a real C#–Node Unicode round trip.
- The rounded tray menu was inspected at desktop scaling and exercised with keyboard navigation and its Settings action.
- Five capture-surface checks, including a 12-image burst and 20 overlapping clipboard writes.
- 22 backend checks with and without the optional ChatGPT component, including cancellation without that component. AI requests use mock transport; they do not incur API charges.
- Real rclone authorization startup with the production config path: the local OAuth endpoint redirects to Google with `drive.file` scope. This does not complete account sign-in.
- Real ShareX window capture, video thumbnails, GIF conversion, and two-second window/full-screen recordings with an AAC system-audio track.
- Real local WebDAV transfer of media and platform preferences.
- Preview-to-editor navigation, save-as-new-record behavior, cancellation, child-process cleanup, and editor icon/caption presentation.
- Official sign-in component: 56 passing tests and one Unix-only permission test skipped on Windows.

Real Google Drive, hosted WebDAV, ChatGPT account authorization, and paid model calls need account-level acceptance testing. Microphone audio content, multi-monitor combinations, scrolling capture, and all annotation tools have not completed full end-to-end coverage. The macOS interface and capture engine are not implemented; the Keychain adapter has not been tested on macOS hardware.

`validation/` 保存截至 v0.1.1 的具体测试报告。已验证资料库、备份、设置、连续截图、剪贴板顺序、后端方案切换、本地 WebDAV 传输、真实窗口截图与录屏、编辑另存和窗口退出流程。v0.1.1 增加真实 Google 授权启动和取消授权回归；AI 推理使用模拟网络。

真实云端账号与模型调用仍需授权后验收；麦克风声音、多显示器、滚动截图及全部标注工具尚未完成完整端到端覆盖。macOS 界面与捕获引擎尚未实现，Keychain 适配器尚未在 Mac 实机验证。
