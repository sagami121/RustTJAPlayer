using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using TjaPlayer.Models;
using TjaPlayer.Utils;

namespace TjaPlayer.Gameplay;

public class TjaParser
{
    private class PendingNote
    {
        public char NoteChar;
        public double Bpm;
        public Complex Scroll;
    }

    public static Score Parse(string filePath)
    {
        var score = new Score();
        score.FilePath = filePath;
        score.DirectoryPath = Path.GetDirectoryName(filePath) ?? "";

        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var lines = ExpandMacros(File.ReadAllLines(filePath, System.Text.Encoding.GetEncoding(932)));

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

        if (!string.IsNullOrEmpty(globalWave))
        {
            globalWave = CompanionFileFinder.FindFileName(score.DirectoryPath, Path.GetFileName(filePath), globalWave);
        }

        score.Charts = ParseTjaFile(lines, score.DirectoryPath, globalBpm, globalWave, globalOffset);
        
        if (score.Charts.Count > 0)
        {
            score.BaseBpm = globalBpm;
        }

        return score;
    }

    private static string[] ExpandMacros(string[] lines)
    {
        var macros = new Dictionary<string, List<string>>();
        var expandedLines = new List<string>();
        string? currentMacroName = null;
        List<string>? currentMacroLines = null;

        foreach (var line in lines)
        {
            string normalizedLine = line.Replace('　', ' ');
            if (normalizedLine.StartsWith("#MACRO_START", StringComparison.OrdinalIgnoreCase))
            {
                currentMacroName = normalizedLine.Substring(12).Trim();
                currentMacroLines = new List<string>();
            }
            else if (normalizedLine.StartsWith("#MACRO_END", StringComparison.OrdinalIgnoreCase))
            {
                if (currentMacroName != null && currentMacroLines != null)
                    macros[currentMacroName] = currentMacroLines;
                currentMacroName = null;
                currentMacroLines = null;
            }
            else if (normalizedLine.StartsWith("#MACRO_CALL", StringComparison.OrdinalIgnoreCase))
            {
                string macroName = normalizedLine.Substring(11).Trim();
                if (macros.TryGetValue(macroName, out var macroLines))
                    expandedLines.AddRange(macroLines);
            }
            else if (currentMacroLines != null)
            {
                currentMacroLines.Add(line);
            }
            else
            {
                expandedLines.Add(line);
            }
        }
        return expandedLines.ToArray();
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
            else if (line.StartsWith("#START", StringComparison.OrdinalIgnoreCase)) { parsingChart = true; currentCourseLines.Clear(); }
            else if (line.StartsWith("#END", StringComparison.OrdinalIgnoreCase))
            {
                parsingChart = false;
                charts[currentCourse] = ParseChart(currentCourseLines.ToArray(), directory, globalBpm, globalWave, globalOffset);
            }
            else if (parsingChart) currentCourseLines.Add(line);
        }
        return charts;
    }

    private class ParserState
    {
        public double CurrentBpm;
        public Complex ScrollValue = new Complex(1.0, 0.0);
        public double MeasureNum = 4.0;
        public double MeasureDen = 4.0;
        public bool IsGogo = false;
        public bool BarlineVisible = true;
        public Stack<bool> SkipStack = new Stack<bool>();
        public bool IsSkipping => SkipStack.Count > 0 && SkipStack.Peek();
    }

    private delegate void CommandHandler(string argument, ParserState state, ref double currentAbsTimeMs, TjaChart chart, ref Note? activeRollNote);

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

        var handlers = new Dictionary<string, CommandHandler>(StringComparer.OrdinalIgnoreCase);
        handlers["#BPMCHANGE"] = (string arg, ParserState s, ref double t, TjaChart c, ref Note? r) => s.CurrentBpm = CTExpression.Evaluate(arg, 0);
        handlers["#SCROLL"] = (string arg, ParserState s, ref double t, TjaChart c, ref Note? r) => {
            var parts = arg.Split(',');
            double magnitude = CTExpression.Evaluate(parts[0], 0);
            double angle = parts.Length > 1 ? CTExpression.Evaluate(parts[1], 0) * (Math.PI / 180.0) : 0.0;
            s.ScrollValue = Complex.FromPolarCoordinates(magnitude, angle);
        };
        handlers["#SCROLL_EXPR"] = (string arg, ParserState s, ref double t, TjaChart c, ref Note? r) => {
            var parts = arg.Split(',');
            double magnitude = CTExpression.Evaluate(parts[0], 0);
            double angle = parts.Length > 1 ? CTExpression.Evaluate(parts[1], 0) * (Math.PI / 180.0) : 0.0;
            s.ScrollValue = Complex.FromPolarCoordinates(magnitude, angle);
        };
        handlers["#BARLINEON"] = (string arg, ParserState s, ref double t, TjaChart c, ref Note? r) => s.BarlineVisible = true;
        handlers["#BARLINEOFF"] = (string arg, ParserState s, ref double t, TjaChart c, ref Note? r) => s.BarlineVisible = false;
        handlers["#MEASURE"] = (string arg, ParserState s, ref double t, TjaChart c, ref Note? r) => {
            var parts = arg.Split('/');
            s.MeasureNum = CTExpression.Evaluate(parts[0], 0);
            s.MeasureDen = CTExpression.Evaluate(parts[1], 0);
        };
        handlers["#GOGOSTART"] = (string arg, ParserState s, ref double t, TjaChart c, ref Note? r) => s.IsGogo = true;
        handlers["#GOGOEND"] = (string arg, ParserState s, ref double t, TjaChart c, ref Note? r) => s.IsGogo = false;
        handlers["#DELAY"] = (string arg, ParserState s, ref double t, TjaChart c, ref Note? r) => t += CTExpression.Evaluate(arg, 0) * 1000.0;
        handlers["#LYRIC"] = (string arg, ParserState s, ref double t, TjaChart c, ref Note? r) => c.Lyrics.Add(new LyricEvent { TimeMs = t, Text = arg });
        
        handlers["#IF"] = (string arg, ParserState s, ref double t, TjaChart c, ref Note? r) => {
            bool parentIsSkipping = s.IsSkipping;
            bool condition = CTExpression.Evaluate(arg, 0) != 0;
            s.SkipStack.Push(parentIsSkipping || !condition);
        };
        handlers["#ELSE"] = (string arg, ParserState s, ref double t, TjaChart c, ref Note? r) => {
            bool last = s.SkipStack.Pop();
            bool parentIsSkipping = s.IsSkipping;
            s.SkipStack.Push(parentIsSkipping || last);
        };
        handlers["#ENDIF"] = (string arg, ParserState s, ref double t, TjaChart c, ref Note? r) => s.SkipStack.Pop();

        foreach (var line in lines)
        {
            string cleanedLine = line;
            int commentIndex = line.IndexOf("//");
            if (commentIndex >= 0) cleanedLine = line.Substring(0, commentIndex);

            string trimmed = cleanedLine.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            if (trimmed.StartsWith("#"))
            {
                var spaceIndex = trimmed.IndexOf(' ');
                string command = spaceIndex >= 0 ? trimmed.Substring(0, spaceIndex) : trimmed;
                string argument = spaceIndex >= 0 ? trimmed.Substring(spaceIndex + 1).Trim() : "";

                bool isStructural = command.Equals("#IF", StringComparison.OrdinalIgnoreCase) || 
                                    command.Equals("#ELSE", StringComparison.OrdinalIgnoreCase) || 
                                    command.Equals("#ENDIF", StringComparison.OrdinalIgnoreCase);

                if (state.IsSkipping && !isStructural) continue;

                if (handlers.TryGetValue(command, out var handler))
                {
                    handler(argument, state, ref currentAbsTimeMs, chart, ref activeRollNote);
                }
                continue;
            }

            if (state.IsSkipping) continue;

            foreach (char c in trimmed)
            {
                if (c == ',')
                {
                    ProcessPendingNotes(chart, pendingNotes, ref currentAbsTimeMs, state, ref activeRollNote);
                }
                else if (c >= '0' && c <= '9')
                {
                    pendingNotes.Add(new PendingNote { NoteChar = c, Bpm = state.CurrentBpm, Scroll = state.ScrollValue });
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
            ScrollValue = state.ScrollValue,
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
                    ScrollValue = pNote.Scroll,
                    Bpm = pNote.Bpm,
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
                    ScrollValue = pNote.Scroll,
                    Bpm = pNote.Bpm,
                    IsGogo = state.IsGogo
                });
            }
        }
        currentAbsTimeMs += barDurationMs;
        pendingNotes.Clear();
    }
}
