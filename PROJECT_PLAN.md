## MyTranslate 翻译小工具 — 项目规划

### 项目概述

基于 C# / .NET 10 WinForms 的桌面翻译工具，支持手动输入翻译、全局悬停翻译、全局选词翻译、区域截图 OCR 识别四种模式。常驻系统托盘，通过 CTRL+Y 全局快捷键开关翻译功能。


### 技术栈

- 框架：.NET 10 WinForms (net10.0-windows10.0.19041.0)
- 翻译 API：腾讯翻译（TencentCloudSDK.tmt）/ 百度翻译 / 阿里翻译 / Google / 智谱 / DeepSeek / 自定义 API
- OCR：Windows 内置 OCR（Windows.Media.Ocr）+ 腾讯 OCR + 百度 OCR / 自定义 OCR API
- 截图：GDI+ CopyFromScreen + DPI 缩放适配（SetProcessDPIAware）
- 窗口检测：Win32 WindowFromPoint + GetWindowRect（自动捕捉窗口）
- 全局 Hook：Win32 SetWindowsHookEx（鼠标钩子 WH_MOUSE_LL）
- 窗口文字读取：Windows UI Automation（System.Windows.Automation）
- 全局快捷键：RegisterHotKey Win32 API
- 配置存储：JSON 文件（AppData/Local/MyTranslate/）
- 语言检测：Unicode 字符范围统计


### 项目文件结构

```
MyTranslate/
│
├── Core/                          # 核心业务（与界面解耦）
│   ├── ITranslator.cs             # 翻译器统一接口
│   ├── TencentTranslator.cs       # 腾讯翻译实现（含 SDK 预热）
│   ├── BaiduTranslator.cs         # 百度翻译（待实现）
│   ├── AlibabaTranslator.cs       # 阿里翻译（待实现）
│   ├── GoogleTranslator.cs        # Google 翻译（待实现）
│   ├── ZhipuTranslator.cs         # 智谱 AI 翻译（待实现）
│   ├── DeepSeekTranslator.cs      # DeepSeek 翻译（待实现）
│   ├── CustomTranslator.cs        # 自定义 API 翻译（待实现）
│   ├── TranslationEngine.cs       # 翻译调度器（文本规范化→同语言→缓存→术语→API）
│   ├── LanguageInfo.cs            # 语言枚举与各翻译器 API 代码映射
│   ├── LanguageDetector.cs        # Unicode 范围语言检测 + 同语言判断
│   ├── TranslationResult.cs       # 翻译结果模型 + TranslationSource 枚举
│   ├── GlossaryEntry.cs           # 术语条目模型
│   ├── GlossaryManager.cs         # 术语库管理（占位符预处理/后处理）
│   └── TranslationHistoryManager.cs # 持久化多向缓存（正向/反向/链式/导出导入）
│
├── Capture/                       # 文字捕获层
│   ├── GlobalMouseHook.cs         # 全局鼠标钩子（移动/悬停/点击/释放）+ NativeMethods
│   ├── UIAutomationReader.cs      # UI Automation 读取窗口文字 + 选区 + 选区边界
│   ├── IOcrProvider.cs            # OCR 提供商统一接口
│   ├── WindowsOcrProvider.cs      # Windows 内置 OCR（Bitmap→SoftwareBitmap→OcrEngine）
│   ├── CloudOcrProvider.cs        # 腾讯云 OCR（GeneralBasicOCR，TencentCloudSDK.ocr）
│   ├── BaiduOcrProvider.cs        # 百度 OCR（待实现）
│   ├── CustomOcrProvider.cs       # 自定义 OCR API（待实现）
│   ├── OcrManager.cs             # OCR 调度（主引擎识别，支持来源追踪）
│   ├── ScreenCaptureHelper.cs     # 屏幕截图辅助（区域截图 + 鼠标周围截图 + DPI 适配）
│   └── ClipboardHelper.cs         # 剪贴板辅助方案
│
├── Overlay/                       # 浮窗显示层
│   ├── OverlayForm.cs             # 通用浮窗基类（无边框/置顶/不抢焦点/圆角/淡入淡出）+ NativeMethods
│   ├── HoverOverlay.cs            # 悬停翻译浮窗
│   ├── SelectionOverlay.cs        # 选词翻译浮窗（选区下方 + 来源标签 + 复制按钮）+ OCR 结果展示
│   ├── CaptureOverlay.cs          # 区域截图浮窗（全屏半透明遮罩 + 鼠标框选 + 多显示器）
│   └── SelectionBubble.cs         # 划词翻译气泡图标
│
├── Services/                      # 系统服务
│   ├── HotkeyManager.cs           # 全局快捷键管理（RegisterHotKey）
│   ├── TrayManager.cs             # 系统托盘管理（右键菜单/气泡通知）
│   ├── AppConfig.cs               # 配置读写（JSON 持久化，含翻译/OCR/快捷键设置）
│   ├── SelectionTranslationManager.cs # 划词翻译控制器（串联完整流程）
│   ├── HoverTranslationManager.cs # 悬停翻译控制器（串联完整流程）
│   └── CaptureTranslationManager.cs # 截图翻译控制器（框选→OCR→浮窗显示结果）
│
├── UI/                            # 用户界面
│   ├── CacheViewerForm.cs         # 缓存查看器（可编辑表格 + 导出/导入 CSV/JSON）
│   └── SettingsForm.cs            # 设置窗口（4 个页签：API设置/术语库/通用/快捷键）
│
├── Main_Translate.cs              # 主窗口逻辑（手动翻译 + 划词翻译集成 + 历史面板）
├── Main_Translate.Designer.cs     # 主窗口布局
├── Program.cs                     # 入口（SetProcessDPIAware + ApplicationConfiguration）
├── App.config
├── app_icon.ico                   # 应用图标（蓝色背景 + 白色"译"字，多尺寸 ICO）
└── MyTranslate.csproj
```


