using System.Text;
using System.Text.Json;
namespace P06;
// Append-only diagnostic events. Memory is bounded by one serialized event plus per-kind counters.
public sealed class EventJournal:IDisposable {
    readonly string path;readonly StreamWriter writer;readonly Dictionary<string,int> counts=new();bool closed;
    public long BytesWritten {get;private set;} public int Total {get;private set;}
    public int Count(string kind)=>counts.GetValueOrDefault(kind);
    public EventJournal(string path){this.path=path;Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);writer=new StreamWriter(new FileStream(path+"l",FileMode.CreateNew,FileAccess.Write,FileShare.Read),new UTF8Encoding(false)){AutoFlush=true};}
    public void Append(string kind,object value){if(closed)throw new ObjectDisposedException(nameof(EventJournal));string line=JsonSerializer.Serialize(value);writer.WriteLine(line);BytesWritten+=Encoding.UTF8.GetByteCount(line+Environment.NewLine);counts[kind]=Count(kind)+1;Total++;}
    public void Flush(){if(!closed)writer.Flush();}
    public void Complete(){if(closed)return;closed=true;writer.Dispose();using var output=new StreamWriter(path,false,new UTF8Encoding(false));output.Write('[');bool first=true;foreach(string line in File.ReadLines(path+"l")){if(!first)output.Write(',');output.WriteLine();output.Write(line);first=false;}output.WriteLine();output.Write(']');}
    public void Dispose()=>Complete();
}
