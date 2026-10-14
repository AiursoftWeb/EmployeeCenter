# 员工中心 (EmployeeCenter) 使用说明教材

欢迎使用员工中心（EmployeeCenter）系统！本手册旨在指导您深入了解系统的核心功能、业务流程及操作细节，并提供每个功能的快速访问路径。

---

## 目录
1. [职业 (Professional) - 职业生涯与日常协作](#1-职业-professional)
2. [个人 (Personal) - 个人权益与信息维护](#2-个人-personal)
3. [管理 (Management) - 行政运维与组织管理](#3-管理-management)
4. [核心实体与数据模型关系 (Data Models & Relationships)](#4-核心实体与数据模型关系-data-models--relationships)
5. [高级智能化功能 (Advanced Intelligent Features)](#5-高级智能化功能-advanced-intelligent-features)
6. [业务资质水印功能开发记录](#6-业务资质水印功能开发记录)

---

## 1. 职业 (Professional)

此板块专注于您的职业生命周期、日常任务汇报及团队协作。

### 1.1 入职 (Onboarding)
*   **[入职任务](/Dashboard/Index)**: 
    *   **分阶段引导**: 系统采用阶段式引导（SmartWizard），将入职流程分为“合同签署”、“办公环境准备”、“技术授权”等多个阶段。
    *   **严格约束**: 您必须按顺序点击“开始任务”并“标记完成”。只有当前阶段的所有必填任务全部完成后，系统才会自动解锁下一个阶段的访问权限。
    *   **自动日志**: 每一个任务的开始与完成时间都会被记录，作为入职进度的考核依据。

### 1.2 蓝图 (Blueprints)
*   **[查看蓝图](/Blueprint/Index)**: 
    *   **知识沉淀**: 浏览公司内部的技术架构图、开发规范、产品说明书等核心文档。
    *   **分类检索**: 支持按文件夹层级浏览，快速定位所需的专业文档。
*   **[管理蓝图](/Blueprint/Manage)**: 
    *   **内容治理**: 管理员可在此创建、重命名、移动或删除文档。支持批量上传和目录结构调整。

### 1.3 开发 (Development)
*   **[项目](/Projects/Index)**: 实时跟踪全公司研发项目的状态。点击项目可查看项目负责人、代码仓库链接及关联的服务。
*   **[服务](/Services/Index)**: 记录所有正在运行的微服务或中间件信息。
*   **[服务器](/Servers/Index)**: 详细列出物理机或云主机的 IP、配置（CPU/RAM）、操作系统及归属。
*   **[周报](/WeeklyReport/Index)**: 
    *   **智能编辑**: 支持 Markdown 语法。提供“记事本”功能，可随时记录零散工作，正式提交时一键导入。
    *   **动态滚动**: 采用无限滚动加载技术，可回溯查看团队往期所有周报。
    *   **互动点评**: 支持在周报下方发表评论，主管可针对特定工作内容进行反馈。
*   **[员工信号](/Feedback/Index)**: 
    *   **即时调研**: 系统会不定期发布问卷，收集员工对公司环境或特定决策的真实看法。

### 1.4 项目需求 (Project Requirements)
*   **[需求提报](/Requirements/My)**: 支持提交业务需求、Bug 修复或功能改进申请。
*   **[工作流审批](/Requirements/Manage)**: 需求需经过负责人审核、立项评估等环节。审批状态会实时同步，并记录详细的操作历史。

### 1.5 汇报线 (Report Line)
*   **[我的汇报线](/ReportLine/Index)**: 
    *   **可视化地图**: 基于 Mermaid.js 实现的交互式组织架构图。
    *   **跨节点导航**: 点击图中的同事头像，可直接跳转到其个人汇报线页面，查看其在公司中的职能位置。
*   **[晋升记录](/PromotionHistory/Index)**: 详细记录您入职以来的职位级别（Rank）及职务（Title）的每一次变迁。

### 1.6 请假管理 (Leave Management)
*   **[我的休假余额](/Leave/Index)**: 
    *   **动态计算**: 自动显示年假、带薪病假、调休天数。系统会根据入职时间自动结算并结转假期。
*   **[团队日历](/Leave/TeamCalendar)**: 以日历视图展示部门成员的休假计划，有效规避项目关键节点的用人风险。
*   **[审批中心](/Leave/Incoming)**: 主管可集中处理待办请假单。支持查看员工的请假历史及冲突检测（如：同一时间段内该团队已请假人数）。

### 1.7 资源 (Resources)
*   **[公司实体信息](/CompanyEntity/Index)**: 包含统一社会信用代码、开票信息、经营范围等，方便商务洽谈时快速复制。
*   **[共享密码](/Passwords/Index)**: 采用分权限共享机制。对于敏感密码，需申请访问权限后方可查看。

---

## 2. 个人 (Personal)

管理与您个人利益、薪资及 IT 物资相关的信息。

### 2.1 薪资 (Payroll)
*   **[我的工资单](/Payroll/Index)**: 
    *   **明细导出**: 支持按年份导出 CSV 格式的薪资明细，方便个人个税申报。
    *   **构成透明**: 清晰列出基本工资、奖金、五险一金缴纳及个税抵扣。

### 2.2 打印证书 (Print Certificate)
*   **[自助证明](/Certificate/Employment)**: 系统根据实时员工数据，自动填充入职日期、岗位、收入等信息，一键生成带有公司合法电子签章的 PDF 文件，具备正式证明效力。

### 2.3 设置 (Settings)
*   **[资料设置](/Manage/Index)**: 维护您的银行卡、社保卡及紧急联系人。
*   **[我的资产](/MyAssets/Index)**: 记录您领用的 IT 硬件（如笔记本电脑序列号）。如果资产出现故障，可在此快速发起维修申请。

---

## 3. 管理 (Management)

供行政、HR、财务、法务及 IT 管理员使用的专业运维模块。

### 3.1 资产与财务
*   **[IT 资产库](/Assets/Index)**: 包含资产的采购日期、维保期限及当前领用人。支持二维码/条码管理。
*   **[无形资产管理](/IntangibleAssets/Index)**: 监控域名、SSL 证书的有效期，并在到期前 30 天向管理员推送通知。
*   **[管理账本](/Ledger/Index)**: 记录公司的收支流向，支持多账户（如：公账、支付宝、备用金）管理。

### 3.2 组织与法律
*   **[管理合同](/ManageContract/Index)**: 
    *   **智能 OCR**: 上传合同扫描件后，系统可自动识别合同金额、到期日及合同相对人（需配置 OCR 插件）。
    *   **收支统计**: 将合同与财务模块联动，统计合同的实际到账金额。
*   **[入职流程配置](/ManageOnboarding/Index)**: 管理员可自由定义入职任务的标题、描述、所属阶段及权重。

### 3.3 系统底层 (System)
*   **[全局参数](/GlobalSettings/Index)**: 配置每年固定年假天数、各国家节假日调整、SMTP 邮件服务器等核心参数。
*   **[后台任务监控](/Jobs/Index)**: 查看定时任务（如假期结算、邮件重试）的运行状态。

---

## 4. 核心实体与数据模型关系 (Data Models & Relationships)

本系统基于 `IdentityDbContext` 构建，以“用户 (User)”为中心，构建了涵盖人力资源、资产管理、财务法律、技术研发等多个领域的复杂关系网络。

### 4.1 核心主轴：用户 (User)
*   **User (用户)**: 系统的核心，继承自 `IdentityUser`。
    *   **关联关系**:
        *   **汇报关系**: `User` 实体中包含 `ManagementId`，指向其直接上级，形成树状的组织架构（汇报线）。
        *   **薪资与福利**: 每个用户关联多个 `Payroll` (工资单) 和一个 `LeaveBalance` (休假余额)。
        *   **职场足迹**: 关联 `PromotionHistory` (晋升记录) 和 `WeeklyReport` (周报)。
        *   **行政关联**: 用户领用 `Asset` (IT 资产)，提交 `Reimbursement` (报销)，完成 `OnboardingTaskLog` (入职任务记录)。

### 4.2 行政与资产管理 (Administration & Assets)
*   **Asset (资产)**: 代表笔记本、显示器等硬件。
    *   **分类体系**: `Asset` 属于某个 `AssetModel` (型号)，而型号归属于 `AssetCategory` (分类)。
    *   **时空属性**: 记录所属的 `Location` (存放地点) 和采购自哪个 `Vendor` (供应商)。
    *   **历史追踪**: 通过 `AssetHistory` 记录资产的领用、归还及维修历史，与 `User` 关联。
*   **IntangibleAsset (无形资产)**: 管理域名、证书、软著等，与 `CompanyEntity` (公司实体) 关联。

### 4.3 财务与法律 (Finance & Legal)
*   **CompanyEntity (公司实体)**: 法律实体的核心，所有 `Contract` (合同)、`FinanceAccount` (银行/财务账户) 都必须归属于特定的公司实体。
*   **Contract (合同)**: 
    *   **结构化存储**: 合同存储在 `ContractFolder` (合同文件夹) 中，形成目录结构。
    *   **智能化**: 每个合同可对应一个 `ContractOcrResult` (OCR 识别结果)，用于自动提取合同关键信息。
*   **Transaction (交易)**: 所有的财务流水，记录在 `FinanceAccount` 中。`Reimbursement` (报销单) 审核通过后会转化为特定的交易记录。

### 4.4 研发与运维 (DevOps)
*   **Project (项目)**: 研发管理的基础。
*   **Service (服务)**: 运行在 `Server` (服务器) 上的逻辑单元。
*   **Server (服务器)**: 属于特定的 `Provider` (服务商，如阿里云、AWS)，并关联 `DnsProvider`。
*   **SSH Key & Password**: 提供研发环境的访问凭证管理。`Password` 通过 `PasswordShare` 在用户间安全共享。

### 4.5 员工信号与反馈 (Signals & Feedback)
*   **SignalQuestionnaire (信号调查问卷)**: 包含多个 `SignalQuestion` (问题)。
*   **SignalResponse (答卷)**: 用户提交的完整问卷回复。
*   **SignalQuestionResponse (题目回答)**: 针对具体问题的答案。

### 4.6 知识库 (Knowledge Base)
*   **Blueprint (蓝图)**: 文档实体的核心。
*   **BlueprintFolder (蓝图目录)**: 支持无限极递归目录结构，用于组织和分类蓝图文档。

---

## 5. 高级智能化功能 (Advanced Intelligent Features)

员工中心集成了现代化的 AI 与 OCR 技术，旨在减少重复性劳动，并为员工提供智能化的信息检索服务。

### 5.1 自动化合同 OCR (Automated Contract OCR)
系统通过集成自研或第三方 OCR 引擎，实现了对 PDF 合同的深度解析。
*   **触发机制**: 当您在 [管理合同](/ManageContract/Index) 中上传一份 PDF 扫描件时，系统会通过后台任务自动触发 OCR 识别。
*   **识别流程**: 引擎按页码解析文本内容及其坐标，系统将其转换为结构化数据并存储为纯文本，使得扫描版 PDF 变为可全文检索的数字化文档。
*   **核心价值**: 极大提高了法务和财务查找历史合同特定条款（如违约责任、金额明细）的效率。

### 5.2 AI 助手：公司信息专家 (AI Assistant)
*   **[公司信息咨询](/AiAssistant/Index)**: 
    *   **多语言交互**: AI 助手会自动识别并以您当前的系统语言（如中文、英文）进行专业回复。
    *   **智能问答**: 它能够实时回答关于公司政策、入职流程或特定项目进度的问题。
    *   **富文本展示**: 助手支持返回带有 Markdown 排版的内容。系统会自动将这些内容转换为经过安全清洗的 HTML，为您呈现清晰的列表、加粗强调及表格，显著提升阅读体验。
*   **后台管理**: 管理员可以在 [全局参数](/GlobalSettings/Index) 中自定义 `AiAssistantSystemPrompt`，以微调 AI 的回复风格、专业深度或知识范围。

### 5.3 数据安全与隐私
*   **权限准入**: 仅拥有 `CanChatWithAi` 权限的用户方可使用 AI 助手。
*   **隔离处理**: OCR 解析在受控的后台任务中运行，所有敏感数据均严格遵循系统的权限控制体系。

---
*本文档由 Gemini 自动生成并维护。最后更新日期: 2026-04-17*

---

## 6. 业务资质水印功能开发记录

> 本节整理日期：2026-10-14。第 1–5 节原使用手册内容保留；本节单独记录水印功能的迁移、评审修订及验收情况。
> 不包含真实证照、检测图片、测试数据库、私钥、口令或安全密钥 PIN。
> 原 PR 分支：`Add_watermark_function`。6.1–6.12 保留原实现与评审历史；2026-10-14 的 ZIP 分发修订以 6.13 为准，本地工作分支为 `refactor/qualification-storage`。

### 6.1 背景、来源与迁移范围

2026-10-12 在旧项目 `EmployeeCenter2/employeecenter-master` 开发了业务资质双层水印功能。随后发现旧代码并非最新，因此于 2026-10-13 将该功能移入当前 `3/employeecenter` 项目。

迁移依据为旧项目的 `docs/业务资质水印功能开发记录.md`，仅迁移水印相关实体、服务、页面、资源、测试及必要集成点，没有复制旧项目的其他业务功能、数据库或真实证照。

目标项目初始基线为 `f4e2a999`。迁移保留了新版会议纪要并发控制及新版 MySQL 依赖；SQLite、MySQL 迁移均在新版模型上重新生成，没有套用旧项目的完整数据库快照。

最初迁移版本已签名提交并发布：

- 提交：`e2d407db`，说明为“添加业务资质双层水印与下载追踪”。
- 合并请求：`!489`，源分支 `Add_watermark_function`，目标分支 `master`。
- **该提交是初始迁移版本，不是包含后续框架修订和独立语言选项的最终版本。**

### 6.2 业务目标与边界

员工使用营业执照等业务资质时，必须填写具体用途和接收单位，由服务端读取登录账号姓名，生成带可见用途水印和隐形追踪编号的 PNG 副本，并持久化生成记录。

| 项目 | 当前范围 |
|---|---|
| 功能名称 | 业务资质 |
| 审批 | 首版不需要审批 |
| 公司归属 | 每个资质版本关联一个公司主体 |
| 添加原件 | PNG/JPEG；不支持 PDF |
| 输出 | 合成可见水印和隐形水印的 PNG |
| 员工姓名 | 服务端优先使用 `LegalName`，否则使用 `DisplayName` |
| 原件保护 | 私有保存，不提供通用原件下载入口 |
| 记录 | 生成成功即留痕，不等到点击下载 |
| 版本 | 每次添加为独立版本，不覆盖原件 |
| 管理启停 | 可停止使用并重新启用，历史记录保留 |
| 检测 | 本地处理图片，提取编号并关联生成记录 |

水印定位为用途约束和来源追溯工具，**不是 DRM，也不是无法去除或无法伪造的防伪系统**。检测匹配不能单独证明申请人实施了滥用，检测不到也不能证明图片从未带水印。

### 6.3 独立员工与管理流程

员工入口：**职业 → 资源 → 业务资质**，页面地址 `/Qualifications`。

员工流程：

1. 选择已启用的资质。
2. 填写具体业务用途和接收单位。
3. 选择本次水印语言。
4. 生成并预览副本。
5. 确认内容后下载，或在“我的下载记录”中查看既有记录。

管理员使用员工入口时也只看到员工功能，其记录列表始终只列当前登录账号本人记录。

管理入口：**管理 → 法律 → 管理业务资质**，页面地址 `/ManageQualifications`。

管理页面提供添加资质、停止使用／启用、全部下载记录、只读追踪详情及隐形水印检测；不混入员工生成入口，不提供“员工视图／管理视图”切换。

| 地址 | 功能 |
|---|---|
| `/Qualifications/Apply/{id}` | 填写用途、接收单位及水印语言 |
| `/Qualifications/Preview/{id}` | 生成记录及副本预览 |
| `/Qualifications/Image/{id}` | 已生成副本图片 |
| POST `/Qualifications/Download/{id}` | 显式下载并更新次数 |
| `/Qualifications/Records` | 本人生成／下载记录 |
| `/ManageQualifications/Create` | 添加资质版本 |
| POST `/ManageQualifications/Retire/{id}` | 停止使用 |
| POST `/ManageQualifications/Enable/{id}` | 重新启用 |
| `/ManageQualifications/Records` | 所有人员记录 |
| `/ManageQualifications/Record/{id}` | 只读追踪详情 |
| `/ManageQualifications/Detect` | 上传副本检测隐形编号 |

副本图片及下载保留原访问地址，但框架修订后实际交付动作集中在 `FilesController`。

停用会阻止新生成和既有副本的图片／下载；历史预览保留文字但不显示图片或下载按钮。重新启用后恢复使用。已经下载到其他设备的文件不能撤回。

### 6.4 2026-10-13 评审反馈与框架修订

初始迁移虽然通过功能测试，但在 MR `!489` 中被指出未充分采用项目的文件管理基础设施。该版本不得直接合并。

本轮修订依据用户提供的 Aiursoft Template 规范：

- [视图开发实践](https://template-docs.aiursoft.com/Features/Views.html)。
- [文件存储与上传](https://template-docs.aiursoft.com/Features/SaveFile.html)。
- 用户同时提供的模型校验、UTC 时间渲染、实体建模、全局设置、Canon 后台任务及模板升级说明。

本机网页抓取受网络策略限制，因此采用用户贴出的规范全文进行核对，没有执行模板升级脚本，也没有重建整个项目或删除既有迁移。

| 评审问题 | 已实施的修订 |
|---|---|
| 业务控制器出现中文字符串 | 改为英文代码和资源键，中文移入 `.resx` |
| 表单直接使用传统文件输入 | 添加和检测页面改用 `vc:file-upload` |
| ViewModel 接收 `IFormFile` | 改为带必填、长度及目录正则约束的逻辑路径字符串 |
| 业务控制器读上传流、检查文件大小等 | 上传集中到 `FilesController`；图像业务处理由文件服务调用水印服务 |
| 副本交付分散在业务控制器 | 移入 `FilesController`，保留账号归属、管理权限及资质启用校验 |
| 手写前端 `required`、长度及字段名称 | 采用 `asp-for`、模型注解和 `asp-validation-for` |
| 页面直接输出 UTC 时间文本 | 改用 `data-utc-time` 和 `ToHtmlDateTime()`，由浏览器显示本地时间 |
| 多个 ViewModel 在一个源码文件 | 拆分为各自文件，保持布局基类及构造函数中的 `PageTitle` |
| 新实体未显式满足建模约定 | 补齐 `[Key]`、主键 `init`、必填外键、显式导航标注及不可编辑字段 `init` |
| 可见水印固定为中文 | 引入 `IStringLocalizer`，支持英文、简体及繁体资源 |
| README 新增大段功能说明 | 删除该段，README 恢复迁移前内容；开发记录按用户要求保存在本手册 |

未放宽 `.editorconfig`、`lint.sh` 或流水线规则。上传页面对外部 UiStack 布局的 Razor section 解析警告，沿用项目已有的精确单次抑制方式。

### 6.5 文件管理、私有目录与临时检测文件

业务页面不直接接收上传文件流。两阶段流程为：

```text
vc:file-upload
  → FilesController 校验上传令牌、权限、文件大小、扩展名及图像
  → StorageService 保存到私有目录
  → 返回逻辑路径，不返回资质文件的通用下载链接
  → 业务表单提交逻辑路径
  → 服务验证目录归属、文件存在性及图像完整性
  → 持久化资质或执行检测
```

私有逻辑目录：

```text
qualifications/
  uploads/{账号ID的SHA-256}/       原件添加前的临时上传
  detections/{账号ID的SHA-256}/    待检测图片临时上传
  originals/                     已保存的资质原件
  copies/                        已生成水印PNG
```

所有业务路径通过 `StorageService` 的物理路径解析及限定子目录解析处理，数据库只存逻辑路径。临时上传目录与账号绑定；资质上传还要求资质管理权限，不能直接向原件或副本目录上传。

公开和带令牌的通用下载路由均拒绝根目录 `qualifications`。资质原件、临时图和副本不能通过通用 token 绕过业务规则。上传组件在此模块关闭既有文件预览，避免生成原件下载链接；其他模块保留原默认行为。

原件添加时检查图像并计算 SHA-256，生成前再次检查原件哈希。数据库保存失败时清理本次已保存的原件或副本。无论字体缺失、图像损坏或处理失败，都不能回退交付无水印原件。

**检测文件处理方式在评审修订后发生变化：**

- 初始实现只在请求内存中检测；改用框架上传组件后，需要先写入私有临时目录。
- 用户已明确同意这一变更。
- 完成检测后删除临时图片，不加入资质版本或生成记录。
- `QualificationUploadCleanupJob` 使用 Canon 注册，每 10 分钟运行一次，首次延迟 1 分钟，清理超过 1 小时的原件／检测临时上传。
- 超过 1 小时是清理条件，不保证恰好在第 60 分钟删除；正常情况下还需等待下一次任务调度，应用停机或删除失败可能延后。
- 该清理不删除已持久化的原件、生成副本或历史追踪记录。

文件上传失败后，框架 `uploader.js` 的回调绑定也已修正，能够正确重置进度及路径字段，而不是发生 JavaScript 异常。

### 6.6 2026-10-14 独立水印语言选择

修订后的第一版水印随当前界面语言生成。用户反馈中文界面下仍生成中文固定文案，希望在生成页面独立选择水印语言。

确认并实现：在“接收单位”下方添加“水印语言”下拉框。

| 选项 | 提交值 | 行为 |
|---|---|---|
| 跟随界面语言（默认） | `auto` | 使用当前界面语言的水印资源 |
| 简体中文 | `zh-CN` | 简体固定文案 |
| 繁體中文（香港） | `zh-HK` | 香港繁体固定文案 |
| English | `en-US` | 英文固定文案 |

选择只影响本次新生成副本的固定水印文案，不改变系统界面、账号资料或历史副本。服务仅在同步资源查找期间临时切换 `CurrentUICulture`，并在 `finally` 中恢复原文化。模型和服务均拒绝不支持的选项，非法请求不产生生成记录。

英文固定文案示例：

```text
For Supplier onboarding only / No other use
Recipient: Test recipient
Applicant: Account name
UTC: 2026-10-14 01:23:00 UTC
Trace ID: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
```

**业务用途、接收单位和账号姓名不会自动翻译。** 如需英文副本，应选择 English 并用英文填写用途和接收单位；姓名仍来自账号。原件自身包含的中文也不会被翻译或移除。

既有副本已经合成为 PNG，改变语言选项不会修改它们，需要重新生成。该语言选项没有新增数据库列；语言效果保存在生成图片中，数据库继续保存原有快照、哈希和追踪编号。

### 6.7 水印协议、图片限制与时间策略

可见水印直接绘制在图片像素中，不使用 CSS 叠层；包含用途、接收单位、账号姓名、UTC 时间及追踪编号。按比例缩放并纠正方向，透明底铺白，输出 PNG 去掉源图像元数据。

隐形载荷保持原版 `QWM1` 协议：

- 总长 29 字节：4 字节魔数、1 字节版本、16 字节 .NET Guid、8 字节完整性字段。
- 完整性字段取前 21 字节 SHA-256 的前 8 字节。
- 8×8 像素块 DCT 系数差嵌入，多次重复并多数投票提取。
- 隐形载荷不包含姓名、用途、接收单位等文本。
- 语言修订未改变载荷、Guid 字节布局或检测协议。

输入与输出限制：

- PNG/JPEG 文件最多 20 MiB；PDF 首版不支持。
- 原图宽高均至少 512 像素，解码像素最多 25,000,000。
- 输出长边最多 2048 像素，缩放后的短边仍须至少 512。
- 用途 4–120 字符，接收单位 2–100 字符；禁止控制字符及不可见格式控制字符。
- 具有完整 PNG/JPEG 容器、主体及复杂度校验，支持受限制的 baseline/progressive JPEG。
- 下载 PNG、同尺寸 JPEG 85–100 质量重编码已测试；不承诺缩放、裁剪、截图重采样、旋转、打印扫描后的可靠检测。

数据库生成时间使用 `DateTime.UtcNow`；网页记录使用 UTC 占位交由浏览器转换时区。**图片中的时间仍明确标注 UTC**，以便不同接收方核对同一份固定副本，不能用网页时区转换机制修改图片中的文字。

字体使用本机字体解析，要求覆盖完整水印文本；Docker 安装 `fonts-noto-cjk`。非容器环境需准备可用字体，可用 `Qualifications:WatermarkFontPath` 或环境变量 `Qualifications__WatermarkFontPath` 指定本地字体。字体缺字时生成失败，不返回原件。

### 6.8 数据、权限及主要源码

数据实体：

- `BusinessQualification`：公司归属、名称、版本、私有原件路径和 SHA-256、创建账号快照、UTC 添加时间及启用状态。
- `QualificationDownload`：Guid 追踪编号、资质版本外键、人员／公司／版本快照、用途、接收单位、私有生成路径和 SHA-256、UTC 生成时间及下载次数／最后下载时间。
- 公司→资质、资质→记录关系使用 `DeleteBehavior.Restrict`；公司主体删除流程检查资质引用。
- 创建账号、申请账号为追踪快照，不建立随账号删除而级联删除记录的关系。
- 关系数据库显式下载次数使用 `ExecuteUpdateAsync` 原子递增；次数表示请求，不证明文件传输完整完成。

管理权限键为 `CanManageQualifications`。已有部署需在角色管理中分配权限并重新登录；新数据库的首次管理员角色初始化才自动包含新权限。员工只能访问本人副本，管理权限人员可访问记录对应副本，均受启用状态约束。

主要文件，路径相对仓库根目录：

```text
src/Aiursoft.EmployeeCenter.Entities/
  BusinessQualification.cs
  QualificationDownload.cs
  EmployeeCenterDbContext.cs

src/Aiursoft.EmployeeCenter/Controllers/
  QualificationsController.cs
  ManageQualificationsController.cs
  FilesController.cs

src/Aiursoft.EmployeeCenter/Models/QualificationsViewModels/
  各独立页面ViewModel及PrintableSingleLineAttribute.cs

src/Aiursoft.EmployeeCenter/Services/
  QualificationWatermarkService.cs
  QualificationWatermarkRequest.cs
  QualificationJpegValidator.cs
  QualificationJpegComponent.cs
  QualificationJpegHuffman.cs
  QualificationJpegBits.cs
  QualificationJpegDecoder.cs
  FileStorage/QualificationFileService.cs
  FileStorage/QualificationDeliveryService.cs
  BackgroundJobs/QualificationUploadCleanupJob.cs

src/Aiursoft.EmployeeCenter/Views/
  Qualifications/
  ManageQualifications/
  Shared/Components/FileUpload/

src/Aiursoft.EmployeeCenter/Resources/
  Controllers/*QualificationsController.zh-CN.resx
  Models/QualificationsViewModels/
  Views/Qualifications/
  Services/QualificationWatermarkService*.resx

 tests/QualificationWatermarkServiceTests.cs
 tests/QualificationPersistenceTests.cs
 tests/QualificationFrameworkConventionTests.cs
 tests/IntegrationTests/QualificationsTests.cs
```

本次新生成的数据库迁移：

- SQLite：`20261013055955_AddBusinessQualifications`。
- MySQL：`20261013060014_AddBusinessQualifications`。

两种迁移只新增业务资质和生成记录相关表、关系及索引。框架规范及语言修订未额外变更数据库结构。

### 6.9 验证、问题定位及人工验收

不同阶段的筛选范围存在重叠，**下列数量不能相加，也不能将早期全量结果误认为最新语言选项又执行了一次全量**。

| 阶段 | 实际验证结果 |
|---|---|
| 2026-10-13 初始迁移 | 126 项水印及相关回归通过；Release 构建 0 警告、0 错误 |
| 框架修订期间 | 完整解决方案测试 536 项通过，0 失败、0 跳过 |
| 框架修订规范清理后 | 108 项相关测试通过，0 跳过 |
| 最后两项 lint 警告修复 | Release 页面／框架专项 15 项通过 |
| 2026-10-14 独立语言选项 | Release 水印、页面和模型专项 107 项通过，0 失败、0 跳过 |
| 语言选项最终规范检查 | 现有 `lint.sh` 通过，`No warnings found` |
| 实际 SQLite HTTP 流程 | 英文、简体、香港繁体上传／生成／预览／下载／检测、启停恢复及原件访问限制通过 |
| 独立语言实际验证 | 中文界面分别选择 English、简体和香港繁体生成，界面保持中文，默认 `auto` 选中 |
| 前端框架回归 | 上传错误回调重置与 JavaScript 语法验证通过 |

测试包含：追踪协议和 JPEG 重编码、长文本、图像格式／尺寸／损坏边界、字体失败、身份姓名防伪造、反伪造令牌、权限隔离、原件及副本通用路径封锁、临时上传账号归属、检测后删除、放弃上传清理、停用恢复、真实 SQLite 持久化、SQLite/MySQL 模型快照一致性、语言选择白名单及文化恢复。

验证期间遇到并处理：

1. 本机 `.NET` 未在默认 PATH 中，使用已安装的 `/home/jimmy/.dotnet` SDK；缺失的依赖先由实际构建失败确认后还原。
2. 新仓库缺少浏览器前端资源，经现有 `npm ci` 和 `npm run build` 补齐，没有修改前端依赖清单。
3. 初始流程测试使用直接 multipart 业务上传；修订后改为真实框架上传端点，再提交逻辑路径。
4. 新规范测试中的 `[NotNull]` 反射断言改为检查编译后 getter 返回值标注。
5. 一轮全量出现 DNS 测试失败，其中三项在未添加水印的干净基线复现；未修改 DNS 业务，后续全量 536 项通过，提示该部分存在基线／顺序不稳定性。
6. ReSharper 与测试并行构建曾触发 Razor 增量缓存解析异常，通过清理构建产物和串行构建解决，没有为此修改无关页面。
7. ReSharper 报告的多余引用、可空链、异常断言捕获已释放流等逐项修正，未降低检查等级。

人工验收记录：

- 2026-10-13 14:52（UTC+08:00）：用户确认初始迁移“好的，测试通过”。
- 2026-10-14 09:44（UTC+08:00）：用户确认新增水印语言选项“测试通过”，并要求再次核对开发规范。

本地复测实例使用 `http://127.0.0.1:5081`，独立 SQLite、私有文件和密钥目录位于会话目录而非 Git 仓库，未影响原 5000 端口项目。测试实例是会话内运行进程，不等同于正式部署；关闭进程或会话后需重新启动。

可重现命令：

```bash
# 最新独立语言选项相关测试；不是整个仓库全量

dotnet test tests/Aiursoft.EmployeeCenter.Tests.csproj \
  --configuration Release \
  --filter 'FullyQualifiedName~QualificationsTests|FullyQualifiedName~QualificationWatermarkServiceTests|FullyQualifiedName~QualificationFrameworkConventionTests'

# 整个解决方案测试

dotnet test Aiursoft.EmployeeCenter.sln

# Release 构建

dotnet build src/Aiursoft.EmployeeCenter/Aiursoft.EmployeeCenter.csproj \
  --configuration Release

# 项目现有规范检查；脚本在当前检出中无执行位，用 Bash 启动

bash ./lint.sh
```

测试和 lint 构建建议串行执行，避免共享构建缓存冲突。

### 6.10 上线注意事项与尚未承诺的能力

上线前：

1. 备份数据库、私有文件和 Data Protection 密钥，并使用项目既有机制应用两种数据库中实际使用的迁移。
2. 为对应管理角色分配 `CanManageQualifications`，重新登录刷新声明。
3. 配置可用字体，验证英文／简体／繁体及实际账号字段能正常生成。
4. 通过“添加资质”将原件放入私有流程，检查公司原有执照／组织证明附件、旧下载链接和公开存储残留；新模块不会自动撤销旧文件。
5. 检查 Canon 临时文件清理任务运行，制定持久化副本与追踪记录的保留策略。
6. 不向仓库提交真实执照、测试数据库、检测图片、私钥或 PIN。

当前未实现或未承诺：PDF 输入／输出、审批、自动有效期、已下载文件撤回、任意几何变换后可靠检测、带密钥的防伪签名、持久化原件／副本自动保留期限清理、全部启停操作独立审计、严格跨生成和启停的原子事务协调。临时上传清理不等同于进程崩溃后持久化孤立文件的完整扫描。

### 6.11 保存时的交付状态与后续事项

截至本节保存时：

- 初始迁移提交 `e2d407db` 已发布，MR `!489` 已创建。
- 后续框架修订、独立语言选项及本节记录仍在 `Add_watermark_function` 工作区，**尚未新建提交或推送**。
- 最新语言相关测试、现有 lint 和用户人工验收已通过。
- 用户于 2026-10-14 要求的额外逐项开发规范复查现已完成，补齐事项及最终验证结果见 6.12；**本地复查完成不代表审核人已批准或所有评审讨论已解决**。
- 原 MR 曾显示失败流水线；没有取得该流水线完整 Job 错误日志，不能仅凭本地通过断言远程 CI 已通过。
- 下一步完成额外规范复查后，在同一分支提交修订并推送，更新原 MR，等待新流水线和审核人复审；不要直接合并 `master`。

### 6.12 2026-10-14 提交前逐项规范复查

复查范围为本次水印新增文件和必要集成点，不修改无关模块，不执行模板升级脚本。

静态核对涵盖：业务页使用 `StackView`、页面模型继承与标题构造、表单注解及 `asp-for` 三件套、文件逻辑路径与框架上传／交付、控制器英文资源键、UTC 网页占位、新实体显式主外键及不可编辑属性、资源本地化、Canon 清理任务、README 段落撤销和新版既有逻辑保留。

本轮发现并补齐两项：

1. JPEG 校验器仍有四个嵌套辅助类，水印测试也有嵌套测试流类；均拆分为各自独立文件。校验器拆分后已逐块比对，算法只涉及类型改名、文件边界和内部可见性调整，没有改动校验流程。
2. 员工与管理记录搜索框已有 `asp-for` 和报错 `span`，但缺少 `label asp-for`；补齐视觉隐藏标签，保留原搜索布局。

本轮完整 Release 测试实际执行 **560 项：556 通过、4 失败、0 跳过**。失败均位于 `tests/IntegrationTests/DnsAuditControllerTests.cs`：

- `UnknownDnsFindingCanBeRegisteredAsCnameAliasWithoutTargetUrl`：预期创建成功后跳转，实际返回原页面；此前单独运行曾通过，具体不稳定原因尚未确定。
- `AdminCanManageAliasesFromCentralDashboard`：页面没有预期的创建入口。
- `CentralCreatePageListsCurrentUnknownDnsFindings`：页面没有预期的候选域名。
- `DomainAliasTargetMustMatchSelectedService`：页面没有预期的目标地址校验文案。

后三项此前已在未添加水印的 `f4e2a999` 干净基线复现。未修改 DNS 业务或删除／跳过失败测试；本轮结果不能表述为“全量通过”。

最终规范整理后的水印、模型、权限、文件上传／交付、SQLite/MySQL 迁移及公司删除相关回归 **131 项全部通过，0 失败、0 跳过**。

最终现有 `lint.sh` 检查已完成并通过：`Linting PASSED! No warnings found.`，退出码为 0。单类文件、业务控制器英文代码及 `StackView`、文件逻辑路径与集中上传／交付、表单三件套、时间占位、实体约定、资源 XML 及 README 恢复均已核对；未降低 lint 等级，也未修改无关 DNS 业务。

本次水印范围的开发规范复查完成，用户人工验收已通过。当前修改仍未提交或推送；可以在 `Add_watermark_function` 提交并推送修订以更新 MR `!489`，在 MR 中同时披露本轮全量 4 项 DNS 测试失败。后续仍须等待远程流水线与审核人复审，不能将本地规范检查通过等同于远程 CI 或 MR 批准。

### 6.13 ZIP 分发与公共模块对称性修订（2026-10-14）

此节替代上文关于资质专用 `FilesController` 路由、目录拦截和逐次下载计数的设计；上文是历史记录，不代表修订后的行为。

公共 `FilesController`、`FileUpload` 组件及 `uploader.js` 恢复本项目主干实现，不再认识资质、申请人、水印或下载追踪。业务文件服务迁出 `Services/FileStorage`，与水印算法、链接签发服务一起留在业务层。

交付流程：

```text
业务服务生成水印 PNG（已清理原始元数据并写入 DCT 追踪编号）
  → 保存私有 PNG 和包含该 PNG 的 ZIP
  → 员工确认用途和接收方，POST /Qualifications/Download/{追踪编号}
  → 业务校验身份、归属、管理权限及启用状态
  → 为 ZIP 签发标准 60 分钟下载链接并持久化签发记录
  → 重定向到通用 /download-private/...zip?token=...
  → 文件模块原样分发 ZIP；用户解压得到字节完全一致的水印 PNG
```

ZIP 是无损封装，不能经过图像缩放、裁剪或重新编码。页面预览使用独立 PNG 的标准私有链接，预览的处理不影响 ZIP 中的 PNG。预览不计入 ZIP 链接签发统计。

归档原件仅存于 Vault 的 `qualifications/originals`，业务页面不输出原件路径、不为该目录签发下载链接，也不将原件作为默认预览。校验失败的上传表单清空不可信路径，防止组件为伪造路径生成 token。标准临时上传回包仍可含上传者自己文件的短期链接；归档／检测完成即删除临时文件，遗留文件由原有后台任务清理。每次表单使用独立随机临时子目录，避免同名文件互相覆盖。

原件保护依赖“服务器不签发原件下载 token”的边界，而非文件控制器内置资质黑名单；Vault 目录名不是加密，也不能防止管理员主动签发 token、服务器失陷或部署时误暴露磁盘。

统计与兼容性：

- `QualificationDownload` 保留 PNG 路径与 SHA-256，新增 ZIP 路径与 SHA-256、ZIP 链接签发次数和最后签发时间。旧 `DownloadCount`／`LastDownloadedAt` 保留但停止更新，不能把旧下载次数冒充签发次数。
- `QualificationLinkIssue` 记录实际申请链接的账号、姓名快照、时间和对应副本，管理员代为申请也记录管理员自己。管理详情展示最近 100 次签发；关系数据库中的计数和日志在同一事务内保存。
- 同一 token 被重复使用不会增加次数。签发日志证明谁取得了链接，不证明谁下载、解压或完成传输，也不能识别收到转发链接的人。
- 资质停用阻止继续生成副本与签发 ZIP 链接，已签发的标准 bearer 链接在 60 分钟过期前仍有效；不承诺即时撤销，已下载文件更无法收回。
- SQLite／MySQL 使用新增迁移，不覆盖原迁移。旧 PNG 副本首次签发时可按已存 SHA-256 验证并补建 ZIP，不需要重新生成水印。

验收重点：下载 ZIP 的 SHA-256 与数据库一致；ZIP 中只有指定 PNG，解压字节与已生成 PNG 完全一致，暗水印检测返回同一追踪编号；携带 `w`／`square` 参数下载 ZIP 也不修改字节；无 token 或借用 ZIP token 访问原件被拒绝；伪造原件路径不能从校验失败表单取得 token；重复下载不增加签发计数，另一次签发增加计数和实际申请人日志。

本次本地验证：相关回归 141/141 通过；最终全量重跑 562/562 通过，0 跳过；Ninja 第二次运行退出码 0、无建议，lint、build 和前端构建均通过。首轮全量命令曾遇到 CLR 内部错误，不能算作测试通过；上述全量数字来自成功的重新执行。日志位于 `/tmp/employeecenter-watermark-*.log`，临时文件不保证长期保存。上述验证完成时尚未运行远端 CI，也未提交、推送或合并 PR；后续提交和推送不代表远端 CI 或 PR 审核已通过。