### 当前进度

#### 第一阶段：基础框架 + 手动翻译（已完成）

**核心翻译链路**
- 手动输入翻译（腾讯翻译 API + SDK 预热避免首次失败）
- 术语库管理（占位符预处理/后处理，纯术语跳过 API，CSV/JSON 批量导入导出）
- 多向持久化缓存（正向查找 → 反向查找 → 单跳链式查找，10000 条上限）
- 语言自动检测（Unicode 字符范围统计：中/日/韩/英/俄）
- 同语言跳过（源语言=目标语言时直接返回原文，不调 API 不缓存）
- Auto 语言解析（缓存存储实际检测到的语言，确保反向/链式查找正确）
- 多义词支持（同一词条多次翻译追加译文，分号分隔）
- 翻译结果缓存查看器（表格内直接编辑、新增/删除词条、CSV+JSON 导出导入）

**界面与配置**
- 主窗口（翻译器切换、语言互换、翻译/清空/复制按钮、翻译历史面板）
- 设置窗口（4 个页签：API设置、术语库、通用、快捷键）
- 系统托盘（右键菜单、最小化到托盘、气泡通知）
- 全局快捷键框架（CTRL+Y 开关翻译，CTRL+SHIFT+Y 截图翻译，CTRL+SHIFT+O 切换 OCR 方案）
- 配置持久化（JSON → AppData/Local/MyTranslate/config.json）

**缓存系统详细设计**
- 存储格式：原文 | 译文 | 选择语言 | 识别语言 | 目标语言 | 翻译器 | 时间
- 查找流程：检测输入语言 → 正向匹配 → 反向匹配（含多义词分号拆分）→ 链式匹配
- 启动时自动清理：同语言错误条目、矛盾反向条目
- 启动时自动迁移：Auto 语言解析为实际语言、SelectedSourceLanguage 字段填充


#### 第二阶段：划词翻译（已完成）

**核心流程**
- 全局鼠标钩子扩展 MouseReleased 事件（WM_LBUTTONUP 检测选词完成）
- SelectionTranslationManager 串联完整流程：鼠标释放 → 100ms 等待 → UI Automation 读取选区 → 剪贴板兜底 → 非翻译内容过滤 → 翻译引擎 → 浮窗显示
- 非翻译内容过滤（URL、邮箱、文件路径、纯数字、特殊符号片段）
- 文本规范化（trim + 合并连续空白）解决划词翻译缓存未命中问题
- 翻译来源追踪（TranslationSource 枚举：API / Cache / Glossary / SameLang）

**浮窗与交互**
- SelectionOverlay 浮窗（选区下方定位 + 屏幕边缘自动避让）
- 浮窗显示翻译来源标签（`[API]` / `[缓存]` / `[术语库]` / `[同语言]`）+ `[选词]` 标记
- 缓存命中绿色文字、API 翻译黑色文字、翻译失败红色文字
- 复制按钮 + ESC/点击外部关闭

**集成与配置**
- 划词翻译记录同步到主窗口历史面板（`[划词]` 标签）
- 可配置项：最小文本长度、剪贴板兜底、历史面板显示、浮窗显示、防抖间隔
- 翻译完成事件（TranslationCompleted）供主窗口订阅

