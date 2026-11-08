using P06.NativeUi;

// ui-macro-bar (Issue #466, docs/design/native-ui.md §5.3; geometry and keys: docs/reference/ui-layout.md V5 (a)(b)).
// State adapters for MacroPanel (RIGHT_WINDOW) and ExMacroPanel (EX_MACRO_WINDOW). No packet parsing, no prediction:
// slots resolve only against the inventory / learned magic the server already sent; anything else is drawn blank.

namespace P06.NativeUi {
    /// <summary>One bindable thing the server sent (inventory item or learned spell). Count is the server count (items only).</summary>
    public sealed record MacroEntry(PlayerHudBinding Binding,string Name,ushort? IconId,long? Count,bool Supported);

    /// <summary>Snapshot of what macro slots can resolve to, stamped with the session identity (same rule as PlayerHudSnapshot).</summary>
    public sealed record MacroInventory(PlayerHudStamp Stamp,IReadOnlyList<MacroEntry> Items,IReadOnlyList<MacroEntry> Skills,bool Busy) {
        public MacroEntry? Resolve(PlayerHudBinding? binding)=>binding==null?null
            :(binding.Kind==PlayerHudKind.Item?Items:Skills).FirstOrDefault(x=>x.Binding==binding);
    }

    /// <summary>
    /// Macro slots of the current character: 3 pages × 8 (F5–F12, V5 (a): real slot = k + 8·page) plus the 12-slot extended macro.
    /// Page 0 lives in a PlayerHudSlots so the existing CharacterConfig (TWCC v1, 8 slots) path saves and restores it unchanged.
    /// Pages 1–2 and the extended macro are kept per character in memory only (TWCC v1 has no room; see the issue-466 report).
    /// </summary>
    public sealed class MacroBook {
        public const int PageCount=3,SlotsPerPage=PlayerHudKeys.SlotCount,ExtendedCount=12;
        sealed class Extra {
            public readonly PlayerHudBinding?[][] Pages=[new PlayerHudBinding?[SlotsPerPage],new PlayerHudBinding?[SlotsPerPage]];
            public readonly PlayerHudBinding?[] Extended=new PlayerHudBinding?[ExtendedCount];
        }
        readonly Dictionary<uint,Extra> extras=[];
        Extra extra=new();
        bool observed;
        /// <summary>Page 0 (F5–F12 of the first tab). Shared with CharacterConfigHud for the server round trip.</summary>
        public PlayerHudSlots Persisted{get;}=new();
        public uint Actor{get;private set;}
        /// <summary>Current tab 0–2 (TW16 Status800 this[66]; TW13 this[69]).</summary>
        public int Page{get;private set;}
        /// <summary>Selected main slot 0–7 (TW16 dword_13CE7FC).</summary>
        public int Selected{get;private set;}
        /// <summary>Selected extended slot 0–11 (TW16 dword_13CE800).</summary>
        public int SelectedExtended{get;private set;}
        /// <summary>Which box shows the select mark (TW16 byte_13CE5EA).</summary>
        public bool SelectionInExtended{get;private set;}
        /// <summary>MACRO_LOCK (action 8, TW13 byte_C31EAF). While locked the slots cannot be re-bound.</summary>
        public bool Locked{get;private set;}
        public int Revision{get;private set;}

        /// <summary>Switches to the character of this stamp (actor 0 = nobody). Idempotent; page and selection reset on a change.</summary>
        public void Observe(PlayerHudStamp stamp){
            if(observed&&stamp.Actor==Actor)return;
            observed=true;Actor=stamp.Actor;Persisted.Observe(stamp);
            if(Actor==0)extra=new();
            else if(extras.TryGetValue(Actor,out var known))extra=known;
            else extras[Actor]=extra=new();
            Page=0;Selected=0;SelectedExtended=0;SelectionInExtended=false;Revision++;
        }

