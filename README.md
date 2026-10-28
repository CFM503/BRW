# 中国燃气 - Oracle EBS 专属工作台

专门针对中国燃气 Oracle EBS 业务定制的极简、轻量专用浏览器。

---

## 🌟 核心特色

1. **精准兼容 Oracle Forms (Java Applet)**：
   - 采用 32位 (x86) 架构编译，原生挂载您电脑上的 32位 Java 6 运行环境 (`C:\Program Files (x86)\Java\jre6`)，完美弹出并运行 EBS Forms 表单界面。
2. **纯粹极简，无广告与多余功能**：
   - 仅保留前进、后退、刷新、门户首页、地址栏、页面缩放、会话清理等核心功能。
3. **单点登录 (SSO) 与 Token 无缝衔接**：
   - 默认直接打开中国燃气统一门户：`https://portal.chinagasholdings.com/group/frontend`。
   - 登录后由门户传递 Token 并重定向至 EBS，会话与 Cookie 完全共享。
4. **多窗口与数据导出原生支持**：
   - 完美接管 EBS 中的弹出窗口（`window.open`），子窗口自动继承登录会话与权限。
   - EBS 中导出的 Excel / CSV / 报表支持直接调用 Windows 系统的保存对话框进行下载保存。
5. **高分屏高清优化 (High-DPI)**：
   - 支持 Windows Per-Monitor 高 DPI 缩放，文字界面清晰不发虚。

---

## 🚀 启动与使用方式

- **桌面快捷方式**：您的桌面上已自动生成 **“中国燃气-EBS专用浏览器”** 快捷方式，直接双击即可启动。
- **程序文件位置**：`d:\SOFT\AI\BRW\EBSBrowser.exe`

### 常用快捷键：
* `F5`：刷新当前页面
* `Alt + ←`：后退
* `Alt + →`：前进
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