**已知限制**
- 依赖 UI Automation 暴露选区，Electron/游戏等应用需 OCR 兜底（第四阶段已完成）
- 剪贴板兜底可能读到非选区内容（如用户之前复制的文本）


#### 第三阶段：悬停翻译（已完成）

**核心流程**
- 复用 GlobalMouseHook 的 MouseHovered 事件（鼠标停留超过 HoverDelayMs 触发）
- HoverTranslationManager 串联完整流程：悬停 → ReadTextAtPoint → 非翻译过滤 → 文本去重 → 翻译引擎 → 浮窗显示
- UIAutomationReader.ReadTextAtPoint 四种读取策略：ValuePattern → TextPattern Line/Paragraph/Word → Name 属性 → 父容器 TextPattern
- ExpandToLine 辅助方法：Word 级别降级时尝试扩展到整行（RangeFromPoint + ExpandToEnclosingUnit(Line)，ValuePattern 兜底）
- 文本去重机制：同一文本不重复翻译，鼠标移动 50px+ 自动重置
- 智能分段翻译：文本含 `[]` 或 ` - ` 时自动拆分，只翻译有意义的段落，其余原样保留
- URI 方案过滤：通用 `scheme://` 检测，过滤所有协议前缀（http://、app://、file:// 等）

**浮窗与交互**
- HoverOverlay 浮窗（鼠标旁定位 + 屏幕边缘自动避让）
- 浮窗显示翻译来源标签 + `[悬停]` 标记，缓存命中绿色文字
- 浮窗消失：8 秒自动消失 / 鼠标移出 30px / 点击外部区域
- 复制按钮 + 重翻按钮（ForceTranslateAsync 跳过缓存）
- 标题栏 AutoSize 适配长文本

**集成与配置**
- 悬停翻译记录同步到历史面板（`[悬停]` 标签 + 可配置开关）
- 可配置项：最小文本长度、历史面板显示、浮窗显示、防抖间隔
- TranslationCompleted 事件供主窗口订阅

**划词翻译同步完善**
- 划词翻译气泡图标（SelectionBubble）：选中文本后显示小图标，点击触发翻译
- 占位符分段方案：`SplitForTranslation` 将不翻译部分替换为 `__PH0__` 占位符，翻译后还原
- 划词翻译也支持复制按钮 + 重翻按钮
- 新行保留：NormalizeText 保留单换行符，合并连续换行和行内空格


### 分阶段实现计划

#### 第一阶段：基础框架 + 手动翻译（已完成）

#### 第二阶段：划词翻译（已完成）

目标：实现全局选词翻译，用户在任何应用中选中文本后自动翻译并显示浮窗。

已完成工作：
1. 扩展 GlobalMouseHook 添加 MouseReleased 事件
2. 创建 SelectionTranslationManager 串联：鼠标释放 → 读取选区 → 翻译 → 浮窗
3. 在主窗口 ToggleTranslation 中启动/停止划词翻译
4. 浮窗显示（选区下方 + 屏幕边缘避让）+ 点击外部关闭
5. 剪贴板兜底方案（UI Automation 读不到时，可配置开关）
6. 文本规范化（修复划词翻译缓存未命中）
7. 翻译来源追踪（TranslationSource 枚举 + 浮窗/历史面板显示）
8. 非翻译内容过滤（URL、邮箱、文件路径、纯数字）
9. 划词翻译同步到历史面板（`[划词]` 标签 + 可配置）
10. 可配置项（最小长度、剪贴板兜底、历史显示、浮窗显示、防抖间隔）


#### 第三阶段：悬停翻译（已完成）


#### 第四阶段：OCR 文字识别与截图翻译（已完成）

目标：为无法通过 UI Automation 读取文字的场景（图片、游戏、Electron 应用等）提供 OCR 识别能力，以及智能窗口截图功能。

**基础设施（已完成）**

1. WindowsOcrProvider 实现
   - 添加 Windows SDK 投影引用（net10.0-windows10.0.19041.0 TFM）
   - 实现 Bitmap → SoftwareBitmap 转换（InMemoryRandomAccessStream + BitmapDecoder）
   - 调用 Windows.Media.Ocr.OcrEngine 识别文字
   - 优先使用中文语言包（zh-Hans/zh-Hant），回退到首个可用语言

2. CloudOcrProvider 实现（腾讯 OCR）
   - 使用 TencentCloudSDK.ocr NuGet 包
   - 调用 GeneralBasicOCR 接口
   - Bitmap → Base64 编码
   - 解析 TextDetections 拼接文字
   - 支持独立 SecretId/SecretKey 配置

