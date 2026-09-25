using System.Collections;

namespace DragonsDogma2DualSense;

// Only the control thread reads the store. Active mixer voices retain their
// arrays, so LRU eviction never interrupts an already playing waveform.
sealed class SampleStore(long budgetBytes = 64L * 1024 * 1024) : IReadOnlyDictionary<string, float[]>
{
    sealed class Entry(string path, int length, string hash)
    {
        public readonly string Path=path, Hash=hash;
        public readonly int Length=length;
        public float[]? Data;
        public LinkedListNode<Entry>? Node;
    }
    readonly Dictionary<string,Entry> entries=[];
    readonly Dictionary<string,Entry> waves=[];
    readonly LinkedList<Entry> recent=new();
    readonly long budget=Math.Max(0,budgetBytes);
    long bytes;
    public long ResidentBytes => bytes;
    public void RegisterWave(string key,string path,int length,string hash)
    {
        if(!waves.TryGetValue(path,out var entry))waves[path]=entry=new(path,length,hash);
        else if(entry.Length!=length||entry.Hash!=hash)throw new InvalidDataException("Conflicting waveform metadata");
        entries.Add(key,entry);
    }
    public float[] this[string key]
    {
        get
        {
            var entry=entries[key];
            var data=entry.Data??PreparedWaves.ReadWave(entry.Path,entry.Length,entry.Hash);
            if(entry.Node!=null)recent.Remove(entry.Node);else bytes+=(long)data.Length*4;
            entry.Data=data;entry.Node=recent.AddLast(entry);
            while(bytes>budget&&recent.First is {} first)
            {
                var old=first.Value;recent.RemoveFirst();old.Node=null;
                bytes-=(long)old.Length*4;old.Data=null;
            }
            return data;
        }
    }
    public bool TryGetValue(string key,out float[] value)
    {
        if(!entries.ContainsKey(key)){value=null!;return false;}
        value=this[key];return true;
    }
    public bool ContainsKey(string key)=>entries.ContainsKey(key);
    public int Count=>entries.Count;
    public IEnumerable<string> Keys=>entries.Keys;
    public IEnumerable<float[]> Values=>entries.Keys.Select(key=>this[key]);
    public IEnumerator<KeyValuePair<string,float[]>> GetEnumerator()
    {
        foreach(var key in entries.Keys)yield return new(key,this[key]);
    }
    IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
}
