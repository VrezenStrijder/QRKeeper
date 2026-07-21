# QRKeeper

QRKeeper 是一款跨平台二维码收集与管理工具，支持 Windows 桌面端和 Android 端。它可以保存二维码内容与二维码图片，提供扫码、图片导入、备份恢复、局域网同步和在线更新检查等功能。
[English](./README.md)

## 功能特性

- 二维码记录管理：创建、搜索、编辑标题/备注、删除和拖动排序。
- 二维码图片保存：记录会保存对应二维码图片，支持预览、导出和分享。
- 多种导入方式：手动输入内容、导入图片、Android 相机扫码、Windows 屏幕区域识别。
- 备份与恢复：支持 `.qrbak` 备份文件，桌面端和 Android 端互通。
- 导入预览：从备份文件预览记录后再选择导入。
- 局域网同步：同一局域网内的 QRKeeper 客户端可互相发现，并按“标题 + 内容完全一致”判定重复记录后增量同步。
- 多语言：支持中文和英文，首次运行按系统语言自动选择。
- 主题与配色：支持浅色、深色、跟随系统，以及多种配色风格。
- 在线更新：通过 GitHub Releases 和 `update.json` 检查 Windows/Android 新版本。

## 支持平台

- Windows 桌面端：Avalonia + FluentAvalonia。
- Android 端：Avalonia Android，面向 ARM64 真机构建。

## 快速开始

需要安装 .NET SDK、Android workload 和 Microsoft JDK 17。Android 调试可使用 Visual Studio 或命令行。

```powershell
dotnet build src\QRKeeper.sln -c Debug
dotnet run --project src\QRKeeper.UI\QRKeeper.UI.csproj -c Debug
dotnet build src\QRKeeper.Android\QRKeeper.Android.csproj -f net8.0-android -c Debug
```

生成 Android Release APK：

```powershell
.\scripts\build-android-release.ps1
```

## 在线更新

`update.json` 位于 `release/update.json`。应用会检查最新 Release，读取该文件并打开对应平台的下载链接；当前版本不会静默安装更新。

## 数据与同步说明

- 同步只做增量补充，不覆盖、不删除远端数据。
- 重复记录判定规则为：二维码标题和二维码内容完全一致。
- 恢复备份会覆盖当前数据，应用会在恢复前创建安全备份。
- 局域网同步依赖同一 Wi-Fi/LAN、系统权限、防火墙和路由器广播设置。

