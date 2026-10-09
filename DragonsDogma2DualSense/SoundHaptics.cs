namespace DragonsDogma2DualSense;
static class SoundHaptics
{
    // Conservative storage cutoff at unity gain, not a calibrated perception threshold.
    // Keep a brief peak or meaningful energy on either actuator, even with a long tail.
    internal static bool IsNegligible(float[] stereo)
    {
        if(stereo.Length%2!=0)throw new InvalidDataException("Invalid stereo samples");
        double left=0,right=0;float peak=0;
        for(int i=0;i<stereo.Length;i+=2)
        {
            float l=stereo[i],r=stereo[i+1];
            if(!float.IsFinite(l)||!float.IsFinite(r))throw new InvalidDataException("Non-finite sample");
            peak=Math.Max(peak,Math.Max(Math.Abs(l),Math.Abs(r)));
            left+=(double)l*l;right+=(double)r*r;
        }
        return stereo.Length==0 || (peak<.01f && Math.Max(left,right)/(stereo.Length/2)<.001*.001);
    }

    internal const int MaxContentFrames=48000*60;
    internal const int MaxDelaySeconds=30;
    internal static float[] Render(float[] stereo, string family, SoundPlayback playback)
    {
        if(stereo.Length%2!=0||stereo.Length/2>MaxContentFrames
            ||!double.IsFinite(playback.PitchCents)||Math.Abs(playback.PitchCents)>4800
            ||!double.IsFinite(playback.DelaySeconds)||playback.DelaySeconds<0||playback.DelaySeconds>MaxDelaySeconds
            ||!double.IsFinite(playback.VolumeDb))throw new InvalidDataException("Unbounded playback metadata or source");
        // Pitch is applied before conversion. Reject an oversized result before
        // allocating it; lowering pitch must not bypass the existing 60-second limit.
        double frames=Math.Ceiling((stereo.Length/2)/Math.Pow(2,playback.PitchCents/1200.0));
        if(frames>MaxContentFrames)throw new InvalidDataException("Pitched haptic source exceeds 60 seconds");
        var tactile = Scale(ConvertChannels(ApplyPlayback(stereo, 2, playback, false), 2, family), playback.VolumeDb);
        // Match PCM16's silence floor, so inaudible branches cannot suppress rumble.
        for (int i = 0; i < tactile.Length; i++) tactile[i] = (float)Math.Round(tactile[i] * 32767) / 32767f;
        return AddDelay(tactile, 2, playback.DelaySeconds);
    }

