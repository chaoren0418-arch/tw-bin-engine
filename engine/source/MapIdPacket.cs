namespace P06;

public sealed partial class WorldSession {
    void ReceiveMapId(LoginReader r){
        ushort map=r.H();byte water=r.C();
        if(r.Remaining==12){
            // S_MapID(int), used by L1MoveX: fixed dry-map profile, not padding.
            // Contract and source identities: docs/protocol/map-id.md.
            ReadOnlySpan<byte> tail=[0x10,0xf8,0,0,0,0,0,0,0x10,0x0d,0x35,0xc5];
            Span<byte> received=stackalloc byte[12];
            for(int i=0;i<received.Length;i++)received[i]=r.C();
            // Preserve #198's S_MapID(int, boolean) recovery wrapper: exactly 12 zero bytes.
            ReadOnlySpan<byte> recoveryTail=[0,0,0,0,0,0,0,0,0,0,0,0];
            bool single=water==0&&received.SequenceEqual(tail);
            bool recovery=water<=1&&received.SequenceEqual(recoveryTail);
            if(!single&&!recovery)throw new ProtocolException("unexpected S206 variant tail or water flag");
        }
        r.Finish();
        // Validate the complete packet before retiring any current world state.
        ClearObjects();Actor=null;ResetSpeedStatus();ResetMovement();ResetCombat();ClearMagicEffects();
        MapId=map;Underwater=water!=0;MapArrived();
    }
}
