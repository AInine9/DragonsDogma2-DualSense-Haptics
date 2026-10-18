namespace DragonsDogma2DualSense;

// Explicit acquisition feedback, not a reconstruction of a game sound.
// Accessed under the Mixer's gate; no allocation on the audio callback.
sealed class LockOnClick
{
    internal const int PulseFrames=576, SpacingFrames=2400, Capacity=64;
    static readonly float[] pulse=CreatePulse();
    int pending, position=SpacingFrames;
    public int Pending => pending+(position<PulseFrames?1:0);
    public bool Playing => pending>0 || position<PulseFrames;
    public bool Enqueue()
    {
        if(pending>=Capacity)return false;
        pending++;return true;
    }
    public void Clear(){pending=0;position=SpacingFrames;}
    public float Next()
    {
        if(position>=SpacingFrames && pending>0){pending--;position=0;}
        float value=position<PulseFrames?pulse[position]:0;
        if(position<SpacingFrames)position++;
        return value;
    }
    static float[] CreatePulse()
    {
        var data=new float[PulseFrames];
        for(int i=0;i<data.Length;i++)
        {
            double t=i/(double)(data.Length-1);
            data[i]=(float)(.80*Math.Sin(2*Math.PI*3*t)*Math.Pow(Math.Sin(Math.PI*t),2));
        }
        return data;
    }
}
