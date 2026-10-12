// Compiles every FishGame/* shader under Assets/_Project/Shaders and reports errors (C 7.20, plan T20.1). Owned by T20.
// Authority: none (editor-only validation). Fails closed: an in-memory canary shader with a deliberate HLSL error must be reported
// as broken, otherwise Run() returns an error instead of a false PASS.
// Compile path: ShaderUtil.GetShaderData(shader).ActiveSubshader -> pass.CompileVariant(stage, keywords, ShaderCompilerPlatform.D3D,
// BuildTarget.StandaloneWindows64) (the four-argument overload exists in 6000.6.5f1). It compiles the real HLSL, so the check does not
// depend on which graphics API the editor itself runs.
// Verified 2026-10-12 (T20 spike, Unity 6000.6.5f1, -batchmode -nographics through Tools/unity.ps1, so no unity.ps1 change was needed):
// the canary is reported, a temporary broken FishGame shader fails the run, and CompileVariant reports an error that exists only under one
// keyword set. FINDING: under -nographics ShaderData.ActiveSubshader is a FALLBACK subshader (index 2 of 3 for FishGame/Toon; its own
// subshader 0 holds the real passes), so using it would have reported a false PASS. Every subshader is compiled instead. A keyword in a
// set only takes effect on a pass that declares it with multi_compile or shader_feature (others ignore it).
// Hardening (T20 review): the canary must be reported by the CompileVariant path (its error exists only under _ADDITIONAL_LIGHTS and it has a
// FallBack like the project shaders); a shader whose own subshader 0 has zero successful compiles is an error; a fourth keyword set covers
// _MAIN_LIGHT_SHADOWS/_LIGHT_LAYERS/FOG_EXP; a project shader FallBack other than Off or URP's FallbackError is an error.
// Render-fix follow-up: VariantErrors counts the raw Error-severity messages of every CompileVariant call BEFORE the per-shader dedupe (the dedupe
// would otherwise hide a canary error that an earlier keyword set or ShaderHasError already reported); keyword set 4 covers the DepthNormals pass
// (_GBUFFER_NORMALS_OCT, _WRITE_RENDERING_LAYERS); the fallback subshaders (index > 0) are compiled with the empty keyword set only.
// Cost with FishGame/Toon alone (before the hardening and the DepthNormals pass): 5 passes (3 own + 2 fallback), 30 compiles, ms=2201 cold and about 30 warm (compile cache).
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Rendering;
using Debug = UnityEngine.Debug;

namespace FishGame.EditorTools
{
    public static class ShaderValidator
    {
        // Data. The undeclared identifier sits behind _ADDITIONAL_LIGHTS (a multi_compile keyword), so the canary reproduces the trap where an error
        // exists only under one keyword set: it is found only if the variant compile path covers that set. The FallBack mirrors the project shaders
        // (under -nographics the active subshader is the fallback one). Only the CompileVariant path counts for the canary (see CanaryIsReported).
        const string CanarySource =
            "Shader \"Hidden/FishGame/ValidatorCanary\" { SubShader { Pass { HLSLPROGRAM\n" +
            "#pragma vertex Vert\n" +
            "#pragma fragment Frag\n" +
            "#pragma multi_compile _ _ADDITIONAL_LIGHTS\n" +
            "float4 Vert(float4 p : POSITION) : SV_POSITION { return p; }\n" +
            "float4 Frag() : SV_Target {\n" +
            "#if defined(_ADDITIONAL_LIGHTS)\n" +
            "return fg_canary_undeclared;\n" +
            "#else\n" +
            "return float4(0, 0, 0, 0);\n" +
            "#endif\n" +
            "}\n" +
            "ENDHLSL } }\n" +
            "FallBack \"Hidden/Universal Render Pipeline/FallbackError\" }\n";

        // Fallbacks a project shader may name (or none). Anything else would pull in an unvalidated shader when the real subshaders are unsupported.
        const string AllowedFallback = "Hidden/Universal Render Pipeline/FallbackError";
        static readonly Regex FallbackLine = new Regex("^\\s*Fallback\\s+(?<name>\"[^\"]*\"|\\S+)", RegexOptions.IgnoreCase | RegexOptions.Multiline);

