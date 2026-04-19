# Hanime1Downloader.CSharp
通宵三天完全重构修复了下    
一个基于 WPF 和 .NET 9 的桌面工具，用于搜索、查看详情、获取hanime1视频源并管理下载队列。

## 界面   
<img width="1722" height="1131" alt="经典" src="https://github.com/user-attachments/assets/98ea7e3f-fa4b-49fc-be24-e1a5c58db4aa" />    
精简模式   
<img width="1722" height="1131" alt="精简" src="https://github.com/user-attachments/assets/461250f0-2c31-44a1-969a-68b981b921b8" />

## 功能
- 搜索视频
- 查看视频详情与相关视频
- 获取不同清晰度的视频源
- 下载队列管理
- Cloudflare 验证会话复用
- 列表封面显示
- 播放器窗口播放视频

## 运行环境
- Windows
- .NET 9 SDK
- WebView2 Runtime

## 构建
```bash
dotnet build "Hanime1Downloader.CSharp.csproj"
```

## 运行
```bash
dotnet run --project "Hanime1Downloader.CSharp.csproj"
```

## 项目结构
- `MainWindow.xaml` / `MainWindow.xaml.cs`：主界面与主流程
- `Views/`：弹窗与子窗口
- `Services/`：站点访问、下载、日志、缓存等服务
- `Models/`：数据模型
- `Assets/`：筛选等静态资源