3. OcrManager 完善
   - 主引擎识别 + 来源追踪（RecognizeWithSourceAsync 返回 providerName）
   - 根据配置切换主引擎（内置/API），不自动降级

4. DPI 适配
   - Program.cs 启动时调用 SetProcessDPIAware
   - 确保截图坐标正确

**截图功能（已完成）**

1. CaptureOverlay 实现
   - 全屏半透明遮罩（30% 黑色，覆盖所有显示器）
   - 悬停自动检测窗口（停止移动 200ms 后检测，避免闪烁）
   - 窗口蓝色描边 + 四角标记 + 尺寸显示
   - 点击捕获当前高亮窗口
   - 拖拽框选任意矩形区域
   - 白色虚线边框 + 尺寸显示（W × H）
   - 临时隐藏遮罩检测下方窗口（WindowFromPoint）
   - GetAsyncKeyState 轮询检测鼠标/ESC
   - TaskCompletionSource 异步返回选区

2. CaptureTranslationManager 实现
   - 快捷键 → 框选 → 截图 → OCR → 浮窗显示结果
   - OCR 结果填入主窗口输入框
   - 浮窗标题：[OCR][内置] 或 [OCR][API]
   - 内置识别绿色文字，API 识别黑色文字

3. 全局快捷键
   - Ctrl+Shift+Y：区域截图翻译
   - Ctrl+Shift+O：切换 OCR 方案（内置 ↔ API）

**设置界面（已完成）**

1. 设置窗口重构
   - 「翻译设置」→「API 设置」
   - 「默认翻译器」→「供应商」
   - 下拉选项去掉"翻译"二字
   - 新增「翻译 API」粗体标题行
   - OCR 方案移到 API 设置页签（内置/API 单选项 + SecretId/SecretKey）
   - 测试连接按钮仅在 API 设置页签显示

2. 配置项扩展（AppConfig）
   - OcrProvider：WindowsBuiltIn / TencentOcr
   - CloudOcrSecretId / CloudOcrApiKey：云端 OCR 密钥
   - CaptureHotkey：区域截图快捷键（默认 Ctrl+Shift+Y）
   - ToggleOcrHotkey：切换 OCR 方案快捷键（默认 Ctrl+Shift+O）

**已知限制**
- Windows OCR 对小字/特殊字体识别精度有限
- 腾讯 OCR 需要网络连接和有效密钥


#### 第五阶段：体验打磨

目标：优化细节，提升使用体验。

具体工作：
1. 浮窗动画（淡入淡出）
2. 翻译请求限流（200ms 最小间隔）
3. 开机自启动
4. 缓存淘汰策略优化（按时间/收藏标记）
5. 多义词浮窗展示优化（标签样式替代分号）
6. 截图翻译自动翻译选项（OCR 结果自动调用翻译 API）


#### 第六阶段：多供应商扩展

目标：支持多个翻译和 OCR 供应商，以及自定义 API 接口。

**翻译供应商扩展**

1. 腾讯翻译（已实现）
   - TencentCloudSDK.tmt
   - SecretId + SecretKey 认证

2. 百度翻译（待实现）
   - HTTP API 调用
   - AppId + SecretKey 认证
   - 签名：md5(appid+q+salt+key)

3. 阿里翻译（待实现）
   - HTTP API 调用
   - AccessKeyId + AccessKeySecret 认证
   - 签名：HMAC-SHA1

4. Google 翻译（待实现）
   - 免费 API（无需密钥，有频率限制）
   - 或 Google Cloud Translation API（需 API Key）

5. 智谱 AI（待实现）
   - ChatGLM 大模型翻译
   - API Key 认证

6. DeepSeek（待实现）
   - DeepSeek 大模型翻译
   - API Key 认证

7. 小米翻译（待确认）
   - 需确认是否有公开翻译 API
   - 如有，按相同接口模式接入

8. 自定义 API（待实现）
   - 用户自定义 API 地址
   - 支持 GET/POST 请求
   - 可配置请求头、请求体模板
   - 可配置响应解析路径（JSON Path）

**OCR 供应商扩展**

1. Windows 内置 OCR（已实现）
2. 腾讯 OCR（已实现）
3. 百度 OCR（待实现）
   - HTTP API 调用
   - AppId + API Key 认证
4. 阿里 OCR（待实现）
5. Google Vision OCR（待实现）
6. 自定义 OCR API（待实现）

