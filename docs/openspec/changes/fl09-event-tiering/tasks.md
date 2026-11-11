# Tasks

本清单不授权产品工作。每组的实施都需要单独授权；完成依据写在每项后面。

- [x] 0. 采用：用户审阅 proposal、design 与 delta，决定是否采用；采用后把 ADDED／MODIFIED 并入 0.9 的现行能力页，本目录保留为采用来源。完成：2026-11-11 用户采用，ADDED／MODIFIED 已并入现行能力页，change 保留采用来源。
- [ ] 1. 0.9 内核：Host 周期状态与 T2 同步点复用（`DrawFrame`、`TryIncremental`、`CollectPointerLayers`、`DrawContextForEntry`），已宣告变化的触发条件与现有拒绝条件一致。完成依据：新语义测试与受影响的现有套件通过。
- [ ] 2. 0.9 内置 wrapper 的 T3 拆分与 FL09-TIER-SAFE 判定；`DeferClippedDisplay` 引入 0.9 并入分层。完成依据：每个 wrapper 都有“不读普通 getter”的源码举证，新语义测试通过。
- [ ] 3. 0.9 manifest：接受 Schema 2 与 3，解析 `TieredEvents`，程序化定义有同义入口，非法值定位报错。完成依据：Schema 2 旧 manifest 原样加载；`--readonly-api` 不变，或列出并批准新增签名。
- [ ] 4. 1.6 adapter：声明展示隔离能力，`finally` 中恢复样式。完成依据：containment 白名单不新增 Unity 接触点。
- [ ] 5. 0.8.x 兼容回补（单独授权）：0.8.x 的 ADDED change、FL09-TIER-SAFE、Schema 2 上的 `TieredEvents="true"`。完成依据：0.8.x 现有套件与新语义测试通过；未声明属性的 0.8.x 页面行为不变。
- [ ] 6. US 启用：0.6.x 在 Schema 2 manifest 根加 `TieredEvents="true"`；0.9 线迁移时使用 Schema 3。完成依据：US 现有 lane，包括 ordinary getters lane，以及原生实机。
- [ ] 7. 原生验收：同条件对比逐 pass 与分层，稳定页扫描、改值成对、静置 GC；用户实体键盘复测。完成依据：断崖速率、每 pass Host 成本、分配与 GC 频率都有改进，且合同场景实机无偏差。
- [ ] 8. 文档：consumer guide 说明 Schema 3、`TieredEvents`、`IUiTranslation` 调用次数不作保证、自定义 widget 默认不变；`implementation.md` 与 `source-coverage.md` 映射。
