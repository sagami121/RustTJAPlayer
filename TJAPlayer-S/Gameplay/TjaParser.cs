using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TjaPlayer.Models;

namespace TjaPlayer.Gameplay;

public class TjaParser
{
    private class PendingNote
    {
        public char NoteChar;
        public double Bpm;
        public double Scroll;
    }

    public static Score Parse(string filePath)
    {
        var score = new Score();
        score.FilePath = filePath;
        score.DirectoryPath = Path.GetDirectoryName(filePath) ?? "";

        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var lines = File.ReadAllLines(filePath, System.Text.Encoding.GetEncoding("shift-jis"));

        double globalBpm = 130.0;
        string globalWave = "";
        double globalOffset = 0.0;

        foreach (var line in lines)
        {
            if (line.StartsWith("TITLE:", StringComparison.OrdinalIgnoreCase)) score.Title = line.Substring(6).Trim();
            else if (line.StartsWith("SUBTITLE:", StringComparison.OrdinalIgnoreCase)) score.Subtitle = line.Substring(9).Trim();
            else if (line.StartsWith("ARTIST:", StringComparison.OrdinalIgnoreCase)) score.Artist = line.Substring(7).Trim();
            else if (line.StartsWith("BPM:", StringComparison.OrdinalIgnoreCase))
            {
                double.TryParse(line.Substring(4).Trim(), out globalBpm);
            }
            else if (line.StartsWith("WAVE:", StringComparison.OrdinalIgnoreCase))
            {
                globalWave = line.Substring(5).Trim();
            }
            else if (line.StartsWith("OFFSET:", StringComparison.OrdinalIgnoreCase))
            {
                if (double.TryParse(line.Substring(7).Trim(), out double offset))
                {
                    globalOffset = offset * 1000.0;
                }
            }
            else if (line.StartsWith("#START", StringComparison.OrdinalIgnoreCase)) break;
        }

        score.Charts = ParseTjaFile(lines, score.DirectoryPath, globalBpm, globalWave, globalOffset);
        
        // BaseBpmの決定 (最初のチャートのBPMを採用)
        if (score.Charts.Count > 0)
        {
            score.BaseBpm = globalBpm;
        }

        return score;
    }

    private static Dictionary<string, TjaChart> ParseTjaFile(string[] lines, string directory, double globalBpm, string globalWave, double globalOffset)
    {
        var charts = new Dictionary<string, TjaChart>();
        string currentCourse = "Oni";
        List<string> currentCourseLines = new();
        bool parsingChart = false;

        foreach (var line in lines)
        {
            if (line.StartsWith("COURSE:", StringComparison.OrdinalIgnoreCase)) currentCourse = line.Substring(7).Trim();
            else if (line.StartsWith("#START", StringComparison.OrdinalIgnoreCase)) { parsingChart = true; currentCourseLines.Clear(); currentCourseLines.Add(line); }
            else if (line.StartsWith("#END", StringComparison.OrdinalIgnoreCase))
            {
                parsingChart = false;
                currentCourseLines.Add(line);
                charts[currentCourse] = ParseChart(currentCourseLines.ToArray(), directory, globalBpm, globalWave, globalOffset);
            }
            else if (parsingChart) currentCourseLines.Add(line);
        }
        return charts;
    }

    private class ParserState
    {
        public double CurrentBpm;
        public double ScrollX = 1.0;
        public double ScrollY = 0.0;
        public double MeasureNum = 4.0;
        public double MeasureDen = 4.0;
        public bool IsGogo = false;
        public bool BarlineVisible = true;
    }

