using System;
using System.IO;
using System.Globalization;
using System.Collections.Generic;
using System.Text;

class Process
{
    public string id;
    public double arrival;
    public double burst;
    public double remaining;
    public int priority; // high=3 normal=2 low=1
    public double completion = -1;

    public Process(string i, double a, double b, int p)
    {
        id = i; arrival = a; burst = b; remaining = b; priority = p;
    }
}

class TimeBlock
{
    public string label;
    public double start;
    public double end;
    public TimeBlock(string l, double s, double e) { label = l; start = s; end = e; }
}

class Program
{
    static double CS = 0.001;

    static int Pri(string s)
    {
        s = s.Trim().ToLower();
        if (s == "high") return 3;
        if (s == "normal") return 2;
        if (s == "low") return 1;
        return 0;
    }

    static List<Process> ReadCsv(string path)
    {
        var list = new List<Process>();
        var lines = File.ReadAllLines(path);

        for (int i = 1; i < lines.Length; i++)
        {
            var p = lines[i].Split(',');
            list.Add(new Process(
                p[0].Trim(),
                double.Parse(p[1], CultureInfo.InvariantCulture),
                double.Parse(p[2], CultureInfo.InvariantCulture),
                Pri(p[3])
            ));
        }
        return list;
    }

    static List<Process> Copy(List<Process> src)
    {
        var c = new List<Process>();
        for (int i = 0; i < src.Count; i++)
            c.Add(new Process(src[i].id, src[i].arrival, src[i].burst, src[i].priority));
        return c;
    }

    static void Add(List<TimeBlock> g, string l, double s, double e)
    {
        if (e <= s) return;
        g.Add(new TimeBlock(l, s, e));
    }

    static void Write(string file, string title, List<Process> p, List<TimeBlock> g, int cs)
    {
        Directory.CreateDirectory("sonuclar");
        var sb = new StringBuilder();

        sb.AppendLine(title);
        sb.AppendLine();

        sb.AppendLine("ZAMAN TABLOSU");
        for (int i = 0; i < g.Count; i++)
            sb.AppendLine("[" + g[i].start.ToString("0.000") + "] " + g[i].label + " [" + g[i].end.ToString("0.000") + "]");

        double sw = 0, st = 0;
        double mw = -1e9, mt = -1e9;

        for (int i = 0; i < p.Count; i++)
        {
            double t = p[i].completion - p[i].arrival;
            double w = t - p[i].burst;
            sw += w; st += t;
            if (w > mw) mw = w;
            if (t > mt) mt = t;
        }

        sb.AppendLine();
        sb.AppendLine("BEKLEME SURESI");
        sb.AppendLine("Ortalama: " + (sw / p.Count).ToString("0.000"));
        sb.AppendLine("Maksimum: " + mw.ToString("0.000"));

        sb.AppendLine();
        sb.AppendLine("TAMAMLANMA SURESI");
        sb.AppendLine("Ortalama: " + (st / p.Count).ToString("0.000"));
        sb.AppendLine("Maksimum: " + mt.ToString("0.000"));

        sb.AppendLine();
        sb.AppendLine("IS TAMAMLAMA SAYISI");
        double[] Ts = { 50, 100, 150, 200 };
        for (int i = 0; i < Ts.Length; i++)
        {
            int c = 0;
            for (int j = 0; j < p.Count; j++)
                if (p[j].completion <= Ts[i]) c++;
            sb.AppendLine("T=" + Ts[i] + " -> " + c);
        }

        double work = 0;
        for (int i = 0; i < p.Count; i++) work += p[i].burst;
        double total = g[g.Count - 1].end;

        sb.AppendLine();
        sb.AppendLine("CPU VERIMLILIGI");
        sb.AppendLine(((work / total) * 100).ToString("0.00") + "%");

        sb.AppendLine();
        sb.AppendLine("BAGLAM DEGISTIRME");
        sb.AppendLine("Toplam: " + cs);

        File.WriteAllText(file, sb.ToString());
    }

    // --------- ALGOS ---------

