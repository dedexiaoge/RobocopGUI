\# Robocopy GUI



Windows 文件复制工具 \[Robocopy](https://learn.microsoft.com/zh-cn/windows-server/administration/windows-commands/robocopy) 的图形界面，使用 WPF (.NET 8) 构建，双击 EXE 直接运行，无需安装任何额外运行时。



\## 截图



!\[Robocopy GUI](screenshot.png)



\## 功能



\- \*\*复制模式\*\* — 全部文件 / 镜像 (MIR) / 移动 (MOV) / 移动并清理 (MOVE)

\- \*\*子目录控制\*\* — 不复制 / 所有子目录 (/S) / 含空子目录 (/E)

\- \*\*过滤器\*\* — 文件筛选、排除文件 (XF)、排除目录 (XD)

\- \*\*高级选项\*\* — 重试次数 / 等待时间 / 多线程 (/MT) / 可重启模式 (/Z) / 备份模式 (/B)

\- \*\*日志记录\*\* — 支持覆盖 (/LOG) 和追加 (/LOG+) 两种模式

\- \*\*附加参数\*\* — 可输入任意 robocopy 额外参数

\- \*\*命令预览\*\* — 实时显示生成的完整命令，支持一键复制

\- \*\*模拟执行\*\* — 使用 /L 参数预览，不实际复制文件

\- \*\*实时输出\*\* — 实时滚动显示 robocopy 控制台输出

\- \*\*退出码解读\*\* — 自动解读 robocopy 退出码含义



\## 系统要求



\- Windows 10 / 11

\- .NET 8 Desktop Runtime（如需从源码编译则需要 .NET 8 SDK）



\## 下载



从 \[Releases](../../releases) 页面下载最新版 EXE，直接双击运行。



> 无需安装，无需管理员权限（除非复制受保护的系统目录）。



\## 从源码编译



```bash

git clone https://github.com/dedexiaoge/RobocopyGUI.git

cd RobocopyGUI

dotnet build -c Release



