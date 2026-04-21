<div align="center">

# 🎬 Hanime1 Downloader

### 基于 WPF + .NET 9 的 Hanime1 视频下载与管理工具

[![GitHub Stars](https://img.shields.io/github/stars/yxxawa/hanime1DownLoader?style=for-the-badge&logo=github&color=fbbf24)](https://github.com/yxxawa/hanime1DownLoader/stargazers)
[![GitHub Forks](https://img.shields.io/github/forks/yxxawa/hanime1DownLoader?style=for-the-badge&logo=github&color=6b7280)](https://github.com/yxxawa/hanime1DownLoader/network/members)
[![GitHub Issues](https://img.shields.io/github/issues/yxxawa/hanime1DownLoader?style=for-the-badge&logo=github&color=ef4444)](https://github.com/yxxawa/hanime1DownLoader/issues)
[![GitHub License](https://img.shields.io/github/license/yxxawa/hanime1DownLoader?style=for-the-badge&logo=open-source-initiative&color=3b82f6)](https://github.com/yxxawa/hanime1DownLoader/blob/main/LICENSE)

[![Platform](https://img.shields.io/badge/Platform-Windows%20x64-0078D6?style=flat-square&logo=windows11&logoColor=white)](#)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?style=flat-square&logo=dotnet&logoColor=white)](#)
[![WPF](https://img.shields.io/badge/UI-WPF-5C2D91?style=flat-square&logo=xaml&logoColor=white)](#)
[![WebView2](https://img.shields.io/badge/WebView2-Runtime-blue?style=flat-square&logo=microsoftedge&logoColor=white)](#)

</div>

---

## 📢 社区交流

> **QQ 反馈 & 吹水群：`1102413927`**  
> 使用遇到问题、有功能建议、或者单纯想聊天，都欢迎来玩。

---

## 📸 界面预览

### 🖥️ 主界面 · 浅色模式
![浅色主界面](https://github.com/user-attachments/assets/f95681a9-dc23-4fb9-bb7b-ed99afc53b0b)

### 🌙 主界面 · 深色模式
![深色主界面](https://github.com/user-attachments/assets/31c3e4c3-ae8d-4ad7-88eb-68a645715251)

### 🪶 精简模式
![精简模式](https://github.com/user-attachments/assets/a87d7e28-7fca-4c24-aef1-142fa0ce4022)

---

## ✨ 功能特性

### 🔍 搜索与浏览
- **视频搜索**：快速搜索 Hanime1 视频资源
- **高级筛选**：按标签、日期、热度等条件精准筛选
- **详情查看**：完整展示简介、标签列表、相关视频推荐

### 📥 下载与队列管理
- **多清晰度获取**：自动解析不同画质的视频源
- **下载队列**：支持并发下载、暂停/恢复、失败自动重试
- **历史记录**：自动记录已下载任务，避免重复下载
- **失败重解析**：下载失败后可手动触发重新解析视频源

### ⭐ 收藏与数据管理
- **收藏夹**：新建、重命名、删除、导入/导出收藏列表
- **配置文件便携化**：所有数据文件均保存在程序目录，方便迁移

### 🎨 个性化与体验
- **明暗主题**：一键切换浅色 / 深色外观
- **精简模式**：隐藏无关信息，界面更清爽
- **详情面板自定义**：自由开关显示项，只看你关心的内容
- **内置播放器**：无需切换软件，直接预览视频源
- **Cloudflare 会话复用**：减少反复验证，提升使用流畅度
- **封面与缩略图缓存**：列表浏览更顺滑

---

## ⚙️ 运行要求

| 环境               | 要求                                                                          |
| :----------------- | :---------------------------------------------------------------------------- |
| **开发运行**       | Windows、[.NET 9 SDK](https://dotnet.microsoft.com/download)、WebView2 Runtime |
| **发布版运行**     | Windows x64、[.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0)、WebView2 Runtime |

> 💡 **提示**：WebView2 Runtime 通常已预装在 Windows 11 中，若缺失可[点击此处下载](https://developer.microsoft.com/microsoft-edge/webview2/)。

---

## 🛠️ 开发与构建

```bash
# 构建项目
dotnet build "Hanime1Downloader.CSharp.csproj"

# 直接运行
dotnet run --project "Hanime1Downloader.CSharp.csproj"
```

### 📦 发布（单文件、非自包含、win-x64）

项目已配置为 **单文件发布**，产物目录如下：

```bash
dotnet publish "Hanime1Downloader.CSharp.csproj" -c Release -p:DebugType=None -p:DebugSymbols=false
```

发布输出路径：
```
bin/Release/net9.0-windows/win-x64/publish/
```

---

## 📁 数据与配置文件

所有数据均存储在程序同目录下，**纯绿色便携**，拷贝文件夹即可完整迁移：

| 文件                      | 说明                     |
| :------------------------ | :----------------------- |
| `settings.json`           | 程序主设置               |
| `favorites.json`          | 收藏夹数据               |
| `download_history.json`   | 下载历史记录             |
| `download_queue.json`     | 下载队列（可设置是否保留） |
| `cookies.json`            | Cloudflare 会话缓存      |
| `Downloads/`              | 默认下载保存目录         |

---

## 🎛️ 设置项一览

在设置窗口中你可以自定义以下选项：

- 🎬 **默认画质**（最高 / 高 / 中等 / 低）
- 🔄 **最大同时下载任务数**
- 🌐 **站点域名管理**（支持自定义镜像站点）
- 💾 **关闭程序后是否保留下载队列**
- 📂 **保存路径**与**文件命名规则**
- 🌓 **明暗主题切换**
- 🖼️ **列表封面显示开关**
- 🪶 **精简模式开关**
- 🧩 **详情面板显示项自定义**

---

## 📂 项目结构速览

```text
Hanime1Downloader.CSharp/
├── MainWindow.xaml(.cs)       # 主界面与核心交互逻辑
├── Views/                     # 子窗口：设置、筛选、WebView验证、播放器等
├── Services/                  # 业务服务：HTTP请求、下载器、主题管理、缩略图缓存等
├── Models/                    # 数据模型：设置、视频信息、下载项等
├── Assets/                    # 静态资源（如筛选项数据）
├── Themes/                    # 浅色 / 深色主题 XAML 资源
└── Converters/                # XAML 绑定值转换器
```

---

## 📄 许可证

本项目遵循 [MIT License](https://github.com/yxxawa/hanime1DownLoader/blob/main/LICENSE) 开源协议，欢迎 Star、Fork 与 PR。

---

<div align="center">

**[⬆ 返回顶部](#hanime1-downloader)**

*Made with ❤️ by [yxxawa](https://github.com/yxxawa)*

</div>

