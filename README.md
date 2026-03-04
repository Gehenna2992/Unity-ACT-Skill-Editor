# ⚔️ Unity ACT Skill Editor (数据驱动动作编辑器)

[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](https://opensource.org/licenses/MIT)
[![Unity](https://img.shields.io/badge/Unity-2021.3%2B-blue.svg)](https://unity.com/)

一个专为 Unity 打造的**无代码、可视化、数据驱动**的动作游戏技能编辑器。
彻底告别 Animator 面板里乱如蛛网的连线，告别难用的 Animation Event！

## 🌟 为什么做这个插件？ 
传统 Unity 动作开发中，状态机连线极其繁琐，且动画帧事件（Event）无法可视化调节。
本项目参考了现代 3A 动作游戏（如《绝区零》、《鬼泣》）的底层逻辑，实现了：
*   **所见即所得**：每一帧的攻击判定框（Hitbox）、特效、音效，均可在 Scene 视图可视化调节。
*   **纯 Root Motion 控制**：告别滑步！完美支持起步、循环、刹车的无缝衔接。
*   **连招与打断窗口**：可视化的 `Jump List` 轻松实现 A->A->A 连招，支持后摇阶段的“移动打断 (Move Cancel)”。


## 🚀 如何安装

下载源码，将 `ACTSkillEditor` 文件夹直接拖入你的 `Assets` 目录，或通过github在Package Manager中安装

## 🎮 快速开始指南
1. 顶部菜单栏点击 `工具 -> 技能编辑器`。
2. 在 Project 窗口右键 `Create -> Combat -> Skill Config` 创建一个剧本。
3. 具体配置及演示见https://www.bilibili.com/video/BV1ncPwzNEQW
4. 里面附有拿ai生成的角色Test脚本，还并未完善，仅供演示

## 🤝 参与贡献
欢迎提交 Pull Request 或 Issue！如果你觉得这个工具帮到了你，请给我一个 ⭐ Star！