    public static TjaChart ParseChart(string[] lines, string directory, double globalBpm, string globalWave, double globalOffset)
    {
        var chart = new TjaChart();
        chart.DirectoryPath = directory;
        chart.AudioFileName = globalWave;
        chart.WaveOffsetMs = globalOffset;
        
        var state = new ParserState { CurrentBpm = globalBpm };
        double currentAbsTimeMs = 0.0;

        List<PendingNote> pendingNotes = new();
        Note? activeRollNote = null;

        foreach (var line in lines)
        {
            // コメント除去 (TJAPlayer3 準拠)
            string cleanedLine = line;
            int commentIndex = line.IndexOf("//");
            if (commentIndex >= 0)
            {
                cleanedLine = line.Substring(0, commentIndex);
            }

            string trimmed = cleanedLine.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            if (trimmed.StartsWith("#"))
            {
                if (trimmed.StartsWith("#BPMCHANGE", StringComparison.OrdinalIgnoreCase))
                {
                    state.CurrentBpm = double.Parse(trimmed.Substring(10).Trim());
                }
                else if (trimmed.StartsWith("#SCROLL", StringComparison.OrdinalIgnoreCase))
                {
                    var val = trimmed.Substring(7).Trim();
                    state.ScrollX = double.Parse(val);
                }
                else if (trimmed.StartsWith("#MEASURE", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmed.Substring(8).Trim().Split('/');
                    state.MeasureNum = double.Parse(parts[0]);
                    state.MeasureDen = double.Parse(parts[1]);
                }
                else if (trimmed.StartsWith("#GOGOSTART", StringComparison.OrdinalIgnoreCase)) state.IsGogo = true;
                else if (trimmed.StartsWith("#GOGOEND", StringComparison.OrdinalIgnoreCase)) state.IsGogo = false;
                else if (trimmed.StartsWith("#DELAY", StringComparison.OrdinalIgnoreCase))
                {
                    double delay = double.Parse(trimmed.Substring(6).Trim());
                    currentAbsTimeMs += delay * 1000.0;
                }
                continue;
            }

            foreach (char c in trimmed)
            {
                if (c == ',')
                {
                    ProcessPendingNotes(chart, pendingNotes, ref currentAbsTimeMs, state, ref activeRollNote);
                }
                else if (c >= '0' && c <= '9')
                {
                    pendingNotes.Add(new PendingNote { NoteChar = c, Bpm = state.CurrentBpm, Scroll = state.ScrollX });
                }
            }
        }

        if (pendingNotes.Count > 0)
        {
            ProcessPendingNotes(chart, pendingNotes, ref currentAbsTimeMs, state, ref activeRollNote);
        }

        return chart;
    }

    private static void ProcessPendingNotes(TjaChart chart, List<PendingNote> pendingNotes, ref double currentAbsTimeMs, ParserState state, ref Note? activeRollNote)
    {
        double effectiveBpm = state.CurrentBpm > 0 ? state.CurrentBpm : 130.0;
        double barDurationMs = (60000.0 / effectiveBpm) * 4.0 * (state.MeasureNum / state.MeasureDen);
        int totalNotes = pendingNotes.Count;
        double timePerNote = totalNotes > 0 ? barDurationMs / totalNotes : 0;

        chart.Barlines.Add(new Barline 
        { 
            TimeMs = currentAbsTimeMs, 
            ScrollFactorX = state.ScrollX, 
            ScrollFactorY = state.ScrollY,
            Bpm = effectiveBpm,
            IsVisible = state.BarlineVisible 
        });

        for (int i = 0; i < totalNotes; i++)
        {
            var pNote = pendingNotes[i];
            double noteTime = currentAbsTimeMs + (i * timePerNote);

            if (pNote.NoteChar == '0') continue;

            if (pNote.NoteChar == '5' || pNote.NoteChar == '6' || pNote.NoteChar == '8')
            {
                activeRollNote = new Note 
                { 
                    Type = (NoteType)(pNote.NoteChar - '0'), 
                    TimeMs = noteTime, 
                    ScrollFactorX = pNote.Scroll, // pNoteの値を使用
                    ScrollFactorY = state.ScrollY,
                    Bpm = pNote.Bpm, // pNoteの値を使用
                    IsGogo = state.IsGogo
                };
                chart.Notes.Add(activeRollNote);
            }
            else if (pNote.NoteChar == '7' && activeRollNote != null)
            {
                activeRollNote.EndTimeMs = noteTime;
                activeRollNote = null;
            }
            else if (pNote.NoteChar >= '1' && pNote.NoteChar <= '4')
            {
                chart.Notes.Add(new Note 
                { 
                    Type = (NoteType)(pNote.NoteChar - '0'), 
                    TimeMs = noteTime, 
                    ScrollFactorX = pNote.Scroll, // pNoteの値を使用
                    ScrollFactorY = state.ScrollY,
                    Bpm = pNote.Bpm, // pNoteの値を使用
                    IsGogo = state.IsGogo
                });
            }
        }
        currentAbsTimeMs += barDurationMs;
        pendingNotes.Clear();
    }
}