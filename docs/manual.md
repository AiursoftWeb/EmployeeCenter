# 员工中心 (EmployeeCenter) 使用说明教材

欢迎使用员工中心（EmployeeCenter）系统！本手册旨在指导您深入了解系统的核心功能、业务流程及操作细节，并提供每个功能的快速访问路径。

---

## 目录
1. [职业 (Professional) - 职业生涯与日常协作](#1-职业-professional)
2. [个人 (Personal) - 个人权益与信息维护](#2-个人-personal)
3. [管理 (Management) - 行政运维与组织管理](#3-管理-management)
4. [核心实体与数据模型关系 (Data Models & Relationships)](#4-核心实体与数据模型关系-data-models--relationships)
5. [高级智能化功能 (Advanced Intelligent Features)](#5-高级智能化功能-advanced-intelligent-features)

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