        static readonly Regex SubShaderKeyword = new Regex("(?<![A-Za-z0-9_])SubShader\\s*\\{", RegexOptions.IgnoreCase);

        static readonly string[][] KeywordSets =
        {
            new string[0],
            new[]
            {
                "_MAIN_LIGHT_SHADOWS_CASCADE", "_ADDITIONAL_LIGHTS", "_CLUSTER_LIGHT_LOOP", "_ADDITIONAL_LIGHT_SHADOWS", "_SHADOWS_SOFT",
                "_LIGHT_LAYERS", "_LIGHT_COOKIES", "FOG_EXP2", "_CASTING_PUNCTUAL_LIGHT_SHADOW", "INSTANCING_ON"
            },
            new[] { "_MAIN_LIGHT_SHADOWS_SCREEN", "_ADDITIONAL_LIGHTS_VERTEX", "FOG_LINEAR" },
            new[] { "_MAIN_LIGHT_SHADOWS", "_ADDITIONAL_LIGHTS", "_LIGHT_LAYERS", "FOG_EXP" },
            new[] { "_GBUFFER_NORMALS_OCT", "_WRITE_RENDERING_LAYERS" },
        };

        // The fallback subshaders (URP FallbackError) have no keywords worth covering: one compile each.
        static readonly string[][] EmptyKeywordSetOnly = { new string[0] };

        sealed class Counters
        {
            public int Passes;
            public int Compiles;
            public int VariantErrors;   // raw errors from the CompileVariant path (counted before the dedupe; not ShaderHasError / GetShaderMessages)
        }

        public static List<string> Run()
        {
            var errors = new List<string>();
            bool previousAsync = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try
            {
                var timer = Stopwatch.StartNew();
                if (!CanaryIsReported(errors)) return errors;

                var counters = new Counters();
                int shaders = 0;
                if (!AssetDatabase.IsValidFolder(AssetPaths.ShadersFolder))
                {
                    errors.Add("no shaders under " + AssetPaths.ShadersFolder);
                    return errors;
                }

                string[] guids = AssetDatabase.FindAssets("t:Shader", new[] { AssetPaths.ShadersFolder });
                if (guids.Length == 0)
                {
                    errors.Add("no shaders under " + AssetPaths.ShadersFolder);
                    return errors;
                }

                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    UnityEngine.Shader shader = AssetDatabase.LoadAssetAtPath<UnityEngine.Shader>(path);
                    if (shader == null) { errors.Add(path + ": could not load as a Shader"); continue; }
                    shaders++;
                    if (!shader.name.StartsWith("FishGame/", System.StringComparison.Ordinal))
                        errors.Add(path + ": shader name '" + shader.name + "' does not start with FishGame/");
                    CheckShader(shader, errors, counters);
                    CheckFallback(path, shader.name, errors);
                    CheckSingleSubShader(path, shader.name, errors);
                }

                timer.Stop();
                Debug.Log("FG SHADERS checked=" + shaders + " passes=" + counters.Passes + " compiles=" + counters.Compiles + " ms=" + timer.ElapsedMilliseconds);   // ms: canary + every shader, elapsed
                return errors;
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = previousAsync;
            }
        }

        [MenuItem("FishGame/Validate Shaders")]
        public static void RunMenu()
        {
            List<string> errors = Run();
            if (errors.Count == 0) { Debug.Log("FG SHADERS PASS"); return; }
            Debug.Log("FG SHADERS FAIL errors=" + errors.Count);
            foreach (string error in errors) Debug.Log("FG SHADERS ERROR " + error);
        }

