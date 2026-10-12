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
// Cost with FishGame/Toon alone: 5 passes (3 own + 2 fallback), 30 compiles, ms=2201 cold and about 30 warm (compile cache).
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEditor.Rendering;
using Debug = UnityEngine.Debug;

namespace FishGame.EditorTools
{
    public static class ShaderValidator
    {
        // Data, copied from the plan. The undeclared identifier makes the fragment stage fail to compile.
        const string CanarySource =
            "Shader \"Hidden/FishGame/ValidatorCanary\" { SubShader { Pass { HLSLPROGRAM\n" +
            "#pragma vertex Vert\n" +
            "#pragma fragment Frag\n" +
            "float4 Vert(float4 p : POSITION) : SV_POSITION { return p; }\n" +
            "float4 Frag() : SV_Target { return fg_canary_undeclared; }\n" +
            "ENDHLSL } } }\n";

        static readonly string[][] KeywordSets =
        {
            new string[0],
            new[]
            {
                "_MAIN_LIGHT_SHADOWS_CASCADE", "_ADDITIONAL_LIGHTS", "_CLUSTER_LIGHT_LOOP", "_ADDITIONAL_LIGHT_SHADOWS", "_SHADOWS_SOFT",
                "_LIGHT_LAYERS", "_LIGHT_COOKIES", "FOG_EXP2", "_CASTING_PUNCTUAL_LIGHT_SHADOW", "INSTANCING_ON"
            },
            new[] { "_MAIN_LIGHT_SHADOWS_SCREEN", "_ADDITIONAL_LIGHTS_VERTEX", "FOG_LINEAR" },
        };

        sealed class Counters
        {
            public int Passes;
            public int Compiles;
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
                }

                timer.Stop();
                Debug.Log("FG SHADERS checked=" + shaders + " passes=" + counters.Passes + " compiles=" + counters.Compiles + " ms=" + timer.ElapsedMilliseconds);
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

        // Fail closed: the deliberately broken in-memory shader must produce at least one error through the same routine the real shaders use.
        static bool CanaryIsReported(List<string> errors)
        {
            UnityEngine.Shader canary = ShaderUtil.CreateShaderAsset(CanarySource, false);
            try
            {
                var canaryErrors = new List<string>();
                CheckShader(canary, canaryErrors, new Counters());
                if (canaryErrors.Count == 0)
                {
                    errors.Add("ShaderValidator: the shader compiler reported no error for the canary; shader errors are invisible in this mode");
                    return false;
                }
                Debug.Log("FG SHADERS canary ok: " + string.Join(" | ", canaryErrors));
                return true;
            }
            finally
            {
                if (canary != null) UnityEngine.Object.DestroyImmediate(canary);
            }
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
                for (int p = 0; p < subshader.PassCount; p++)
                    CheckPass(shaderName + " #" + s, subshader.GetPass(p), errors, counters, seen);
            }
        }

        static void CheckPass(string shaderName, ShaderData.Pass pass, List<string> errors, Counters counters, HashSet<string> seen)
        {
            counters.Passes++;
            foreach (ShaderType stage in new[] { ShaderType.Vertex, ShaderType.Fragment })
            {
                if (!pass.HasShaderStage(stage)) continue;
                foreach (string[] keywords in KeywordSets)
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
                        continue;
                    }

                    ReportMessages(info.Messages, label, errors, seen);
                    if (!info.Success)
                    {
                        bool anyError = false;
                        foreach (ShaderMessage message in info.Messages)
                            if (message.severity == ShaderCompilerMessageSeverity.Error) anyError = true;
                        if (!anyError) errors.Add(label + ": compile failed without an error message");
                    }
                    else if (info.ShaderData == null || info.ShaderData.Length == 0)
                    {
                        errors.Add(label + ": empty bytecode");
                    }
                }
            }
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