        public PlayerHudBinding? Binding(int page,int slot){
            Check(page,PageCount,nameof(page));Check(slot,SlotsPerPage,nameof(slot));
            return page==0?Persisted.Bindings[slot]:extra.Pages[page-1][slot];
        }
        public PlayerHudBinding? Extended(int slot){Check(slot,ExtendedCount,nameof(slot));return extra.Extended[slot];}
        public IReadOnlyList<PlayerHudBinding?> PageBindings(int page)=>Enumerable.Range(0,SlotsPerPage).Select(s=>Binding(page,s)).ToArray();

        /// <summary>Binds (or clears with null) a main slot. false while MACRO_LOCK is on. Invalid bindings throw (same rule as TWCC).</summary>
        public bool Assign(int page,int slot,PlayerHudBinding? binding){
            Check(page,PageCount,nameof(page));Check(slot,SlotsPerPage,nameof(slot));Validate(binding);
            if(Locked)return false;
            if(page==0)Persisted.Assign(slot,binding);else extra.Pages[page-1][slot]=binding;
            Revision++;return true;
        }
        public bool AssignExtended(int slot,PlayerHudBinding? binding){
            Check(slot,ExtendedCount,nameof(slot));Validate(binding);
            if(Locked)return false;
            extra.Extended[slot]=binding;Revision++;return true;
        }

        public void SetPage(int page){Check(page,PageCount,nameof(page));if(Page!=page){Page=page;Revision++;}}
        public void Select(int slot){Check(slot,SlotsPerPage,nameof(slot));Selected=slot;SelectionInExtended=false;Revision++;}
        public void SelectExtended(int slot){Check(slot,ExtendedCount,nameof(slot));SelectedExtended=slot;SelectionInExtended=true;Revision++;}
        public void ToggleLock(){Locked=!Locked;Revision++;}

        /// <summary>F5–F12 → slot 0–7 of the current page (V5 (b): vk − 116). Modifiers excluded (Ctrl+F = teleport_F%d.txt).</summary>
        public static int KeySlot(Keys keyData)=>PlayerHudKeys.Slot(keyData);
        /// <summary>F1/F2/F3 → page 0/1/2 (V5 (b), same as tab actions 10–12). F4 is pickup, not a macro key.</summary>
        public static int KeyPage(Keys keyData)=>keyData is >=Keys.F1 and <=Keys.F3?keyData-Keys.F1:-1;

        /// <summary>
        /// Migration of a stored TWCC blob into page 0. Count 8 → F5–F12. Count 4 (T138 era, then shown as F1–F4) → F5–F8 of page 0,
        /// F9–F12 empty. Pages 1–2 are never filled from storage. Unknown formats give null (kept on the server, not overwritten).
        /// </summary>
        public static PlayerHudBinding?[]? MigrateStored(CharacterConfig config)=>config.Slots;

        /// <summary>Common activation: resolved, supported, not busy, same character → IUiMacroCommands.ActivateMacro.</summary>
        public static bool TryActivate(IUiMacroState? state,PlayerHudBinding? binding,IUiCommands commands){
            if(state?.Inventory is not {} inv||binding==null||inv.Busy||inv.Stamp.Actor==0||inv.Stamp.Actor!=state.Macros.Actor)return false;
            if(inv.Resolve(binding) is not {Supported:true})return false;
            if(commands is not IUiMacroCommands macro)return false;
            macro.ActivateMacro(inv.Stamp,binding);return true;
        }

        static void Check(int v,int count,string name){if(v<0||v>=count)throw new ArgumentOutOfRangeException(name);}
        static void Validate(PlayerHudBinding? b){
            if(b!=null&&(b.Id==0||!Enum.IsDefined(b.Kind)||(b.Kind==PlayerHudKind.Magic&&b.Id>int.MaxValue)))throw new ArgumentException("invalid macro binding");
        }
    }

    /// <summary>Extra state for the macro panels; the product state implements it, offline checks provide their own.</summary>
    public interface IUiMacroState {
        MacroBook Macros { get; }
        /// <summary>null outside the world or before the character is known.</summary>
        MacroInventory? Inventory { get; }
        /// <summary>Game keyboard focus (not chat, not the dev-tools window). F-keys only act when true.</summary>
        bool InputAllowed { get; }
        /// <summary>Existing item icon (engine assets/inventory, same as the legacy HUD); null → nothing is drawn.</summary>
        Bitmap? ItemIcon(ushort iconId);
    }

