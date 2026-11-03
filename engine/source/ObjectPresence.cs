namespace P06;
public sealed record PresenceLabel(uint Id,int X,int Y,string Name,string Detail,bool Missing);
public sealed record PresenceInfo(PresenceLabel[] Labels,string IncomingAttack);
public static class ObjectPresence {
    public static PresenceInfo Describe(WorldSession world,ObjectAssets catalog,ObjectVisual[] visuals,WorldObject[] missing,Patch? patch){
        var labels=visuals.Where(v=>v.Action=="shop"&&patch?.VisibleObjectIds.Contains(v.ObjectId)==true).Select(v=>new PresenceLabel(v.ObjectId,v.X,v.Y,v.Name,"",false))
            .Concat(missing.Select(o=>new PresenceLabel(o.ObjectId,o.X,o.Y,catalog.DisplayName(o),"外觀未顯示",true))).ToArray();
        string attack="";
        if(world.LastIncomingAttack is {} report){var source=world.Objects.FirstOrDefault(o=>o.ObjectId==report.Attacker);bool visible=patch?.VisibleObjectIds.Contains(report.Attacker)==true;
            attack=$"上次對你攻擊 #{report.Number}："+(source==null?$"物件 {report.Attacker}（名稱、位置尚未回報或已離場）":$"{catalog.DisplayName(source)} #{source.ObjectId} · {source.X}, {source.Y}")+(visible?" · 外觀已繪製":" · 目前無可見外觀")+$" · 傷害欄 {report.DamageWord}（非 HP 扣除量）";
        }
        return new(labels,attack);
    }
    public static void Draw(Graphics g,Font font,PresenceInfo info,Func<PointF,PointF> transform,RectangleF viewport){
        using var background=new SolidBrush(Color.FromArgb(215,12,22,26));var placed=new List<RectangleF>();
        foreach(var label in info.Labels.OrderByDescending(l=>l.Missing).ThenBy(l=>l.Id)){
            var point=transform(Coordinates.Screen(label.X,label.Y));if(!viewport.Contains(point))continue;
            string text=label.Name+(label.Missing?$"\n{label.X}, {label.Y} · {label.Detail}":"");var size=g.MeasureString(text,font,(int)Math.Max(1,viewport.Width-16));
            float x=Math.Clamp(point.X-size.Width/2,viewport.Left+4,Math.Max(viewport.Left+4,viewport.Right-size.Width-4)),y=Math.Clamp(point.Y+12,viewport.Top+4,Math.Max(viewport.Top+4,viewport.Bottom-size.Height-4));var box=new RectangleF(x,y,size.Width,size.Height);
            for(int i=0;i<info.Labels.Length&&placed.Any(b=>b.IntersectsWith(box))&&box.Top-size.Height-4>=viewport.Top;i++)box.Y-=size.Height+4;
            placed.Add(box);if(label.Missing){using var pen=new Pen(Color.Orange,1);g.DrawLine(pen,point,new PointF(box.Left+box.Width/2,box.Bottom));g.DrawLine(pen,point.X-3,point.Y,point.X+3,point.Y);g.DrawLine(pen,point.X,point.Y-3,point.X,point.Y+3);}
            g.FillRectangle(background,box);g.DrawString(text,font,label.Missing?Brushes.Orange:Brushes.LightCyan,box);
        }
        if(info.IncomingAttack.Length>0){var size=g.MeasureString(info.IncomingAttack,font,(int)Math.Max(1,viewport.Width-16));var box=new RectangleF(viewport.Left+8,viewport.Top+8,Math.Min(size.Width,viewport.Width-16),size.Height);g.FillRectangle(background,box);g.DrawString(info.IncomingAttack,font,Brushes.Orange,box);}
    }
}
public sealed partial class ClientForm {
    PresenceInfo PresenceSnapshot()=>objectAssets==null||!login.World.Renderable?new([],""):ObjectPresence.Describe(login.World,objectAssets,objectVisuals,missingObjects,objectPatch);
    void PaintObjectPresence(Graphics g,Func<PointF,PointF> transform,RectangleF viewport)=>ObjectPresence.Draw(g,Font,PresenceSnapshot(),transform,viewport);
}
