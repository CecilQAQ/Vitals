# 第三方组件声明

本项目使用了以下第三方组件，版权归各自作者所有。

---

## 1. Intel PresentMon

- 项目地址：https://github.com/GameTechDev/PresentMon
- 许可证：MIT
- 用途：通过 ETW 采集 DXGI 呈现事件，用于计算帧率与 1% low。
- 分发方式：**不随仓库提供**。构建前请自行下载 `PresentMon-2.6.0-x64.exe`
  放到 `tools/` 目录下，编译时会被嵌入到 Vitals.exe 中。
  详见 `tools/README.md`。

## 2. PawnIO / IntelMSR 模块

- 项目地址：https://github.com/namazso/PawnIO
- 许可证：MPL-2.0（PawnIO 及其模块）
- 用途：通过已签名的内核驱动读取 CPU 的 MSR 寄存器，得到温度、功耗与频率。
- 分发方式：`src/IntelMSR.bin`（约 4 KB）为 PawnIO 提供的 MSR 访问模块，
  随源码一同提供以便直接构建。驱动本体不随本项目分发，需用户自行安装。

## 3. NVIDIA NVML

- 随 NVIDIA 显卡驱动安装（`nvml.dll`），本项目通过 P/Invoke 调用。
- 不随仓库分发。

---

## 商标声明

HP、OMEN、Victus、NVIDIA 等名称与商标归各自所有者所有。
本项目与惠普、NVIDIA 均无隶属或背书关系。
