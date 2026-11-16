# Validation scope / 验证范围

The reports in [`validation/`](../validation/) cover the following checks through v0.1.2:

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

## v0.1.2 refinement

- 11 checks for per-field sync, immediate edit/tag saving, 60-record pagination, initial switch state, stable streaming controls, a bounded thumbnail cache, inbox cleanup, in-process OCR, monitor coordinates, and backup-aware redundant-file cleanup.
- 22 backend checks rerun, including original-path preservation and NTFS hard-link deduplication. Four streaming tests cover fragmented UTF-8, incomplete responses, cancellation, thought-part filtering, and the optional component's inline-image extension.
- Library/backup, layout, capture surfaces, selector lifecycle, resident-host bursts/recovery, and real window/full-screen recording regressions passed. A selector test race was fixed by waiting for the actual selector before cancelling it.
- Manual UI inspection covered the library, all six settings sections, Ask AI, preview, automatic thumbnails, quick-history search/copy, floating menus, tags, maximize/restore, close-to-tray, and repeated launch. See `validation/ui-refinement.txt`.
- README artwork is rendered from the actual controls with synthetic content and grayscale text rasterization.

本轮验证增加即时编辑保存、逐字段同步、60 条记录分页、标签、流式消息、进程内 OCR、磁盘清理与硬链接回归。界面逐页检查及具体操作记录见上述报告。真实付费 API、云端账号写入和物理多显示器组合未在本轮测试。