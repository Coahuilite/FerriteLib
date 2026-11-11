# Tasks — 已采用、未实施

状态：0.9 正式规格已采用、产品未实施。此次只完成 SPEC；产品源码／tests／构建／二进制继续停在 PM 两库确认门。

- [x] SPEC-1 PM 按 WO r2 选用 bb7da49，整个新增闭包定为 Public-unstable；补齐独立 choices／paint footprint 与未变 source 不逐字段重算。
- [x] SPEC-2 原 owner 将含 r2 补充的 ADDED／MODIFIED 并入唯一 current，更新真实入口及未导出 API 计划；
  change 保采用来源，不自称 current；仅规格完成，PM 两库核对后才开代码。
- [ ] RETAIN-1 FL 实现显式 enrollment／表示端口与事务准入；完整源及环境相等时在区域边界
  停止后代重复派生／验证，保 unknown 洞和真实输入。完成释放与失败／重入路径。
- [ ] RETAIN-2 同一几何 owner 支持变化成员／局部 plan、后续兄弟及 Scroll／共享依赖传播；
  首次 materialize 保完整。不是只给叶增缓存或只更换 full/sparse 标签。
- [ ] RETAIN-3 交付外部可采用 participant、内置表示消费和适配器支持／回退；独立 embedded
  与手动 phase 成立，UI 状态和原生控件仍走原 owner。正常 Release 无 FER_TEST。
- [ ] RETAIN-4 运行受影响的既有中性、布局、绑定、Repeat、失败、输入、手动／窗口、API／
  containment 检查；不新增单测／case／等价 probe，不弱化断言。新增 API 按采用决定分类，
  旧 Stable 清单完整保留。记录 build／source／native 不同证据。
- [ ] CONSUME US owner 从 `1ee2ae0` 的原1.6工作树建立独立0.9消费分支，采用真实 b-actions／
  sparse 及表示源。覆盖无业务 view bump 的 row-result、未提交稿、未知贡献源；
  不要求旧 source 调用者额外通知。普通 Release FL/US 的实际引用身份及退出旧用法可核验。
- [ ] NATIVE Terra 在同代固定配对下测开／关或等效基线：首次／暖开关、展开／收起整帧
  P50/P95/max 与超额分母、点击到目标实际绘制调用、真实滚动／编辑／提交／重开、表示凭据反例。
  分开区域更新、操作调度、主线程提交、GPU／scanout 的可测与未知；按实际节奏配对，历史144Hz与独立60Hz场不混用。
- [ ] CLOSE PM 按区域重复工作实际减少、正确性和整帧目标分别验收；保留未通过项的实际 owner。

性能证据必须显示未变区域不再逐后代 Build／Capture／Measure／上下文／pointer派生，
并报告仍保留的 interactive／unknown 调度和实际 Repaint。计数减少不替代真实时间／FPS。
