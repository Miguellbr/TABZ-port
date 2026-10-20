using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TabzMinimal
{
    /// In-game log. Captures every Unity log, exception and stack trace so it can be copied on the phone.
    public static class TabzLog
    {
        private static readonly object Gate = new object();
        private static readonly List<string> Lines = new List<string>();
        private static bool hooked;
        public static int ErrorCount;

        public static void Hook()
        {
            if (hooked) return;
            hooked = true;
            Application.logMessageReceivedThreaded += OnUnityLog;
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Add("UNHANDLED EXCEPTION: " + e.ExceptionObject);
        }

        private static void OnUnityLog(string condition, string stack, LogType type)
        {
            string line = "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " + type + ": " + condition;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                ErrorCount++;
                if (!string.IsNullOrEmpty(stack)) line += "\n" + stack.TrimEnd();
            }
            Add(line);
        }

        public static void Add(string line)
        {
            lock (Gate)
            {
                Lines.Add(line);
                if (Lines.Count > 5000) Lines.RemoveRange(0, 1000);
            }
        }

        public static string Dump()
        {
            lock (Gate) { return string.Join("\n", Lines.ToArray()); }
        }

        public static string Tail(int count)
        {
            lock (Gate)
            {
                int start = Math.Max(0, Lines.Count - count);
                return string.Join("\n", Lines.GetRange(start, Lines.Count - start).ToArray());
            }
        }

        public static string Save()
        {
            string path = Path.Combine(Application.persistentDataPath, "tabz-log.txt");
            try { File.WriteAllText(path, Dump()); }
            catch (Exception e) { return "save failed: " + e.Message; }
            return path;
        }
    }
}
