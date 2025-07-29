using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Obvious.Soap.Editor.Tests
{
    public class TypeCreationTest
    {
        private readonly string _path = "Assets/SoapGenerated/";

        [UnityTest]
        public IEnumerator CreateCustomType()
        {
            var typeTexts = new[] { "double", "Inventory", "_iLovePurple" };
            var namespaces = new[] { "Obvious", "Game", "" };
            var result = TryCreateClasses(typeTexts, namespaces);

            Assert.AreEqual(true, result);
            yield return new WaitForDomainReload();

            LogAssert.NoUnexpectedReceived();

            //Clean up
            var folderPath = _path.Substring(0, _path.Length - 1);
            FileUtil.DeleteFileOrDirectory(folderPath);
            AssetDatabase.Refresh();
            yield return null;
        }

        private bool TryCreateClasses(string[] typeTexts, string[] namespaces)
        {
            TextAsset newFile = null;
            var targetAmount = typeTexts.Length * 4 + 2; //2 classes that need to be created.
            var filesCreated = 0;

            for (int i = 0; i < typeTexts.Length; i++)
            {
                var typeText = typeTexts[i];
                var namespaceText = namespaces[i];
                var isIntrinsicType = SoapTypeUtils.IsIntrinsicType(typeText);
                if (!SoapTypeUtils.IsIntrinsicType(typeText))
                {
                    if (SoapEditorUtils.CreateClassFromTemplate("NewTypeTemplate.cs", namespaceText, typeText, _path,
                            out newFile))
                        filesCreated++;
                }

                if (SoapEditorUtils.CreateClassFromTemplate("ScriptableVariableTemplate.cs", namespaceText, typeText,
                        _path,
                        out newFile, isIntrinsicType, true))
                    filesCreated++;

                if (SoapEditorUtils.CreateClassFromTemplate("ScriptableEventTemplate.cs", namespaceText, typeText,
                        _path, out newFile,
                        isIntrinsicType, true))
                    filesCreated++;

                if (SoapEditorUtils.CreateClassFromTemplate("EventListenerTemplate.cs", namespaceText, typeText, _path,
                        out newFile,
                        isIntrinsicType, true))
                    filesCreated++;

                if (SoapEditorUtils.CreateClassFromTemplate("ScriptableListTemplate.cs", namespaceText, typeText, _path,
                        out newFile,
                        isIntrinsicType, true))
                    filesCreated++;
            }

            return filesCreated == targetAmount;
        }

        [Test]
        public void IsTypeNameValid()
        {
            var typeTexts = new[] { ";BadClass", "#698Spaceship", "1stClass" };
            foreach (var typeText in typeTexts)
            {
                var result = SoapTypeUtils.IsTypeNameValid(typeText);
                Assert.AreEqual(result, false);
            }
        }

        [Test]
        public void IsTypeNameBuiltIn()
        {
            var typeTexts = new[] { "double", "long", "string", "Transform", "Inventory" };
            var count = 0;
            foreach (var typeText in typeTexts)
            {
                var result = SoapTypeUtils.IsIntrinsicType(typeText);
                if (result)
                    count++;
            }

            Assert.AreEqual(count, 3);
        }

        [Test]
        public void CreateInvalidCustomType()
        {
            var typeTexts = new[] { ";BadClass", "Spaceship", "1stClass" };
            var namespaces = new[] { "Obvious.Test", " 79 Game", "_nerdi87-##" };
            var result = TryCreateClasses(typeTexts, namespaces);
            Assert.AreEqual(result, false);
        }
    }
}