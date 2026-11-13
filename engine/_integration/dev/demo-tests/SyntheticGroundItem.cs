// T464 (#1049): the server-template ground catalog is gone (asset-recipe.md §7 L2 = X).
// Offline checks build S15/S87 packets from this invented item instead. Every value below is
// synthetic (not a server template row, not a client resource); only the packet shape matters.
sealed record SyntheticGroundItem(string Id,string NameToken,ushort GroundGfx,ushort InventoryIcon,ushort InventoryDesc,byte WireUseType){
    public string CountName(uint count)=>count>1?NameToken+" ("+count.ToString(System.Globalization.CultureInfo.InvariantCulture)+")":NameToken;
    // Stackable, non-equipment (use type 0), no charges.
    public static readonly SyntheticGroundItem Stack=new("synthetic-stack","$9001",9001,9002,9003,0);
}
