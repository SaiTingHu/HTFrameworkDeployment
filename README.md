# Unity HTFramework Deployment

HTFramework的Deployment模块，必须依赖于HTFramework主框架使用。

## 环境

- Unity版本：2022.3.34。

- .NET API版本：.NET Framework。

- [HTFramework(Latest version)](https://github.com/SaiTingHu/HTFramework)。

## 模块简介

- Deployment - 轻量级资源部署管线，整合资源打包、资源版本构建、资源版本更新为一体，快速实现资源部署和交付游戏。

## 使用方法

- 1.拉取框架到项目中的Assets文件夹下（Assets/HTFramework/），或以添加子模块的形式。

- 2.在入口场景的层级（Hierarchy）视图点击右键，选择 HTFramework -> Main Environment（创建框架主环境），并删除入口场景其他的东西（除了框架的主要模块，其他任何东西都应该是动态加载的）。

- 3.拉取本模块到项目中的Assets文件夹下（Assets/HTFrameworkDeployment/），或以添加子模块的形式。

- 4.参阅各个模块的帮助文档，开始开发。
