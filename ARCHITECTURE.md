# 平台边界与数据协议

## Windows 前端

`Main.xaml`、`LumaShot.cs`、`Integrations.cs`：WPF 界面、系统托盘、快捷键、历史交互与应用内问答。
`Core.cs`：截图记录、不可覆盖编辑、JSON 原子保存和备份恢复。
`Engine.cs`：独立 ShareX 便携进程、禁用默认上传、文件归档。
`Ocr.cs`：进程内 Windows OCR，串行识别并限制解码尺寸。
`Backend.cs`：带 requestId 的 JSON-lines stdio IPC；Key 不放在进程命令行。
`CaptureSurfaces.cs`：左侧截后缩略图、顶部快速历史、拖出文件和剪贴板复制。开始捕获前仅隐藏自动截后浮窗，完成后恢复；手动打开的快速历史可被截图。
`Preferences.cs`：平台快捷键、独立的图片/视频完成行为与录屏音频设置。先验证快捷键并尝试注册，失败时恢复原绑定。

## 可复用的 Node 核心

- `backend/main.mjs`：方案管理、单一 AI 请求路由、服务调用、同步调度入口。
- `catalog.mjs`：pi-ai 目录与账号实时列表的交集；展示列表，不自动选择模型。
- `credentials.mjs`：`id / isAvailable / encrypt / decrypt` 统一接口，Windows DPAPI 和 macOS Keychain 实现。平台原生功能不进入共享业务逻辑。
- `backend/siwc`：固定版本官方登录组件，保留源码和许可证。
- `media.mjs`：FFmpeg 时长检查、有限抽帧、时间戳和请求体积检查。
- `sync.mjs`：平台无关的不可变对象/修订协议，可用测试目录模拟两台机器。
- `cloud.mjs`、`process.mjs`：rclone 授权结果、传输和受控子进程。

未来 Mac 版可以以 SwiftUI 或其他前端使用同一个 stdio API。需要实现 ScreenCaptureKit 捕获、系统剪贴板、托盘/菜单栏、全局快捷键、Vision OCR 和原生窗口行为；提供 Mac 的 Node、FFmpeg、rclone 二进制，并重新安装对应平台 npm 依赖。WPF 和 ShareX Windows 引擎不属于可直接运行的 Mac 代码。

## 历史数据

`library.json` 是截图数组，`captures/` 和 `thumbs/` 存放文件。`Id` 是 32 位 GUID。`ParentId` 表示编辑来源，每次编辑有新 Id 和新媒体文件。`File`、`Original`、`Thumb` 均是相对路径。图片原件不覆盖。收藏、标签、备注、OCR 和 AiHistory 可更新。删除仅改变 Deleted。

默认历史和连接信息均不放在代码目录中；`--data` 可显式选择不同的历史目录。每个资料库在本机有独立连接设置。

## 配置同步

`preferences.json` 只保存明确修改的设置。快捷键使用 `hotkeys.windows.<action>` / `hotkeys.macos.<action>`，没有设备 ID。六个捕获操作为 region、window、fullscreen、recordRegion、recordWindow、recordFullscreen，另有 quickHistory、library。未来 Mac 前端读取 macos 键，不从 Windows 组合键自动转换。

`capture.image/video.copy` 和 `.open` 分开记忆；open 为 none/library/folder。`recording.microphone` 默认 false，`recording.systemAudio` 默认 true。每次运行 ShareX 前按当前偏好配置，录屏工具条修改的音频开关在退出时回写；全屏录屏使用虚拟桌面物理坐标的 CustomRegion。

`backend/preferences.mjs` 将各设置生成不可变的 `sync-store/settings/<sha256>.json` 修订，以 Lamport 时钟和修订哈希合并。首次启动的默认值不发布，因此不会覆盖旧设备设置；凭证不属于此协议。传输同时支持 Google Drive 和 WebDAV。保存后本机立即生效，启用同步时下一轮自动同步。

## 同步协议 v2

`sync-store/objects/<sha256>` 保存不可变媒体内容。`revisions/<sha256>.json` 保存有向无环修订记录，包括完整元数据、变化字段、父修订、Lamport 时钟和媒体哈希。上传先对象后修订，rclone 只做增量 copy，不使用 sync/delete。

不同截图 Id 直接取并集；同 Id 的媒体必须一致。元数据按字段合并，较高 Lamport 时钟优先，同钟以修订哈希稳定排序，不受设备墙上时钟漂移影响。问答消息取并集。删除/恢复是 Deleted 字段修订。

下载验证修订哈希、父关系、文件路径、媒体哈希再导入。缺少对象时停止当前合并，下次重试。UI 先提交编辑，再以发送前快照逐字段比较；未修改字段接受远端值，同步期间的本地编辑保留。本地绝对路径 SourceKey、API Key、OAuth token、WebDAV 密码都不进入远端文档。

源媒体按内容存储，可被不同编辑记录引用而不重复上传相同内容。本机在支持的文件系统上用硬链接共享不可变媒体的数据块，不支持时退回复制；合并保留已校验的本地路径。旧修订/已回收媒体当前不自动清理，长期使用需要后续实现显式压缩归档及垃圾回收协议。协议 v1 仅用于开发实验，未提供用户数据迁移承诺。

## 测试

Windows：`LumaShot.exe --self-test --data <新的测试目录>`。
截图引擎：`LumaShot.exe --engine-test --data <新的测试目录>`，会打开自建测试窗口并写入虚构图片到剪贴板。
录屏引擎：`LumaShot.exe --recording-test --data <新的测试目录>`，自建画面进行窗口/全屏各两秒录制，麦克风关闭。
核心：`runtime/node.exe backend/tests/run.mjs <新的测试目录>`。
传输：`runtime/node.exe backend/tests/webdav.mjs <新的测试目录>`，仅回环地址。
官方登录：`runtime/node.exe --test backend/siwc/test/*.test.mjs`。

`backend/tests/mock-http.mjs` 只被测试进程显式加载，正常运行不会加载，且该模拟器从不向外网转发请求。

## 本地资源管理

`StorageMaintenance.cs` 在入库提交并校验 SHA-256 后移除自身 inbox 中转文件。设置中的存储整理仅清除已确认的冗余副本，并保护当前索引和备份索引引用。回收站记录与云端历史不在清理范围。

`ThumbnailCache.cs` 缓存解码缩略图，预算 32 MiB；资料库分页限制同时创建的卡片数为 48。`EditingState.cs` 管理即时模型更新、延迟落盘和切换前提交。`VisualDesign.cs` 与 `DisplayPlacement.cs` 管理矢量图标、窗口工作区边界和每显示器定位。