    /// <summary>Activation forwards to the existing ClientForm.ActivatePlayerHud → PlayerHudActions.Activate.</summary>
    public interface IUiMacroCommands {
        void ActivateMacro(PlayerHudStamp stamp,PlayerHudBinding binding);
    }

    public sealed class UiMacroGameState(IUiGameState inner,MacroBook macros,MacroInventory? inventory,bool inputAllowed,Func<ushort,Bitmap?>? icons=null):IUiGameState,IUiMacroState {
        public IUiGameState Inner=>inner;
        public bool InWorld=>inner.InWorld;
        public string? CharacterName=>inner.CharacterName;
        public int? Level=>inner.Level;
        public long? Hp=>inner.Hp;
        public long? MaxHp=>inner.MaxHp;
        public long? Mp=>inner.Mp;
        public long? MaxMp=>inner.MaxMp;
        public long Now=>inner.Now;
        public MacroBook Macros=>macros;
        public MacroInventory? Inventory=>inventory;
        public bool InputAllowed=>inputAllowed;
        public Bitmap? ItemIcon(ushort iconId)=>icons?.Invoke(iconId);
    }

    /// <summary>Decorates the bridge commands with macro activation; every other member is forwarded unchanged.</summary>
    public sealed class UiMacroCommands(IUiCommands inner,Action<PlayerHudStamp,PlayerHudBinding> activate):IUiCommands,IUiMacroCommands {
        public IUiCommands Inner=>inner;
        public void ShowDevTools(string? page=null)=>inner.ShowDevTools(page);
        public void ReturnGameFocus()=>inner.ReturnGameFocus();
        public void Logout()=>inner.Logout();
        public void ActivateMacro(PlayerHudStamp stamp,PlayerHudBinding binding)=>activate(stamp,binding);
    }
}

namespace P06 {
    // Product side of the macro adapter. UiClientBridge calls these two hooks (the only bridge change of #466).
    public sealed partial class ClientForm {
        readonly MacroBook nativeMacros=new();
        readonly CharacterConfigHud nativeMacroConfig=new();
        internal MacroBook NativeMacros=>nativeMacros;
        /// <summary>Last CharacterConfig save notice of native page 0 (no native notice area yet; kept for the chat/notice modules).</summary>
        internal string NativeMacroNotice{get;private set;}="";

        IUiGameState NativeMacroGameState(IUiGameState basic){
            if(login.State!=LoginState.InWorld){nativeMacros.Observe(default);return new UiMacroGameState(basic,nativeMacros,null,false);}
            var w=login.World;var status=w.Status;var stamp=PlayerHudActions.Stamp(login);
            nativeMacros.Observe(stamp);
            // Same busy rule as the legacy HUD (PlayerHudIntegration.SyncPlayerHud).
            bool busy=playerHudSending||login.MovementBusy||World.Walking||!w.Ready||w.ActionLocked||w.TradeBlocksActions||w.OwnDead||status?.Hp is not >0;
            var items=w.Inventory.Select(i=>new MacroEntry(new(PlayerHudKind.Item,i.ObjectId),ItemName(i),i.IconId,i.Count,i.SupportedUse)).ToArray();
            var skills=w.LearnedMagic.Select(s=>new MacroEntry(new(PlayerHudKind.Magic,(uint)s.Id),s.Name,null,null,s.Supported)).ToArray();
            if(stamp.Actor!=0){
                // Existing TWCC round trip for page 0: restore on login, save on change (never auto-saves defaults).
                var configState=new PlayerHudSnapshot(stamp,"","",0,0,0,0,"","",busy,"",false,[],[]);
                nativeMacroConfig.Update(login,configState,nativeMacros.Persisted,message=>NativeMacroNotice=message);
            }
            return new UiMacroGameState(basic,nativeMacros,new MacroInventory(stamp,items,skills,busy),GameInputAllowed&&Focused,ItemIcon);
        }

        IUiCommands NativeMacroCommands(IUiCommands inner)=>new UiMacroCommands(inner,(stamp,binding)=>_=ActivatePlayerHud(stamp,binding));
    }
}