        // Fail closed: the deliberately broken in-memory shader must produce at least one error from the variant compile path (CompileVariant), not only
        // from ShaderHasError, through the same routine the real shaders use. Its error exists only under _ADDITIONAL_LIGHTS.
        static bool CanaryIsReported(List<string> errors)
        {
            UnityEngine.Shader canary = ShaderUtil.CreateShaderAsset(CanarySource, false);
            try
            {
                var canaryErrors = new List<string>();
                var canaryCounters = new Counters();
                CheckShader(canary, canaryErrors, canaryCounters);
                if (canaryCounters.VariantErrors < 1)
                {
                    errors.Add("ShaderValidator: the variant compile path reported no error for the canary (errors=" + canaryErrors.Count +
                               "); shader errors are invisible in this mode");
                    return false;
                }
                Debug.Log("FG SHADERS canary ok: variantErrors=" + canaryCounters.VariantErrors + " " + string.Join(" | ", canaryErrors));
                return true;
            }
            finally
            {
                if (canary != null) UnityEngine.Object.DestroyImmediate(canary);
            }
        }

        // A project shader may declare no FallBack, FallBack Off, or URP's FallbackError. Reads the source text of the .shader file.
        static void CheckFallback(string assetPath, string shaderName, List<string> errors)
        {
            string text;
            try { text = System.IO.File.ReadAllText(BuilderUtil.Abs(assetPath)); }
            catch (System.Exception e) { errors.Add(shaderName + ": cannot read " + assetPath + " (" + e.Message + ")"); return; }
            foreach (Match match in FallbackLine.Matches(text))
            {
                string name = match.Groups["name"].Value.Trim('"');
                if (name.Equals("Off", System.StringComparison.OrdinalIgnoreCase) || name == AllowedFallback) continue;
                errors.Add(shaderName + ": FallBack \"" + name + "\" is not allowed (only Off or " + AllowedFallback + ")");
            }
        }

        // Exactly one SubShader block per project shader (source text, comments stripped). CheckShader gives only subshader 0 every keyword set and
        // requires it to compile; a second own SubShader would silently get the empty keyword set only (it would be treated like a fallback).
        static void CheckSingleSubShader(string assetPath, string shaderName, List<string> errors)
        {
            string text;
            try { text = System.IO.File.ReadAllText(BuilderUtil.Abs(assetPath)); }
            catch (System.Exception e) { errors.Add(shaderName + ": cannot read " + assetPath + " (" + e.Message + ")"); return; }
            int count = CountSubShaders(text);
            if (count != 1)
                errors.Add(shaderName + ": " + assetPath + " has " + count + " SubShader blocks (exactly 1 is required: only subshader 0 is compiled with every keyword set)");
        }

        // One left-to-right pass: a "//" line comment, a "/* */" block comment and a "..." string literal are each skipped as a unit, so a "/*" inside a
        // line comment or a quote inside a comment cannot start the wrong construct (the two-regex strip did get that wrong). Comments and string bodies
        // become a space, then SubShaderKeyword counts the remaining "SubShader {" tokens.
        internal static int CountSubShaders(string source)
        {
            if (string.IsNullOrEmpty(source)) return 0;
            var sb = new System.Text.StringBuilder(source.Length);
            int i = 0, n = source.Length;
            while (i < n)
            {
                char c = source[i];
                if (c == '/' && i + 1 < n && source[i + 1] == '/')
                {
                    while (i < n && source[i] != '\n' && source[i] != '\r') i++;
                    sb.Append(' ');
                }
                else if (c == '/' && i + 1 < n && source[i + 1] == '*')
                {
                    int end = source.IndexOf("*/", i + 2, System.StringComparison.Ordinal);
                    i = end < 0 ? n : end + 2;
                    sb.Append(' ');
                }
                else if (c == '"')
                {
                    i++;
                    while (i < n && source[i] != '"' && source[i] != '\n' && source[i] != '\r') i++;
                    if (i < n && source[i] == '"') i++;
                    sb.Append(' ');
                }
                else
                {
                    sb.Append(c);
                    i++;
                }
            }
            return SubShaderKeyword.Matches(sb.ToString()).Count;
        }

