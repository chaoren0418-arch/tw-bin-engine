using P06.NativeUi;

// ui-party (Issue #471, docs/design/native-ui.md §5.3). State and command adapters for PartyPanel (PARTY_INTERFACE) and
// PartyRtPanel (PARTY_PET_RT_WINDOW). Data comes only from the existing Party model (WorldSession.PartySnapshot); actions go only
// through the existing ClientForm.PartyAction → LoginConnection.SendParty path. No packet parsing, no invented HP.

namespace P06.NativeUi {
    /// <summary>Extra state the party panels read; absent → the panels stay hidden.</summary>
    public interface IUiPartyState {
        /// <summary>Snapshot of the existing party model; null outside the world.</summary>
        PartyView? Party { get; }
    }

    /// <summary>Party actions. command is one of the existing SendParty commands ("leave", "kick"); shown is the displayed snapshot.</summary>
    public interface IUiPartyCommands {
        void PartyAction(string command,PartyView shown,string name);
    }

    public sealed class UiPartyGameState(IUiGameState inner,PartyView? party):IUiGameState,IUiPartyState {
        public IUiGameState Inner=>inner;
        public PartyView? Party=>party;
        public bool InWorld=>inner.InWorld;
        public string? CharacterName=>inner.CharacterName;
        public int? Level=>inner.Level;
        public long? Hp=>inner.Hp;
        public long? MaxHp=>inner.MaxHp;
        public long? Mp=>inner.Mp;
        public long? MaxMp=>inner.MaxMp;
        public long Now=>inner.Now;
    }

    /// <summary>Decorates the bridge commands with party actions; every other member is forwarded unchanged.</summary>
    public sealed class UiPartyCommands(IUiCommands inner,Action<string,PartyView,string> act):IUiCommands,IUiPartyCommands {
        public IUiCommands Inner=>inner;
        public void ShowDevTools(string? page=null)=>inner.ShowDevTools(page);
        public void ReturnGameFocus()=>inner.ReturnGameFocus();
        public void Logout()=>inner.Logout();
        public void PartyAction(string command,PartyView shown,string name)=>act(command,shown,name);
    }

    public static class PartyStates {
        /// <summary>
        /// Finds the party state through the other decorators. Product nesting (#471):
        /// UiHpMpGameState( UiMacroGameState( UiPartyGameState(basic) ) ) — the party state is innermost so that
        /// MacroBook.StateOf (which only unwraps UiHpMpGameState) keeps working unchanged.
        /// </summary>
        public static IUiPartyState? StateOf(IUiGameState? state){
            for(int depth=0;state!=null&&depth<8;depth++){
                if(state is IUiPartyState p)return p;
                state=state switch{UiHpMpGameState h=>h.Base,UiMacroGameState m=>m.Inner,UiPartyGameState q=>q.Inner,_=>null};
            }
            return null;
        }
        /// <summary>Same idea for commands: UiMacroCommands( UiPartyCommands(bridge) ) — macro checks only the outer object.</summary>
        public static IUiPartyCommands? CommandsOf(IUiCommands? commands){
            for(int depth=0;commands!=null&&depth<8;depth++){
                if(commands is IUiPartyCommands p)return p;
                commands=commands switch{UiMacroCommands m=>m.Inner,UiPartyCommands q=>q.Inner,_=>null};
            }
            return null;
        }
    }
}

namespace P06 {
    public sealed partial class ClientForm {
        /// <summary>Existing party snapshot (same source as the legacy PartyUi drawer); null outside the world.</summary>
        IUiGameState NativePartyGameState(IUiGameState basic)=>new UiPartyGameState(basic,login.State==LoginState.InWorld?PartyRead():null);
        /// <summary>Existing PartyAction (ClearInput + SendParty); mode 0 like the legacy drawer default. Result is only the server's reply.</summary>
        IUiCommands NativePartyCommands(IUiCommands inner)=>new UiPartyCommands(inner,(command,view,name)=>_=PartyAction(command,view,name,0));
    }
}
