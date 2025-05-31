using System;

namespace Pix2d.Abstract.Platform;

public struct MemoryInfo
{
    public ulong AvailableRam { get; set; }
    public ulong UsedRam { get; set; }

    public MemoryInfo(ulong availableRam, ulong usedRam)
    {
            AvailableRam = availableRam;
            UsedRam = usedRam;
        }

    public override string ToString()
    {
            return $"{BytesToString((long) UsedRam)} of {BytesToString((long) AvailableRam)}";
        }

    static String BytesToString(long byteCount)
    {
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB
            if (byteCount == 0)
                return "0" + suf[0];
            long bytes = Math.Abs(byteCount);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num).ToString() + suf[place];
        }

}