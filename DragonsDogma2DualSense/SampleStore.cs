using System.Collections;

namespace DragonsDogma2DualSense;

// Reads and LRU maintenance run on the control thread. Voice leases are released
// with an atomic decrement only; the audio callback never performs cache work.
sealed class SampleStore(long budgetBytes = 64L * 1024 * 1024, long totalBudgetBytes = 128L * 1024 * 1024) : IReadOnlyDictionary<string, float[]>
{
    internal sealed class Entry(string path, int length, string hash)
    {
        public readonly string Path=path, Hash=hash;
        public readonly int Length=length;
        public float[]? Data;
        public LinkedListNode<Entry>? Node;
        public int Users;
        public long Bytes=>(long)Length*4;
    }
    internal sealed class Lease(Entry entry) : IDisposable
    {
        Entry? owned=entry;
        public float[] Data {get;}=entry.Data!;
        public void Dispose(){var old=Interlocked.Exchange(ref owned,null);if(old!=null)Interlocked.Decrement(ref old.Users);}
    }
    readonly object gate=new();
    readonly Dictionary<string,Entry> entries=[];
    readonly Dictionary<string,Entry> waves=[];
    readonly LinkedList<Entry> recent=new();
    readonly List<Entry> detached=[];
    readonly long budget=Math.Max(0,budgetBytes),totalBudget=Math.Max(0,totalBudgetBytes);
    long bytes,retained,reserved,denied;
    public long ResidentBytes {get{lock(gate)return bytes;}}
    public long RetainedBytes {get{lock(gate)return retained;}}
    public long ReservedBytes {get{lock(gate)return reserved;}}
    public long BudgetBytes=>totalBudget;
    public long DeniedLoads {get{lock(gate)return denied;}}
    public void RegisterWave(string key,string path,int length,string hash)
    {
        if(length<0||length>PreparedWaves.MaxWaveSamples||length%2!=0)throw new InvalidDataException("Invalid sample count");
        lock(gate)
        {
            if(!waves.TryGetValue(path,out var entry))waves[path]=entry=new(path,length,hash);
            else if(entry.Length!=length||entry.Hash!=hash)throw new InvalidDataException("Conflicting waveform metadata");
            entries.Add(key,entry);
        }
    }
    void Reclaim()
    {
        for(int i=detached.Count-1;i>=0;i--)
        {
            var entry=detached[i];
            if(Volatile.Read(ref entry.Users)!=0)continue;
            retained-=entry.Bytes;entry.Data=null;detached.RemoveAt(i);
        }
    }
    void Evict()
    {
        var entry=recent.First!.Value;recent.RemoveFirst();entry.Node=null;bytes-=entry.Bytes;
        if(Volatile.Read(ref entry.Users)==0){retained-=entry.Bytes;entry.Data=null;}
        else detached.Add(entry);
    }
    public bool TryAcquire(string key,out Lease? lease)
    {
        lock(gate)
        {
            lease=null;Reclaim();
            if(!entries.TryGetValue(key,out var entry))return false;
            if(entry.Data==null)
            {
                // Reserve before ReadWave allocates. Eviction cannot free a leased array.
                if(entry.Bytes>totalBudget){denied++;return false;}
                while(retained+reserved+entry.Bytes>totalBudget&&recent.Count>0)Evict();
                if(retained+reserved+entry.Bytes>totalBudget){denied++;return false;}
                reserved+=entry.Bytes;
                try{entry.Data=PreparedWaves.ReadWave(entry.Path,entry.Length,entry.Hash);retained+=entry.Bytes;}
                finally{reserved-=entry.Bytes;}
            }
            Interlocked.Increment(ref entry.Users);
            lease=new Lease(entry);
            if(entry.Node!=null)
            {
                recent.Remove(entry.Node);
                recent.AddLast(entry.Node);
            }
            else
            {
                detached.Remove(entry);bytes+=entry.Bytes;
                entry.Node=recent.AddLast(entry);
            }
            while(bytes>budget&&recent.Count>0)Evict();
            return true;
        }
    }
    // Dictionary access is retained for offline callers. Playback owns a lease
    // for every active voice; external callers retaining raw arrays are not owners.
    public float[] this[string key]
    {
        get
        {
            if(!TryAcquire(key,out var lease))throw new InvalidDataException("Waveform unavailable within memory budget");
            using(lease)return lease!.Data;
        }
    }
    public bool TryGetValue(string key,out float[] value)
    {
        if(!TryAcquire(key,out var lease)){value=null!;return false;}
        using(lease){value=lease!.Data;return true;}
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
