# 版本发布流程

本仓库从 `v0.3.1` 开始使用 Git 与 GitHub 保存版本。

以后每次发布都按以下顺序执行：

1. 修改源码、素材和 `README.md`。
2. 更新 `GeorgeRecovery/manifest.json` 中的版本号。
3. 运行 `.NET` 编译，要求 0 个错误，并检查 ZIP 内容。
4. 把源码改动提交到 Git，提交信息写明版本号和主要变化。
5. 创建同名版本标签，例如 `v0.3.2`。
6. 推送源码与标签到 GitHub。
7. 创建 GitHub Release，并附上 `GeorgeRecovery X.Y.Z.zip`，方便直接下载。

## 仓库中保存什么

- C# 源码、项目文件和 manifest。
- Mod 自制最终素材。
- README、版本说明和发布脚本。

## 不上传什么

- `bin/`、`obj/` 等可重新生成的编译文件。
- `_build/` 中的临时图片、预览和本地研究资料。
- 从 Stardew Valley 游戏文件中提取的原版素材。
- `archive/` 中的本机安装备份。
- 密钥、登录令牌、日志或其他个人信息。
