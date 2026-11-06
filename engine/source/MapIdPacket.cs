namespace P06;

public sealed partial class WorldSession {
    void ReceiveMapId(LoginReader r){
        ushort map=r.H();byte water=r.C();
        if(r.Remaining==12){
            // S_MapID(int), used by L1MoveX: fixed dry-map profile, not padding.
            // Contract and source identities: docs/protocol/map-id.md.
            ReadOnlySpan<byte> tail=[0x10,0xf8,0,0,0,0,0,0,0x10,0x0d,0x35,0xc5];
            if(water!=0)throw new ProtocolException("unexpected S206 single-argument water flag");
            foreach(byte expected in tail)
                if(r.C()!=expected)throw new ProtocolException("unexpected S206 single-argument tail");
        }
        r.Finish();
        // Validate the complete packet before retiring any current world state.
        ClearObjects();Actor=null;ResetSpeedStatus();ResetMovement();ResetCombat();ClearMagicEffects();
        MapId=map;Underwater=water!=0;MapArrived();
    }
}
