# tools 目录

构建时需要把 **Intel PresentMon** 放这里，编译时会被嵌入到 Vitals.exe。

## 下载

1. 打开 https://github.com/GameTechDev/PresentMon/releases
2. 找到 **v2.6.0**（或更新版本）
3. 下载 `PresentMon-2.6.0-x64.exe`
4. 重命名为 `PresentMon.exe`，放到本目录：

```
tools/PresentMon.exe
```

## 校验（可选）

v2.6.0 的 SHA256：

```
B2A706BC6AD475749E3B7E3409263AA1E6906D45BDCF993F6DBC0F660188F1AF
```

```powershell
Get-FileHash .\PresentMon.exe -Algorithm SHA256
```

## 为什么不用仓库直接带

PresentMon 是 Intel 的独立产品（MIT 许可），体积约 1 MB。
把它放进仓库会让每次更新都带上二进制，所以改为构建时自行下载。
`tools/*.exe` 已在 `.gitignore` 中排除。