    internal static float[] ReadSource(string path, bool stereo = false)
    {
        using var stream=File.OpenRead(path);
        using var reader=new BinaryReader(stream);
        // PCM16 is the decoder contract. Bound even unknown metadata before scanning.
        if(stream.Length<12||stream.Length>192L*1024*1024)throw new InvalidDataException("Invalid source size");
        if(reader.ReadUInt32()!=0x46464952||reader.ReadUInt32()!=stream.Length-8||reader.ReadUInt32()!=0x45564157)
            throw new InvalidDataException("Invalid RIFF source");
        int channels=0,rate=0;long dataOffset=-1,dataBytes=0;bool format=false;
        while(stream.Position<stream.Length)
        {
            if(stream.Length-stream.Position<8)throw new InvalidDataException("Truncated source chunk");
            uint tag=reader.ReadUInt32(),length=reader.ReadUInt32();long end=stream.Position+length;
            if(end+(length&1)>stream.Length)throw new InvalidDataException("Invalid source chunk length");
            if(tag==0x20746d66)
            {
                if(format||length<16||length>4096||reader.ReadUInt16()!=1)throw new InvalidDataException("Source must be PCM16");
                channels=reader.ReadUInt16();uint sampleRate=reader.ReadUInt32(),byteRate=reader.ReadUInt32();
                int align=reader.ReadUInt16(),bits=reader.ReadUInt16();
                if(channels is <1 or >8||sampleRate is <8000 or >192000||bits!=16||align!=channels*2||byteRate!=sampleRate*align)
                    throw new InvalidDataException("Invalid PCM16 source format");
                rate=(int)sampleRate;format=true;
            }
            else if(tag==0x61746164)
            {
                if(dataOffset>=0)throw new InvalidDataException("Duplicate source data");
                dataOffset=stream.Position;dataBytes=length;
            }
            stream.Position=end+(length&1);
        }
        if(!format||dataOffset<0||dataBytes%(channels*2)!=0||dataBytes/(channels*2)>(long)rate*60)
            throw new InvalidDataException("Invalid source duration or data");
        int inputFrames=(int)(dataBytes/(channels*2)),outputFrames=(int)((long)inputFrames*48000/rate),outputChannels=stereo?2:1;
        // Decode from a fixed window; no whole PCM or high-rate mono float array.
        var result=new float[outputFrames*outputChannels];
        byte[] window=new byte[65536];long windowStart=-1;int windowLength=0;
        void Frame(int frame,out float left,out float right)
        {
            long offset=(long)frame*channels*2;
            if(offset<windowStart||offset+channels*2>windowStart+windowLength)
            {
                windowStart=offset;windowLength=(int)Math.Min(window.Length,dataBytes-offset);
                stream.Position=dataOffset+offset;stream.ReadExactly(window.AsSpan(0,windowLength));
            }
            int at=(int)(offset-windowStart);
            if(stereo&&channels==2){left=BitConverter.ToInt16(window,at)/32768f;right=BitConverter.ToInt16(window,at+2)/32768f;}
            else
            {
                float mono=0;for(int c=0;c<channels;c++)mono+=BitConverter.ToInt16(window,at+c*2)/(32768f*channels);
                left=right=mono;
            }
        }
        for(int i=0;i<outputFrames;i++)
        {
            double t=i*(double)rate/48000.0;int a=(int)t,b=Math.Min(a+1,inputFrames-1);
            Frame(a,out float x,out float xr);Frame(b,out float y,out float yr);
            result[i*outputChannels]=x+(y-x)*(float)(t-a);
            if(stereo)result[i*2+1]=xr+(yr-xr)*(float)(t-a);
        }
        return result;
    }

