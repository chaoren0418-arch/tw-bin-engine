# 後續缺口觀察（尚未派工）

觀察基準為本目錄 baseline.json 所固定的 r29；候選 r30 尚未完成，因此以下不能當成 r30 缺陷或下一版接受結論。

r29 的 `NormalSpeed.SendIntervalMs` 已將 Speed1 的原生 0.75 與固定 JAR 0.755 區別處理，目前呼叫位於 MovementIntervalMs。`Combat.cs` 的 AttackCore 與 ApplyCombatAcknowledgement 仍直接使用 CombatRules.DurationFor；`Magic.cs` 的 CastSelectedMagic 仍直接使用 MagicRules.CastMsFor。兩者對 Speed1 可用，但送出間隔未使用速度倍率。角色戰鬥動畫、弓箭／光箭釋放也仍採用原時間。因此既有加速狀態的動作節奏是具體的後續調查候選。

下一步須先完成本次 Speed2 候選審查及整合，再以新版本源碼確認缺口仍存在，並核對固定 BIN 動作 1/18/19 的逐幀取整、釋放標記及 JAR 行為。不可直接套用步行倍率，也不可把 reuse、本機餘裕、飛行及效果循環一起縮短。若成立，使用同一既有實作者與獨立審查者另派最小候選；本次 Speed2 候選維持既定範圍。

道具／地面動作的緩速拒絕另列後續候選，其來源與 busy 行為尚未判定；本觀察不授權解除拒絕。

接受的 r30 候選 ActionCadence 明確只在 Slow=true 時調整節奏，Speed1 仍沿用原行為，以上缺口在接受候選中仍存在。後續逐幀取整推導另存 future-speed1-rounding-observation.json：126 組動作的 sum((3*frameMs)>>2) 全部不同於 (3*sum(frameMs))>>2；這是已封存 metadata 的算術比較，不是新原生執行。素材含零時間 frame，後續也須保留其事件相位，不能直接套用要求 basis>0 的步行 helper 或強制每幀至少1ms。正式後續派工仍以本次 r30 整合完成後的來源為基底。
