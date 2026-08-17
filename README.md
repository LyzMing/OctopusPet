[README.md](https://github.com/user-attachments/files/31126398/README.md)
# OctopusPet# 章鱼桌宠 🐙

用你的 `Octopus.psd` 制作的桌面宠物（WPF / C#，.NET 9）。
本文件夹是章鱼项目的全部内容，放在 `D:\TestToys` 下，与其他小项目互不干扰。

## 目录结构
```
OctopusPet\
├── OctopusPet.csproj / App.xaml / MainWindow.* / app.manifest   ← 程序源码
├── sprites\                 ← 程序内嵌的精灵图（编译时打包进 exe）
├── assets\                  ← 素材
│   ├── Octopus.psd          ← 原始画稿（改画稿就从这里改）
│   ├── layers\              ← 从 PSD 提取的透明图层（自动生成）
│   ├── sprites_full\        ← 原尺寸合成精灵 1493×1306（自动生成）
│   └── sprites_app\         ← 程序尺寸精灵 146×128（自动生成，同步给 sprites\）
├── tools\                   ← 生成工具
│   ├── psd_info.py          ← 查看 PSD 图层结构
│   ├── extract_layers.py    ← 提取图层为透明 PNG
│   └── make_sprites.py      ← 合成全部精灵图并同步到 sprites\
├── 启动桌宠.bat / launch_pet.bat   ← 一键启动（相对路径，可随意移动文件夹）
└── README.md
```
`bin\`、`obj\` 为构建产物，可随时删除重建。

## 快速使用
- **启动**：双击 `启动桌宠.bat`（或 `bin\Release\net9.0-windows\OctopusPet.exe`）
- **退出**：在章鱼上**右键 → 退出桌宠**
- **拖拽**：左键按住章鱼可拖到屏幕任意位置，松手后停留一会儿再继续走动。
  拖拽用 Win32 `GetCursorPos`/`GetWindowRect` 真实物理坐标实现，任何 DPI 缩放下都严格粘在鼠标上。
  **拖动时眼睛会切换成"激动"表情**，松手后恢复。
- **框选截图**：**右键按住章鱼拖动**，以按下点为起点框选屏幕区域（往哪个方向拖，按下点就充当对应的角：
  右下拖=左上角、右上拖=左下角、左上拖=右下角、左下拖=右上角），松手后自动把该区域截图复制到剪贴板。
  拖动时章鱼跟随鼠标、表情同正常拖动；截图内容**不包含章鱼**（截图前会自动隐藏）。
  只按一下右键（不拖动）仍是打开菜单。
- **睡觉 / 结束睡觉**：右键菜单选择。睡觉期间不能拖动也不能框选截图，完全回到常态后才能用。

## 行为说明
- **身体抖动**：3 个身体图层随机切换（常态约每 30ms 20% 概率换帧），停着/走动/拖拽时都一直抖
- **眼睛状态机（常态）**：睁眼 3~7 秒（期间随机眨眼 0.1~0.2 秒）→ 35% 概率进入闭眼"打盹"2~4 秒 → 循环
- **吐舌头**：闭眼"打盹"期间 50% 概率吐舌头，舌头 1/2 图层来回切换形成摆动，打盹结束收回
- **睡觉活动**（右键 → 睡觉）：
  1. 先切常态闭眼 → 2. 切过渡身体+过渡眼睛 → 3. 进入睡觉状态
  4. 睡觉：3 个睡觉身体以较低频率抖动，停止移动，头顶 zzz 按 z1→z2→z3 循环冒出（位置不受翻转影响）
- **结束睡觉**（睡觉时右键 → 结束睡觉）：
  停止 zzz → 刚醒眼睛一小会 → 睡觉眼睛 → 过渡身体+眼睛 → 常态身体+闭眼 → 回到常态
- **移动**：在屏幕内随机游走（速度 45~95 px/s），到达后停留 3~8 秒再走
- **朝向**：往左走时整体镜像翻转，往右走时恢复正常
- **窗口**：透明、无边框、置顶、不在任务栏显示

## 调整参数
所有可调参数在 `MainWindow.xaml.cs` 顶部常量区：
`BodySwitchChance`（常态抖动强度）、`SleepBodySwitchChance`（睡觉抖动频率）、
`WalkSpeedMin/Max`（移动速度）、`IdleMinSec/Max`（停留时长）、
`OpenEyeMinSec/Max`（睁眼时长）、`ClosedEyeMinSec/Max`（打盹时长）、`BlinkMsMin/Max`（眨眼时长）、
`SleepChance`（进入打盹概率）、`TongueChance`（吐舌头概率）、`TongueSwingMs`（舌头摆动间隔）、
`SleepStartClosedMs/SleepStartTransMs`（入睡过渡时长）、`Waking*Ms`（醒来各阶段时长）、`ZzzMs`（zzz 间隔）。
改完重新编译：
```
cd D:\TestToys\OctopusPet
dotnet build OctopusPet.csproj -c Release
```

## 如何从 PSD 重新生成精灵图
1. 更新画稿：把新 PSD 存为 `assets\Octopus.psd`（替换旧文件）
2. 提取图层：
   ```
   cd D:\TestToys\OctopusPet\tools
   python extract_layers.py        # → assets\layers\
   ```
3. 合成精灵（自动同步到 `sprites\`）：
   ```
   python make_sprites.py          # → assets\sprites_full\ + assets\sprites_app\ + sprites\
   ```
4. 重新编译：
   ```
   cd ..
   dotnet build OctopusPet.csproj -c Release
   ```

## 开机自启（可选）
想开机自动出现章鱼，创建一个"登录时"触发的计划任务指向 `启动桌宠.bat`。