    static void FCFS(List<Process> input, string outFile)
    {
        var p = Copy(input);

        // basit siralama
        for (int i = 0; i < p.Count; i++)
            for (int j = i + 1; j < p.Count; j++)
                if (p[j].arrival < p[i].arrival)
                { var t = p[i]; p[i] = p[j]; p[j] = t; }

        var g = new List<TimeBlock>();
        double time = 0; int cs = 0; string last = "";

        for (int i = 0; i < p.Count; i++)
        {
            if (time < p[i].arrival)
            {
                Add(g, "IDLE", time, p[i].arrival);
                time = p[i].arrival; last = "IDLE";
            }
            if (last != "" && last != p[i].id)
            {
                cs++; Add(g, "CS", time, time + CS); time += CS;
            }
            Add(g, p[i].id, time, time + p[i].burst);
            time += p[i].burst; p[i].completion = time; last = p[i].id;
        }

        Write(outFile, "FCFS", p, g, cs);
    }

    static void SJF_NP(List<Process> input, string outFile)
    {
        var p = Copy(input);
        var g = new List<TimeBlock>();
        double time = 0; int done = 0; int cs = 0; string last = "";

        while (done < p.Count)
        {
            int pick = -1;
            for (int i = 0; i < p.Count; i++)
            {
                if (p[i].completion >= 0) continue;
                if (p[i].arrival > time) continue;
                if (pick == -1 || p[i].burst < p[pick].burst) pick = i;
            }

            if (pick == -1)
            {
                double na = 1e18;
                for (int i = 0; i < p.Count; i++)
                    if (p[i].completion < 0 && p[i].arrival < na) na = p[i].arrival;
                Add(g, "IDLE", time, na); time = na; last = "IDLE"; continue;
            }

            if (last != "" && last != p[pick].id)
            { cs++; Add(g, "CS", time, time + CS); time += CS; }

            Add(g, p[pick].id, time, time + p[pick].burst);
            time += p[pick].burst; p[pick].completion = time; done++; last = p[pick].id;
        }

        Write(outFile, "SJF (kesmesiz)", p, g, cs);
    }

    static void RR(List<Process> input, string outFile, double q)
    {
        var p = Copy(input);
        var g = new List<TimeBlock>();

        for (int i = 0; i < p.Count; i++)
            for (int j = i + 1; j < p.Count; j++)
                if (p[j].arrival < p[i].arrival)
                { var t = p[i]; p[i] = p[j]; p[j] = t; }

        Queue<Process> Q = new Queue<Process>();
        double time = 0; int cs = 0; string last = "";
        int idx = 0, fin = 0;

        while (fin < p.Count)
        {
            while (idx < p.Count && p[idx].arrival <= time) { Q.Enqueue(p[idx]); idx++; }

            if (Q.Count == 0)
            {
                Add(g, "IDLE", time, p[idx].arrival);
                time = p[idx].arrival; last = "IDLE"; continue;
            }

            var cur = Q.Dequeue();
            if (last != "" && last != cur.id)
            { cs++; Add(g, "CS", time, time + CS); time += CS; }

            double run = q; if (cur.remaining < run) run = cur.remaining;
            Add(g, cur.id, time, time + run);
            time += run; cur.remaining -= run; last = cur.id;

            while (idx < p.Count && p[idx].arrival <= time) { Q.Enqueue(p[idx]); idx++; }

            if (cur.remaining <= 0) { cur.completion = time; fin++; }
            else Q.Enqueue(cur);
        }

        Write(outFile, "Round Robin", p, g, cs);
    }

    static void Run(string csv)
    {
        var input = ReadCsv(csv);
        string name = Path.GetFileNameWithoutExtension(csv);

        FCFS(input, "sonuclar/fcfs_" + name + ".txt");
        SJF_NP(input, "sonuclar/sjf_kesmesiz_" + name + ".txt");
        RR(input, "sonuclar/rr_" + name + ".txt", 4.0);
    }

    static void Main(string[] args)
    {
        string path = args.Length > 0 ? args[0] : "odev1_case1.txt";
        Run(path);
        Console.WriteLine("bitti");
    }
}