**架构设计**

1. 翻译器统一接口（ITranslator）
   - TranslateAsync(text, sourceLang, targetLang) → TranslationResult
   - 各供应商实现此接口

2. OCR 提供商统一接口（IOcrProvider）
   - RecognizeAsync(bitmap) → string
   - 各供应商实现此接口

3. 供应商注册机制
   - TranslationEngine.RegisterTranslator(ITranslator)
   - OcrManager.RegisterProvider(IOcrProvider)
   - 启动时根据配置注册所有已配置密钥的供应商

4. 配置扩展（AppConfig）
   - 每个供应商独立的密钥配置
   - 当前选中的翻译器 ID
   - 当前选中的 OCR 提供商

**实施顺序**

| 步骤 | 内容 | 涉及文件 |
|------|------|----------|
| 1 | 定义供应商注册机制 | TranslationEngine.cs, OcrManager.cs |
| 2 | 实现百度翻译 | BaiduTranslator.cs |
| 3 | 实现阿里翻译 | AlibabaTranslator.cs |
| 4 | 实现 Google 翻译 | GoogleTranslator.cs（新建） |
| 5 | 实现智谱 AI 翻译 | ZhipuTranslator.cs（新建） |
| 6 | 实现 DeepSeek 翻译 | DeepSeekTranslator.cs（新建） |
| 7 | 实现自定义 API 翻译 | CustomTranslator.cs（新建） |
| 8 | 实现百度 OCR | BaiduOcrProvider.cs（新建） |
| 9 | 实现自定义 OCR API | CustomOcrProvider.cs（新建） |
| 10 | 设置界面扩展（多供应商密钥配置） | SettingsForm.cs, AppConfig.cs |
| 11 | 测试各供应商 | 集成测试 |


### 关键技术要点

1. **全局鼠标钩子**：WH_MOUSE_LL 回调必须快速返回，耗时操作必须异步。
2. **浮窗不抢焦点**：WS_EX_NOACTIVATE + WS_EX_TOOLWINDOW 扩展样式。
3. **UI Automation 限制**：Electron 应用、游戏等可能不暴露接口，需 OCR 兜底。
4. **缓存键设计**：`原文|检测语言|目标语言`，确保反向/链式查找的正确性。
5. **术语库占位符**：预替换为 `{{G0}}` 等占位符，翻译后还原，全术语文本跳过 API。
6. **浮窗圆角**：GDI+ GraphicsPath 绘制，8px 圆角。
7. **翻译来源追踪**：TranslationSource 枚举贯穿所有返回路径（同语言/缓存/术语库/API）。
8. **文本规范化**：TranslateAsync 入口处 trim + 合并空白，消除划词选区的不可见字符。
9. **非翻译内容过滤**：URL/邮箱/文件路径/纯数字在划词翻译入口自动跳过。
10. **OCR 降级机制**：悬停/划词翻译中 UI Automation 读取失败时，自动截图 OCR 识别文字作为兜底。
11. **DPI 适配**：启动时调用 SetProcessDPIAware，确保截图坐标正确。
12. **Windows OCR 语言包**：优先使用中文语言包（zh-Hans/zh-Hant），回退到首个可用语言。
13. **窗口自动捕捉**：截图时临时隐藏遮罩检测下方窗口，停止移动 200ms 后触发检测避免闪烁。
14. **供应商扩展架构**：ITranslator / IOcrProvider 统一接口，支持多供应商注册和切换。


### 待细化与技术债

**代码结构**
- NativeMethods 重复定义：Capture/GlobalMouseHook.cs 和 Overlay/OverlayForm.cs 各有一份 NativeMethods 类（不同命名空间可编译），建议抽取到共享的 Interop/NativeMethods.cs

**性能优化**
- FindDirect / FindReverse / FindChain 遍历全部缓存条目（O(n)），缓存量大时可能变慢；可考虑建立 `原文→key` 的正向字典索引加速正向查找
- 反向索引只存完整 TranslatedText，多义词（分号分隔）的精确反向匹配需全遍历；可拆分为单个词条索引

**历史面板**
- 历史面板仅保留在内存中（100 条上限），重启后清空

**功能增强**
- 划词翻译的源/目标语言使用全局默认值，无法针对划词单独设置
- 多义词在浮窗中以分号显示，体验不佳（可用标签/换行展示）
- 截图翻译的 OCR 结果仅填入输入框，未自动翻译（可配置自动翻译选项）
- 悬停/划词翻译的 OCR 降级功能（当 UI Automation 读不到时自动截图 OCR）
