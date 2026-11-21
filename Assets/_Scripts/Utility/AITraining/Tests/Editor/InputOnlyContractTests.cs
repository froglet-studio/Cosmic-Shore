#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Utility.AITraining.Tests
{
    /// <summary>
    /// Source scan of the actuation path. A trained genome transfers only if
    /// the pilot that flies it in a match is the same stick writer that flew
    /// it in training. This fails when TrainingPilot, a policy, a sensor, the
    /// ditherer, or a deployment installer grows a pose or physics write.
    /// </summary>
    public class InputOnlyContractTests
    {
        static readonly string[] Banned =
        {
            @"\.Course\b",
            @"\bSetPose\b",
            @"\bTeleport\b",
            @"\bSetInitialSpeed\b",
            @"\bAddForce\b",
            @"\bAddTorque\b",
            @"\bMovePosition\b",
            @"\bMoveRotation\b",
            @"\blinearVelocity\b",
            @"\bangularVelocity\b",
            @"\bRigidbody\b",
            @"\.position\s*=",
            @"\.rotation\s*=",
            @"\.localPosition\s*=",
            @"\.localRotation\s*=",
            @"\.localScale\s*=",
            @"\.eulerAngles\s*=",
            @"\.forward\s*=",
            @"\.up\s*=",
            @"\.Translate\s*\(",
            @"\.LookAt\s*\(",
            @"\bSetPositionAndRotation\b",
            @"\.Warp\s*\(",
        };

        [Test]
        public void InputOnly_CommentMentioningTeleport_IsNotACall()
        {
            string comment = StripNonCode("/// do not Teleport or set Course\nvoid Ok() { var x = 1; }\n");
            Assert.IsNull(FirstHit(comment));

            string call = StripNonCode("void Bad(IVessel vessel) { vessel.Teleport(); }\n");
            Assert.AreEqual("Teleport", FirstHit(call));
        }

        [Test]
        public void InputOnly_PilotPoliciesAndSensors_DoNotCallBannedApis()
        {
            var root = TrainingRoot();
            var files = new List<string>
            {
                Path.Combine(root, "Pilot/TrainingPilot.cs"),
                Path.Combine(root, "Pilot/TrainingDeploymentService.cs"),
                Path.Combine(root, "Pilot/TrainingAIDeploymentBridge.cs"),
                Path.Combine(root, "Pilot/ArchiveDeployment.cs"),
                Path.Combine(root, "Core/IntensityDitherer.cs"),
            };
            files.AddRange(Directory.GetFiles(Path.Combine(root, "Policies"), "*.cs"));
            files.AddRange(Directory.GetFiles(Path.Combine(root, "Sensors"), "*.cs"));

            var seenPolicy = new HashSet<string>();
            foreach (var type in typeof(IDecisionPolicy).Assembly.GetTypes())
            {
                if (!type.IsClass || type.IsAbstract) continue;
                if (!typeof(IDecisionPolicy).IsAssignableFrom(type)) continue;
                string path = Path.Combine(root, "Policies", type.Name + ".cs");
                Assert.IsTrue(File.Exists(path),
                    type.Name + " implements IDecisionPolicy but has no Policies/" + type.Name + ".cs to scan.");
                seenPolicy.Add(path);
            }

            Assert.Greater(seenPolicy.Count, 0, "No IDecisionPolicy implementations were discovered.");

            var failures = new List<string>();
            foreach (var file in files)
            {
                Assert.IsTrue(File.Exists(file), "Missing actuation source: " + file);
                string code = StripNonCode(File.ReadAllText(file));
                string hit = FirstHit(code);
                if (hit != null)
                    failures.Add(Path.GetFileName(file) + " calls " + hit);
            }

            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void InputOnly_TrainingAndDeploy_ShareTheStickPath()
        {
            var root = TrainingRoot();
            string pilot = StripNonCode(File.ReadAllText(Path.Combine(root, "Pilot/TrainingPilot.cs")));
            StringAssert.Contains("_sensors[i].Sample", pilot);
            StringAssert.Contains("p.Decide", pilot);
            StringAssert.Contains("ApplyToInputStatus", pilot);
            StringAssert.Contains("_input.XSum", pilot);
            StringAssert.Contains("PerformShipControllerActions", pilot);
            StringAssert.Contains("StopShipControllerActions", pilot);

            string install = StripNonCode(File.ReadAllText(Path.Combine(root, "Pilot/ArchiveDeployment.cs")));
            StringAssert.Contains("StopAIPilot", install);
            StringAssert.Contains("enabled = false", install);
            StringAssert.Contains("LoadGenome", install);
            StringAssert.Contains("BeginEpisode", install);
            Assert.Less(install.IndexOf("StopAIPilot", System.StringComparison.Ordinal),
                install.IndexOf("BeginEpisode", System.StringComparison.Ordinal));
            Assert.Less(install.IndexOf("enabled = false", System.StringComparison.Ordinal),
                install.IndexOf("BeginEpisode", System.StringComparison.Ordinal));
            Assert.IsNull(FirstHit(install));

            foreach (var installer in new[]
            {
                "Pilot/TrainingDeploymentService.cs",
                "Pilot/TrainingAIDeploymentBridge.cs",
            })
            {
                string code = StripNonCode(File.ReadAllText(Path.Combine(root, installer)));
                StringAssert.Contains("ArchiveDeployment", code, installer);
                StringAssert.Contains("TrainingPilot", code, installer);
                Assert.IsFalse(code.Contains("ApplyToInputStatus"),
                    installer + " must not write sticks itself.");
                Assert.IsNull(FirstHit(code), installer);
            }
        }

        static string TrainingRoot()
        {
            return Path.Combine(Application.dataPath, "_Scripts/Utility/AITraining");
        }

        static string FirstHit(string code)
        {
            for (int i = 0; i < Banned.Length; i++)
            {
                var match = Regex.Match(code, Banned[i]);
                if (match.Success) return match.Value;
            }
            return null;
        }

        /// <summary>
        /// Drops comments and string literals so the contract text can name
        /// the banned calls without tripping the scan.
        /// </summary>
        internal static string StripNonCode(string src)
        {
            var sb = new StringBuilder(src.Length);
            for (int i = 0; i < src.Length; i++)
            {
                if (i + 1 < src.Length && src[i] == '/' && src[i + 1] == '/')
                {
                    while (i < src.Length && src[i] != '\n') i++;
                    continue;
                }
                if (i + 1 < src.Length && src[i] == '/' && src[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < src.Length && !(src[i] == '*' && src[i + 1] == '/')) i++;
                    if (i + 1 < src.Length) i++;
                    continue;
                }
                if (src[i] == '@' && i + 1 < src.Length && src[i + 1] == '"')
                {
                    i += 2;
                    while (i < src.Length)
                    {
                        if (src[i] == '"')
                        {
                            if (i + 1 < src.Length && src[i + 1] == '"') { i += 2; continue; }
                            break;
                        }
                        i++;
                    }
                    continue;
                }
                if (src[i] == '"')
                {
                    i++;
                    while (i < src.Length && src[i] != '"')
                    {
                        if (src[i] == '\\' && i + 1 < src.Length) i++;
                        i++;
                    }
                    continue;
                }
                sb.Append(src[i]);
            }
            return sb.ToString();
        }
    }
}
#endif
