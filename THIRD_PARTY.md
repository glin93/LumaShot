# 依赖与来源

| 组件 | 版本 / 来源 | 许可及用途 |
| --- | --- | --- |
| .NET apphost | Microsoft.NETCore.App.Host.win-x64 10.0.14，微软官方 NuGet 包 | MIT，常驻截图进程启动器；许可及第三方声明见 licenses/dotnet-apphost-* |
| MdXaml / MdXaml.Plugins | [1.27.0](https://github.com/whistyun/MdXaml) | MIT，AI 回答 Markdown 排版；许可见 licenses/MdXaml-MIT.txt |
| AvalonEdit | [6.3.0.90](https://github.com/icsharpcode/AvalonEdit) | MIT，MdXaml 文本渲染依赖；许可见 licenses/AvalonEdit-MIT.txt |
| ShareX | [v22.4.0](https://github.com/ShareX/ShareX/releases/tag/v22.4.0)，官方 portable-x64 ZIP | GPL-3.0，未修改独立进程；[对应源码](https://github.com/ShareX/ShareX/tree/v22.4.0)，完整附带许可在 engine/Licenses |
| FFmpeg | ShareX 便携包自带 `n9.0.2-48-gd4070a14ce-20261107` | 此构建启用 GPL/version3，许可在 engine/Licenses/FFmpeg_license.txt；[FFmpeg 源码](https://git.ffmpeg.org/gitweb/ffmpeg.git/commit/d4070a14ce)、[Windows 构建项目](https://github.com/BtbN/FFmpeg-Builds) |
| Node.js | [v24.22.3 Windows x64](https://nodejs.org/dist/v24.22.3/) | runtime/LICENSE.txt，包含其第三方通知 |
| rclone | [v1.76.0](https://github.com/rclone/rclone/releases/tag/v1.76.0) | MIT；官方归档和随包文档保留在 sync-tools |
| OpenAI Sign in with ChatGPT DevKit | [固定 commit 0a36fef](https://github.com/openai/sign-in-with-chatgpt-devkit/tree/0a36fefeb913055c8c7a1a29b63d82b96b2e841a) | **Noncommercial License**，不是 MIT/OSI 开源许可；源码、LICENSE、通知、修改说明位于 backend/siwc |
| openai | 7.21.0 | Apache-2.0，官方 SDK |
| jose | 6.2.12 | MIT，身份签名验证，由 DevKit 使用 |
| proper-lockfile | 4.1.2 | MIT，由 DevKit 使用 |
| @earendil-works/pi-ai | 1.5.2 | MIT，[项目源码](https://github.com/earendil-works/pi/tree/main/packages/ai)，复用模型目录 |
| @napi-rs/keyring | 2.1.2 | MIT，[项目源码](https://github.com/Brooooooklyn/keyring-node)，macOS Keychain 适配；原生依赖通知保留在安装包中 |

全部 npm 精确依赖和完整性值见 backend/package-lock.json，安装后的各包 LICENSE/NOTICE 保留在 node_modules。DevKit 原有第三方通知原样保留，其列举范围可能大于本应用实际使用的组件。

DeepSeek Harness 的参考来源：[官方仓库](https://github.com/deepseek-ai/deepseek-harness)，特别是 `llm-pi-ai` 的凭证/模型目录分离设计。未复制其产品界面或整套代理框架。仅依赖其所用的公开模型目录库。


各依赖的许可证分别适用。DevKit 限非商业用途；商业使用需另获 OpenAI 协议。
