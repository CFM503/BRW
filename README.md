# 中国燃气 - Oracle EBS 专属工作台 (v1.0.5)

[![Release](https://img.shields.io/github/v/release/CFM503/BRW?include_prereleases&style=flat-square)](https://github.com/CFM503/BRW/releases)

专门针对中国燃气 Oracle EBS 业务定制的极简、轻量专用浏览器。单个独立 exe 运行，无多余依赖，专为稳定高效办公打造。

---

## 🌟 核心特色

1. **门户账号密码安全记录与自动填充（NEW）**：
   - 可以在“⚙ 设置”中保存您的工号和密码，密码采用 **Windows 原生 DPAPI 高强度数据保护加密**，仅限本机当前登录账户解密，绝非明文存储。
   - 每次打开燃气门户登录页，浏览器会自动将账号密码填入输入框，无需重复手动敲键盘，一键登录！
2. **保活心跳间隔自由设置（NEW）**：
   - 在“⚙ 设置”中支持自由调整 **保活心跳间隔（1 ~ 30 分钟）**，保存后即刻热更新生效，适配不同网络环境。
3. **单窗口多页签（Tabbed UI）设计**：
   - 彻底摆脱多弹窗杂乱痛点！点击门户进入 EBS 时，**自动在同一窗口内新建标签页**，桌面与任务栏只保留一个整洁的主窗口。
   - 自定义现代化 Tab 标签栏，支持独立标题、`[×]` 一键关闭、中键关闭与 `[➕ 新页签]`。
   - **智能防误关保护**：关闭正在运行 Oracle EBS 的页签时会弹出二次防误关确认，避免丢失未保存的表单数据。
4. **智能防超时保活 (Anti-Timeout Keep-Alive)**：
   - 双轨保活机制：后台自动向 EBS 业务端发送动态 OAF 凭证心跳刷新数据库会话，并通过底层 Win32 消息队列直接向 Oracle Forms 窗口投递无感微脉冲（即使窗口最小化或在后台也 100% 生效）。
   - 底部状态栏提供实时可视化诊断（显示 Web 刷新状态与 Forms 窗口保活计数），支持点击立即手动测试。
5. **精准兼容 Oracle Forms (Java Applet)**：
   - 采用 32位 (x86) 架构编译，原生挂载您电脑上的 32位 Java 6 运行环境 (`C:\Program Files (x86)\Java\jre6`)，完美弹出并运行 EBS Forms 表单界面。
6. **白屏与未响应彻底免疫**：
   - 启动时自动禁用冲突的 DirectDraw 硬件加速（改为稳定软件渲染），并自动清理后台残留的假死 Java 僵尸进程。
7. **单点登录 (SSO) 与 Token 无缝衔接**：
   - 默认直接打开中国燃气统一门户：`https://portal.chinagasholdings.com/group/frontend`。
   - 登录后由门户传递 Token 并重定向至 EBS，各标签页间 Cookie 与 Token 完全共享。
8. **底层静默脚本语法报错**：
   - 底层 COM 接管语法错误拦截，彻底杜绝登录时弹出的 `base64.js 语法错误` 干扰框。
9. **数据导出原生支持**：
   - EBS 中导出的 Excel / CSV / 报表支持直接调用 Windows 系统的保存对话框进行下载保存。
10. **高分屏高清优化 (High-DPI)**：
   - 支持 Windows Per-Monitor 高 DPI 缩放，文字界面清晰不发虚。

---

## 🚀 启动与使用方式

- **桌面快捷方式**：双击桌面的 **“中国燃气-EBS专用浏览器”** 快捷方式即可启动。
- **程序文件位置**：`d:\SOFT\AI\BRW\EBSBrowser.exe`

### 常用快捷键：
* `Ctrl + T`：新建标签页
* `Ctrl + W`：关闭当前标签页
* `F5`：刷新当前标签页
* `Alt + ←` / `Alt + →`：后退 / 前进
* `Ctrl + +` / `Ctrl + -`：放大 / 缩小页面（支持 50% ~ 250%）
* `Ctrl + 0`：恢复 100% 原始大小
* `Ctrl + L`：快速定位并全选地址栏

---

## ⚙️ 配置文件 (config.ini)

如需修改门户默认网址或窗口标题，可直接在软件工具栏点击 **“⚙ 设置”**，或直接编辑同目录下的 `config.ini`：
```ini
AppTitle=中国燃气 - Oracle EBS 专用工作台
HomeUrl=https://portal.chinagasholdings.com/group/frontend
DefaultZoom=100
```

---

## 🛠 重新编译说明

若后续需要修改代码，直接双击运行目录下的 `build.bat` 即可利用 Windows 内置的编译器一键自动完成编译打包。