    internal static int Onset(float[] source)
    {
        float peak = source.Select(Math.Abs).DefaultIfEmpty(0).Max();
        return Math.Max(0, Array.FindIndex(source, v => Math.Abs(v) >= peak * .018f) - 96);
    }
    internal static int DrawCount(SoundVariant variant) => Variable(variant.RandomMin.PitchCents, variant.RandomMax.PitchCents)
        || Variable(variant.RandomMin.DelaySeconds, variant.RandomMax.DelaySeconds)
        || Variable(variant.RandomMin.VolumeDb, variant.RandomMax.VolumeDb) ? 5 : 1;
    static bool Variable(double low, double high) => Math.Abs(high - low) > 1e-9;
    internal static int RepresentativeDraw(SoundVariant variant)
    {
        int count=DrawCount(variant);
        var values=Enumerable.Range(0,count).Select(draw=>Realize(variant,draw,count)).ToArray();
        double pitch=(values.Min(v=>v.PitchCents)+values.Max(v=>v.PitchCents))/2;
        double volume=(values.Min(v=>v.VolumeDb)+values.Max(v=>v.VolumeDb))/2;
        double delay=(values.Min(v=>v.DelaySeconds)+values.Max(v=>v.DelaySeconds))/2;
        // Match the existing comparison: choose an existing draw near the middle,
        // rather than inventing a new waveform. Weights are selection heuristics.
        double Distance(int draw)=>Math.Pow((values[draw].PitchCents-pitch)/100,2)
            +Math.Pow((values[draw].VolumeDb-volume)/3,2)+Math.Pow((values[draw].DelaySeconds-delay)/.1,2);
        return Enumerable.Range(0,count).OrderBy(Distance).First();
    }
    internal static SoundPlayback Realize(SoundVariant variant, int draw, int count)
    {
        if (count < 1 || draw < 0 || draw >= count) throw new ArgumentOutOfRangeException(nameof(draw));
        double Sample(double low, double high, uint salt, int stride)
        {
            if (!Variable(low, high)) return (low + high) * .5;
            uint seed = variant.Path ^ salt;
            int slot = (int)(((uint)(draw * stride) + seed % (uint)count) % (uint)count);
            return low + (high - low) * ((slot + .5) / count);
        }
        return new(
            variant.Fixed.PitchCents + Sample(variant.RandomMin.PitchCents, variant.RandomMax.PitchCents, 0x9e3779b9, 2),
            Math.Max(0, variant.Fixed.DelaySeconds + Sample(variant.RandomMin.DelaySeconds, variant.RandomMax.DelaySeconds, 0x85ebca6b, 3)),
            variant.Fixed.VolumeDb + Sample(variant.RandomMin.VolumeDb, variant.RandomMax.VolumeDb, 0xc2b2ae35, 4));
    }
    internal static float[] ApplyPlayback(float[] source, int channels, SoundPlayback playback, bool applyVolume = true)
    {
        if (channels < 1 || source.Length % channels != 0) throw new ArgumentOutOfRangeException(nameof(channels));
        double rate = Math.Pow(2, playback.PitchCents / 1200.0);
        int inputFrames = source.Length / channels;
        if (inputFrames == 0) return [];
        int outputFrames = Math.Max(1, (int)Math.Ceiling(inputFrames / rate));
        float gain = applyVolume ? (float)Math.Pow(10, playback.VolumeDb / 20.0) : 1;
        var result = new float[outputFrames * channels];
        for (int f = 0; f < outputFrames; f++)
        {
            double p = Math.Min(inputFrames - 1, f * rate); int a = (int)p, b = Math.Min(a + 1, inputFrames - 1); float mix = (float)(p - a);
            for (int c = 0; c < channels; c++) result[f * channels + c] = (source[a * channels + c] + (source[b * channels + c] - source[a * channels + c]) * mix) * gain;
        }
        return result;
    }
    internal static float[] Scale(float[] source, double volumeDb)
    {
        float gain = (float)Math.Pow(10, volumeDb / 20.0);
        float peak = source.Select(Math.Abs).DefaultIfEmpty(0).Max();
        if (peak > 0) gain = Math.Min(gain, .98f / peak); // preserve shape instead of hard clipping positive Wwise gain
        if (gain == 1) return source;
        var result = new float[source.Length];
        for (int i = 0; i < source.Length; i++) result[i] = source[i] * gain;
        return result;
    }
    internal static float[] AddDelay(float[] source, int channels, double seconds)
    {
        int delay = Math.Max(0, (int)Math.Round(seconds * 48000)) * channels;
        if (delay == 0) return source;
        var result = new float[delay + source.Length]; Array.Copy(source, 0, result, delay, source.Length); return result;
    }
    internal static float[] Convert(float[] source, string family) => ConvertChannels(source, 1, family);

