// ShaderValidator.CountSubShaders (T20 review leftover): the source scanner must skip comments and string literals as units.
// Authority: none (editor-only, pure string logic).
using FishGame.EditorTools;
using NUnit.Framework;

namespace FishGame.Tests.EditMode
{
    public class ShaderValidatorTests
    {
        [TestCase("Shader \"A\" { SubShader{ Pass { } } }", 1, TestName = "SubShader{ without a space")]
        [TestCase("Shader \"A\" { SubShader { Pass { } } }", 1, TestName = "SubShader { with a space")]
        [TestCase("Shader \"A\" {\n// SubShader {\n}", 0, TestName = "line comment hides it")]
        [TestCase("Shader \"A\" {\n/* SubShader { */\n}", 0, TestName = "block comment hides it")]
        [TestCase("Shader \"A\" { MySubShader { } }", 0, TestName = "MySubShader is not a SubShader")]
        [TestCase("Shader \"A\" { SubShader { } SubShader { } }", 2, TestName = "two real blocks count 2")]
        [TestCase("// note /*\nShader \"A\" { SubShader { } }\n// */\n", 1, TestName = "block-comment opener inside a line comment does not swallow code")]
        [TestCase("Shader \"A // not a comment\" { SubShader { } }", 1, TestName = "// inside a string literal is not a comment")]
        [TestCase("Shader \"SubShader {\" { SubShader { } }", 1, TestName = "SubShader { inside a string literal is not counted")]
        [TestCase("Shader \"A\" { /* a */ SubShader { } /* b */ }", 1, TestName = "block comments around a real block")]
        [TestCase("", 0, TestName = "empty source")]
        // Catches a scanner that resumes after an unterminated /* (for example IndexOf returning -1 treated as i + 2), which counts 2.
        [TestCase("Shader \"A\" { SubShader { } /* SubShader { }", 1, TestName = "unterminated block comment swallows the rest")]
        // Catches dropping RegexOptions.IgnoreCase: ShaderLab keywords are case-insensitive, so a lowercase second block would slip past the "exactly 1" rule.
        [TestCase("Shader \"A\" { subshader { } }", 1, TestName = "keyword is case-insensitive")]
        // Catches a regex narrowed to "SubShader ?\{", which counts 0 for a newline and tab before the brace.
        [TestCase("Shader \"A\" { SubShader\n\t{ } }", 1, TestName = "newline and tab before the brace")]
        public void CountSubShaders_Cases(string source, int expected)
        {
            Assert.That(ShaderValidator.CountSubShaders(source), Is.EqualTo(expected));
        }
    }
}