        // The same message at the same file and line is reported once per shader (the keyword sets and stages repeat it); the first label wins.
        static void CheckShader(UnityEngine.Shader shader, List<string> errors, Counters counters)
        {
            string shaderName = shader.name;
            var seen = new HashSet<string>();
            if (ShaderUtil.ShaderHasError(shader))
            {
                bool any = false;
                foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
                {
                    if (message.severity != ShaderCompilerMessageSeverity.Error) continue;
                    any = true;
                    if (seen.Add(message.severity + " " + Describe(message))) errors.Add(shaderName + ": " + Describe(message));
                }
                if (!any) errors.Add(shaderName + ": ShaderUtil.ShaderHasError is true but no error message was returned");
            }

            // Every subshader is compiled, not ShaderData.ActiveSubshader: under -batchmode -nographics the active subshader is a FALLBACK
            // subshader (index 2 for FishGame/Toon: its own subshader 0 has the three real passes), which would let a broken real pass pass.
            // The fallback subshaders (URP's FallbackError) compile cleanly, so compiling them too costs only a few extra compiles.
            ShaderData data = ShaderUtil.GetShaderData(shader);
            if (data == null || data.SubshaderCount == 0)
            {
                errors.Add(shaderName + ": no subshaders");
                return;
            }

            for (int s = 0; s < data.SubshaderCount; s++)
            {
                ShaderData.Subshader subshader = data.GetSubshader(s);
                if (subshader == null) { errors.Add(shaderName + ": subshader " + s + " is null"); continue; }
                int successes = 0;
                for (int p = 0; p < subshader.PassCount; p++)
                    successes += CheckPass(shaderName + " #" + s, subshader.GetPass(p), errors, counters, seen, s == 0);
                // The shader's own subshader (0) must really compile: if only the fallback subshaders compiled, a broken real pass would go unnoticed.
                if (s == 0 && successes == 0) errors.Add(shaderName + ": subshader 0 produced no successful compile");
            }
        }

        // Returns the number of successful variant compiles.
        static int CheckPass(string shaderName, ShaderData.Pass pass, List<string> errors, Counters counters, HashSet<string> seen, bool allKeywordSets)
        {
            counters.Passes++;
            int successes = 0;
            foreach (ShaderType stage in new[] { ShaderType.Vertex, ShaderType.Fragment })
            {
                if (!pass.HasShaderStage(stage)) continue;
                foreach (string[] keywords in allKeywordSets ? KeywordSets : EmptyKeywordSetOnly)
                {
                    string label = shaderName + " / " + pass.Name + " / " + stage + " [" + string.Join(" ", keywords) + "]";
                    counters.Compiles++;
                    ShaderData.VariantCompileInfo info;
                    try
                    {
                        info = pass.CompileVariant(stage, keywords, ShaderCompilerPlatform.D3D, BuildTarget.StandaloneWindows64);
                    }
                    catch (System.Exception e)
                    {
                        errors.Add(label + ": CompileVariant threw " + e.GetType().Name + ": " + e.Message);
                        counters.VariantErrors++;
                        continue;
                    }

                    ReportMessages(info.Messages, label, errors, seen);
                    int rawErrors = CountErrors(info.Messages);   // before the dedupe in ReportMessages
                    counters.VariantErrors += rawErrors;
                    if (!info.Success)
                    {
                        if (rawErrors == 0)
                        {
                            errors.Add(label + ": compile failed without an error message");
                            counters.VariantErrors++;
                        }
                    }
                    else if (info.ShaderData == null || info.ShaderData.Length == 0)
                    {
                        errors.Add(label + ": empty bytecode");
                        counters.VariantErrors++;
                    }
                    else
                    {
                        successes++;
                    }
                }
            }
            return successes;
        }

        static int CountErrors(ShaderMessage[] messages)
        {
            int count = 0;
            if (messages == null) return 0;
            foreach (ShaderMessage message in messages)
                if (message.severity == ShaderCompilerMessageSeverity.Error) count++;
            return count;
        }

        static void ReportMessages(ShaderMessage[] messages, string label, List<string> errors, HashSet<string> seen)
        {
            if (messages == null) return;
            foreach (ShaderMessage message in messages)
            {
                if (!seen.Add(message.severity + " " + Describe(message))) continue;
                if (message.severity == ShaderCompilerMessageSeverity.Error) errors.Add(label + ": " + Describe(message));
                else Debug.Log("FG SHADER WARN " + label + ": " + Describe(message));
            }
        }

        static string Describe(ShaderMessage message)
        {
            return message.message + " (" + message.file + ":" + message.line + ")";
        }
    }
}