    static float[] ConvertChannels(float[] source, int channels, string family)
    {
        if (source.Any(v => !float.IsFinite(v))) throw new InvalidDataException("Non-finite source");
        int frames = source.Length / channels;
        if (frames == 0) return [];
        if (frames > MaxContentFrames) throw new InvalidDataException("Haptic source exceeds 60 seconds");
        // Preserve the complete recorded envelope, including delayed impacts and
        // sustained magic. No fixed-duration crop or imposed exponential decay.
        // These artistic tunings are not a calibrated actuator transfer function.
        double level = family == "footsteps" ? .45 : family == "attack" ? .85 : .95;
        var stereo = new float[frames * 2];
        double Coef(double hz) => 1 - Math.Exp(-2 * Math.PI * hz / 48000);
        double a280 = Coef(280), a1200 = Coef(1200), adc = Coef(25);
        // Follow the source's overall force rather than converting each fast
        // amplitude fluctuation into a separate pressure impulse.
        double attack = 1 - Math.Exp(-1 / (48000 * .004));
        double releaseEnvelope = 1 - Math.Exp(-1 / (48000 * .045));
        double Envelope(double current, double sample) => current + (Math.Abs(sample) - current) * (Math.Abs(sample) > current ? attack : releaseEnvelope);
        double max = 0;
        for (int channel = 0; channel < channels; channel++)
        {
            double low280 = 0, bodyLow = 0, bodyDc = 0, low1200 = 0;
            double envMid = 0, envHigh = 0, outputDc = 0;
            var midOscillator = new SourcePhase(8);
            var highOscillator = new SourcePhase(32);
            double translatedLow = 0, translatedBody = 0;
            for (int i = 0; i < frames; i++)
            {
                double x = source[i * channels + channel];
                low280 += a280 * (x - low280);
                bodyLow += a280 * (low280 - bodyLow);bodyDc += adc * (bodyLow - bodyDc);
                low1200 += a1200 * (x - low1200);
                double mid = low1200 - low280, high = x - low1200;
                envMid = Envelope(envMid, mid);
                envHigh = Envelope(envHigh, high);
                // Keep recorded bass directly. Translate higher bands by dividing
                // their own zero-crossing phase, never by clocking a shared tone.
                // Pitch changes and noisy/tonal textures remain source-dependent.
                // Advance continuously between measured source crossings. V3
                // held phase flat then jumped it at every audio zero crossing.
                double translated = .9 * envMid * midOscillator.Next(mid, i) + .4 * envHigh * highOscillator.Next(high, i);
                translatedLow += a280 * (translated - translatedLow);
                translatedBody += a280 * (translatedLow - translatedBody);
                // Preserve actual recorded bass transients. Do not add another
                // impulse from the amplitude envelope on top of those transients.
                double v = 1.4 * (bodyLow - bodyDc) + translatedBody;
                outputDc += adc * (v - outputDc);
                // Short boundary fades prevent clicks without erasing the source tail.
                double edge = Math.Min(1, i / 72.0);
                double release = Math.Min(1, (frames - 1 - i) / 480.0);
                float value = (float)((v - outputDc) * edge * release);
                stereo[i * 2 + channel] = value;
                if (channels == 1) stereo[i * 2 + 1] = value;
                max = Math.Max(max, Math.Abs(value));
            }
        }
        // Common bounded gain preserves stereo balance and leaves quiet sources quiet.
        // Inherited Wwise volume is still applied after this tactile conversion.
        double scale = max > 0 ? level * Math.Min(8.0, .94 / max) : 0;
        for (int i = 0; i < stereo.Length; i++) stereo[i] *= (float)scale;
        return stereo;
    }

    struct SourcePhase(int divisor)
    {
        double phase, frequency, target, previous, crossing;
        int sign;
        bool measured;
        public double Next(double sample, int frame)
        {
            int next = sample > .00001 ? 1 : sample < -.00001 ? -1 : sign;
            if (sign != 0 && next != sign)
            {
                double fraction = Math.Abs(previous) / Math.Max(1e-12, Math.Abs(previous) + Math.Abs(sample));
                double now = frame - 1 + fraction;
                if (measured && now > crossing)
                    target = Math.Clamp(24000 / ((now - crossing) * divisor), 25, 240);
                crossing = now; measured = true;
            }
            sign = next; previous = sample;
            // 12 Hz smoothing avoids pitch steps caused by noisy crossings.
            frequency += .00156956327 * (target - frequency);
            phase += 2 * Math.PI * frequency / 48000;
            if (phase >= 2 * Math.PI) phase -= 2 * Math.PI;
            return Math.Sin(phase);
        }
    }
}
