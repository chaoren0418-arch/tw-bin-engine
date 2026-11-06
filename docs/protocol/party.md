# T125 組隊封包補充（Issue #259）

已查閱 `docs/reference/server-protocol.md` 與 `server-rules.md`；以下補足隊伍語義。依據為本機伺服器來源的唯讀檢視，不代表已連線驗證部署中的伺服器。C／H／D 分別是 1／2／4 bytes，小端序；S 為 Big5、NUL 結尾。下表包含 opcode，客戶端送出與伺服器測試向量皆補零至 8-byte 邊界。

| 方向 | 格式 | 語義 |
|---|---|---|
| C230 | C230 C(mode=0/1) D(targetId) | 邀請指定玩家；只有 C(mode) 不足以組隊。兩種分配模式皆支援。 |
| C33 | C33 | 離隊；隊長離隊由伺服器解散隊伍。 |
| C43 | C43 | 查詢名單；回 S39 `party`，無隊伍則 S71/425。 |
| C255 | C255 S(name) | 踢除；隊長權限與成員有效性由伺服器裁定。 |
| S219 | C219 H(0) D(challenge) H(953) S(inviter) | 實際組隊邀請；951 屬聊天隊伍，本張不接管。 |
| C121 | C121 H(0) D(challenge) H(953) C(answer=0/1) | 拒絕／接受邀請；接受不立即建立本地隊伍。 |
| S237 | C237 D(id) C(hp) | 0–100 為比例，255 移除 HP 條；0 仍是有效隊員。未知物件不憑 HP 封包新增為隊員。 |
| S250/104 | C250 C104 C(count) [D(id) S(name) C(hp) D(map) H(x) H(y)] | 隊員資料，通常排除自己。 |
| S250/105 | C250 C105 D(id) S(name) C(hp) D(map) H(x) H(y) | 傳給隊長的新隊員資料，沒有 count。 |
| S250/106 | C250 C106 D(leaderId) S(leaderName) | 新隊長通知。 |
| S250/110 | C250 C110 C(count) [D(id) D(map) H(x) H(y)] | 更新位置，不帶名稱或 HP。 |
| S39 | C39 D(ownId) S("party") H(1) H(2) S(leader) S(spaceSeparatedMembers) | C43 的正式名單；缺少 ID／HP 時顯示未知，不補造 HP。 |
| S71 | C71 H(code) C(argc) [S(argument)] | 418 解散、419 被踢、420 指定名稱離隊、425 不在隊伍；保留既有伺服器訊息分派。 |

伺服器 `L1Party.MAXPT=8`，HUD 最多 7 名其他隊員。C230 type=2 是聊天隊伍邀請，type=3 是隊長轉移，本張不提供這兩個送出功能。

`L1Party.addMember` 呼叫 `S_PacketBoxParty(pc,this)` 時，pc 是新加入者，並非每個封包接收者；舊隊員收到的 104 可能包含自己、排除新隊員。因此 104 合併具名資料並排除自己，使用 C43/S39 手動更新完整名單，不以缺少某列推定該隊員離隊。另見 `removeMember` 使用未更新的 `removeKey=-1`，可能造成伺服器名單殘留；這是來源可見疑點，未修改或測試伺服器。客戶端依實收的移除封包及離隊訊息更新，不能宣稱已修復伺服器。

本地防重播政策：邀請保留 30 秒，切圖、死亡、其他 Yes-No 取代後失效；30 秒是客戶端期限，不是推定的伺服器期限。送出檢查 session generation、世界 epoch、畫面 revision 與選取目標，實際 transport gate 前再檢查一次。隊員 HP 更新也會使舊操作快照失效，使用者可重新核對後再操作。

來源根目錄：`D:/lineage開發/1-伺服器/src/com/lineage/server/`。只記檔案身份與衍生規格，未複製 Java 來源。

| 相對路徑 | SHA-256 |
|---|---|
| clientpackets/C_CreateParty.java | cd0f739dafc8eae990eb59eee5b7706c4b387b9112edd44914dd9f89c27e9b9c |
| clientpackets/C_LeaveParty.java | 5508cc450de0fbc5a4a1a20d3b8b846163b36e1c70e429cf169205bf642dd1ff |
| clientpackets/C_Party.java | 3a159744b4e70a125c12f7a73ecb67638473de73e6caecf834fc5a58a8062ee4 |
| clientpackets/C_BanParty.java | c149cad2cc052d5876eee112b88a2db7a560658b836310963badbcb57a1ad900 |
| clientpackets/C_Attr.java | 793a83eae9e44870e4ff80561f78bbeabff453fffc96886f097fd2d991d86d23 |
| serverpackets/S_PacketBoxParty.java | 1f57efd8f5d88d01e0e6dc0ca369d29ffa822d5a27f7fce2db8d71a0762fa956 |
| serverpackets/S_HPMeter.java | a60d3bf921345e1835b9ead6c71549e0042252a2757c09dba2e67c1ef3b6143f |
| serverpackets/S_Party.java | 0b63e4624368e487d5ef5a77fc0804cd2bf996150ff435454727177309946cb3 |
| model/L1Party.java | 944bf78213b8c3330ab8d52b289cb89727df1c25598da54c3a72d1de8683ac9e |
