// Editor tooling (T1). After every script compilation, writes Logs/FishGame/compile.txt so an
// editor-open session can read errors and warnings without the Console window. Not used at runtime.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace FishGame.EditorTools
{
    [InitializeOnLoad]
    public static class CompileLogHook
    {
        public const string RelativeLogPath = "Logs/FishGame/compile.txt";

        private static readonly List<string> Lines = new List<string>();
        private static int errorCount;
        private static int warningCount;

        static CompileLogHook()
        {
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
        }

        private static void OnCompilationStarted(object context)
        {
            Lines.Clear();
            errorCount = 0;
            warningCount = 0;
        }

        private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            if (messages == null)
            {
                return;
            }

            foreach (CompilerMessage message in messages)
            {
                string kind;
                if (message.type == CompilerMessageType.Error)
                {
                    kind = "Error";
                    errorCount++;
                }
                else if (message.type == CompilerMessageType.Warning)
                {
                    kind = "Warning";
                    warningCount++;
                }
                else
                {
                    continue;
                }

                string text = (message.message ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');
                Lines.Add(kind + " " + message.file + "(" + message.line + "," + message.column + "): " + text);
            }
        }

        private static void OnCompilationFinished(object context)
        {
            string status = errorCount == 0 ? "PASS" : "FAIL";
            string stamp = DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:ssK", CultureInfo.InvariantCulture);

            var output = new StringBuilder();
            output.Append("FG COMPILE ").Append(status)
                  .Append(" errors=").Append(errorCount)
                  .Append(" warnings=").Append(warningCount)
                  .Append(" at=").Append(stamp).Append('\n');
            foreach (string line in Lines)
            {
                output.Append(line).Append('\n');
            }

            try
            {
                string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", RelativeLogPath));
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                File.WriteAllText(fullPath, output.ToString(), new UTF8Encoding(false));
            }
            catch (Exception exception)
            {
                Debug.LogWarning("FG COMPILE log not written: " + exception.Message);
            }
        }
    }
}
