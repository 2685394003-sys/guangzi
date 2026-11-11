using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Guangzi.Tests
{
    /// <summary>
    /// 架构守卫测试（architecture guard）。
    /// 把 CODE_STYLE.md 里的硬规则变成会失败的测试——规范不靠自觉，靠红灯。
    ///
    /// 规则开关放在同目录的 style_rules.txt 里，改规则不用碰这个文件。
    /// 设计原则：只检查「违反了一定出 bug」和「能零误报检查」的项，
    /// 宁可少查，也不要制造一堆假警报让人直接无视规范。
    /// </summary>
    public class ArchitectureTests
    {
        private static string ScriptsRoot
        {
            get { return Path.Combine(Application.dataPath, "Scripts/Guangzi"); }
        }

        private static Dictionary<string, string> _rules;

        private static Dictionary<string, string> Rules
        {
            get
            {
                if (_rules != null) return _rules;

                _rules = new Dictionary<string, string>();
                string path = Path.Combine(ScriptsRoot, "Tests/style_rules.txt");
                if (!File.Exists(path)) return _rules;

                foreach (var raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;

                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;

                    _rules[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }

                return _rules;
            }
        }

        private static string Rule(string key, string fallback)
        {
            string value;
            return Rules.TryGetValue(key, out value) && !string.IsNullOrEmpty(value) ? value : fallback;
        }

        private static bool BoolRule(string key, bool fallback)
        {
            string value = Rule(key, null);
            if (string.IsNullOrEmpty(value)) return fallback;
            return value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        // ---------- 工具方法 ----------

        private static IEnumerable<string> CsFiles()
        {
            if (!Directory.Exists(ScriptsRoot)) yield break;
            foreach (var file in Directory.GetFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                yield return file;
            }
        }

        private static IEnumerable<string> CsFilesIn(string layer)
        {
            string dir = Path.Combine(ScriptsRoot, layer);
            if (!Directory.Exists(dir)) yield break;
            foreach (var file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                yield return file;
            }
        }

        private static string Relative(string fullPath)
        {
            return fullPath.Replace(ScriptsRoot, string.Empty).Replace('\\', '/').TrimStart('/');
        }

        /// <summary>
        /// 剥掉注释再检查。否则「Core 刻意不引用 UnityEngine」这种注释会把检查器自己骗过去。
        /// </summary>
        private static string StripComments(string text)
        {
            text = Regex.Replace(text, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            text = Regex.Replace(text, @"//.*?$", string.Empty, RegexOptions.Multiline);

            // 再剥掉 #if UNITY_EDITOR ... #endif 整块。
            // 理由：这块代码打包时根本不参与编译，里面引用 UnityEditor 是安全的
            // （Application.Quit 的编辑器分支就是这么写的，Unity 官方模板同款）。
            // 只处理最外层，不支持嵌套 #if —— 真需要嵌套就拆成两个方法，别硬塞。
            text = Regex.Replace(text,
                @"^[ \t]*#if[ \t]+UNITY_EDITOR[\s\S]*?^[ \t]*#endif[^\n]*$",
                string.Empty,
                RegexOptions.Multiline);

            return text;
        }

        // ---------- 硬规则 ----------

        [Test]
        public void Core_MustNotReferenceForbiddenNamespaces()
        {
            string[] forbidden = Rule("forbiddenInCore", "UnityEngine,UnityEditor").Split(',');
            var offenders = new List<string>();

            foreach (var file in CsFilesIn("Core"))
            {
                string code = StripComments(File.ReadAllText(file));

                foreach (var token in forbidden)
                {
                    string ns = token.Trim();
                    if (ns.Length == 0) continue;

                    if (Regex.IsMatch(code, @"\b" + Regex.Escape(ns) + @"\b"))
                    {
                        offenders.Add(Relative(file) + " → " + ns);
                    }
                }
            }

            Assert.That(offenders, Is.Empty,
                "Core 层必须零引擎引用（否则单元测试就跑不出编辑器），但发现：\n" + string.Join("\n", offenders));
        }

        [Test]
        public void Runtime_MustNotReferenceUnityEditor()
        {
            string[] forbidden = Rule("forbiddenInRuntime", "UnityEditor").Split(',');
            var offenders = new List<string>();

            foreach (var file in CsFilesIn("Runtime"))
            {
                string code = StripComments(File.ReadAllText(file));

                foreach (var token in forbidden)
                {
                    string ns = token.Trim();
                    if (ns.Length == 0) continue;

                    if (Regex.IsMatch(code, @"\b" + Regex.Escape(ns) + @"\b"))
                    {
                        offenders.Add(Relative(file) + " → " + ns);
                    }
                }
            }

            Assert.That(offenders, Is.Empty,
                "Runtime 层不允许碰 UnityEditor（打包会失败），编辑器代码请放 Editor/ 目录：\n" + string.Join("\n", offenders));
        }

        [Test]
        public void NoRegionDirectives()
        {
            if (!BoolRule("forbidRegion", true))
            {
                Assert.Ignore("规则已关闭（style_rules.txt: forbidRegion=false）");
            }

            var offenders = new List<string>();

            foreach (var file in CsFiles())
            {
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].TrimStart().StartsWith("#region"))
                    {
                        offenders.Add(Relative(file) + ":" + (i + 1));
                    }
                }
            }

            Assert.That(offenders, Is.Empty,
                "禁止 #region：需要用折叠来忍受的类，说明它太大了。发现：\n" + string.Join("\n", offenders));
        }

        [Test]
        public void EachLayer_MustHaveAsmdef()
        {
            var layers = new[] { "Core", "Runtime", "Tests" };
            var missing = new List<string>();

            foreach (var layer in layers)
            {
                string dir = Path.Combine(ScriptsRoot, layer);
                if (!Directory.Exists(dir)) continue;

                if (Directory.GetFiles(dir, "*.asmdef").Length == 0)
                {
                    missing.Add(layer + "/");
                }
            }

            Assert.That(missing, Is.Empty,
                "每层必须自带 .asmdef，否则会退化成 Assembly-CSharp，分层就白做了：\n" + string.Join("\n", missing));
        }

        // ---------- 软规则（用 Warn，不阻断） ----------

        [Test]
        public void SingleFile_ShouldNotExceedTypeBudget()
        {
            int max;
            if (!int.TryParse(Rule("maxTypesPerFile", "3"), out max)) max = 3;

            var over = new List<string>();

            foreach (var file in CsFiles())
            {
                string code = StripComments(File.ReadAllText(file));

                int count = Regex.Matches(code,
                    @"^[ \t]*(public|internal)[ \t]+(?:static[ \t]+|sealed[ \t]+|abstract[ \t]+|partial[ \t]+)*(class|struct|enum|interface)[ \t]+\w+",
                    RegexOptions.Multiline).Count;

                if (count > max)
                {
                    over.Add(Relative(file) + " 有 " + count + " 个类型（上限 " + max + "）");
                }
            }

            if (over.Count > 0)
            {
                // Unity 自带的 NUnit 没有 Assert.Warn（那是 NUnit 3 的 API），
                // 所以软规则用控制台警告：测试保持绿，但 Console 会亮黄，问题不会被藏起来。
                Debug.LogWarning("[ArchitectureTests] 文件里类型过多，建议按主类型拆分：\n" + string.Join("\n", over));
            }
        }

        [Test]
        public void PublicTypes_ShouldHaveXmlDoc()
        {
            if (!BoolRule("requireXmlDocOnPublicTypes", true))
            {
                Assert.Ignore("规则已关闭（style_rules.txt: requireXmlDocOnPublicTypes=false）");
            }

            var missing = new List<string>();

            foreach (var file in CsFiles())
            {
                var lines = File.ReadAllLines(file);

                for (int i = 0; i < lines.Length; i++)
                {
                    if (!Regex.IsMatch(lines[i],
                        @"^[ \t]*public[ \t]+(?:static[ \t]+|sealed[ \t]+|abstract[ \t]+|partial[ \t]+)*(class|struct|enum|interface)[ \t]+\w+"))
                    {
                        continue;
                    }

                    bool documented = false;
                    for (int j = i - 1; j >= 0; j--)
                    {
                        string prev = lines[j].Trim();
                        if (prev.Length == 0) continue;

                        documented = prev.StartsWith("///") || prev.StartsWith("[") || prev.EndsWith("*/");
                        break;
                    }

                    if (!documented)
                    {
                        missing.Add(Relative(file) + ":" + (i + 1) + "  " + lines[i].Trim());
                    }
                }
            }

            if (missing.Count > 0)
            {
                Debug.LogWarning("[ArchitectureTests] public 类型缺 /// 摘要：\n" + string.Join("\n", missing));
            }
        }
    }
}
