using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CosmicShore.Engine;
using CosmicShore.Engine.Collections;
using CosmicShore.Engine.InputSystem;
using CosmicShore.Engine.UI;
using Xunit;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// The engine surface the real Assets/_Scripts needed to compile (CosmicShore.Live):
// JsonUtility, unmanaged FixedStrings, GeometryUtility, the Logger, the TMP input
// field and dropdown popup, SOAP implicit reads, Resources.LoadAll.
// ─────────────────────────────────────────────────────────────────────────────

public class JsonUtilityTests
{
    [Serializable]
    public class Inner { public int count = 3; public string label = "x"; }

    public enum Mode { A = 0, B = 7 }

    [Serializable]
    public class Payload
    {
        public int number;
        public float ratio;
        public string title;
        public Mode mode;
        public Vector3 position;
        public List<int> values = new();
        public Inner inner = new();
        [SerializeField] int hidden = 5;
        [NonSerialized] public int skipped = 9;
        public Dictionary<string, int> notSupported = new() { ["a"] = 1 };
        public int Hidden => hidden;
        public int Property { get; set; } = 4;
    }

    [Fact]
    public void RoundTrip_SerializesFieldsNotProperties_AndFollowsUnityRules()
    {
        var p = new Payload { number = 42, ratio = 0.25f, title = "hi", mode = Mode.B, position = new Vector3(1, 2, 3), values = { 1, 2 } };
        string json = JsonUtility.ToJson(p);

        Assert.Contains("\"number\":42", json);
        Assert.Contains("\"mode\":7", json);            // enums as their integer
        Assert.Contains("\"hidden\":5", json);          // [SerializeField] private
        Assert.DoesNotContain("skipped", json);         // [NonSerialized]
        Assert.DoesNotContain("notSupported", json);    // dictionaries are not serialized
        Assert.DoesNotContain("Property", json);        // properties never are
        Assert.Contains("\"position\":{\"x\":1,\"y\":2,\"z\":3}", json);

        var back = JsonUtility.FromJson<Payload>(json);
        Assert.Equal(42, back.number);
        Assert.Equal(0.25f, back.ratio);
        Assert.Equal(Mode.B, back.mode);
        Assert.Equal(new Vector3(1, 2, 3), back.position);
        Assert.Equal(new List<int> { 1, 2 }, back.values);
        Assert.Equal(5, back.Hidden);
    }

    [Fact]
    public void FromJsonOverwrite_OnlyTouchesFieldsPresent()
    {
        var target = new Payload { number = 1, title = "keep" };
        JsonUtility.FromJsonOverwrite("{\"number\":99}", target);
        Assert.Equal(99, target.number);
        Assert.Equal("keep", target.title);
    }

    [Fact]
    public void PrettyPrint_IndentsWithFourSpaces()
    {
        string json = JsonUtility.ToJson(new Inner(), true);
        Assert.Contains("\n    \"count\": 3", json);
    }

    public class Config : ScriptableObject { public int power = 2; public Config other; }

    [Fact]
    public void ObjectReferences_RoundTripByInstanceId_ForConfigCopies()
    {
        var a = ScriptableObject.CreateInstance<Config>();
        var b = ScriptableObject.CreateInstance<Config>();
        var linked = ScriptableObject.CreateInstance<Config>();
        a.power = 11;
        a.other = linked;

        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(a), b); // FullAutoAction's copy idiom
        Assert.Equal(11, b.power);
        Assert.Same(linked, b.other);
    }
}

public class FixedStringTests
{
    [Fact]
    public void FixedStrings_AreUnmanaged_WithTheOriginalSizes()
    {
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<FixedString32Bytes>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<FixedString64Bytes>());
        Assert.Equal(32, Unsafe.SizeOf<FixedString32Bytes>());
        Assert.Equal(64, Unsafe.SizeOf<FixedString64Bytes>());
        Assert.Equal(128, Unsafe.SizeOf<FixedString128Bytes>());
    }

    [Fact]
    public void Truncates_AtCodePointBoundary_AndCompares()
    {
        FixedString32Bytes s = new string('a', 28) + "é"; // 28 + 2 bytes > 29
        Assert.Equal(new string('a', 28), s.Value);
        FixedString32Bytes t = "pilot";
        Assert.Equal("pilot", (string)t);
        Assert.True(t == new FixedString32Bytes("pilot"));
        Assert.True(default(FixedString64Bytes).IsEmpty);
    }
}

public class GeometryUtilityTests
{
    [Fact]
    public void FrustumPlanes_AcceptBoxesInFront_RejectBoxesBehindAndBeside()
    {
        var proj = Matrix4x4.Perspective(60f, 1f, 0.3f, 1000f);
        var view = Matrix4x4.identity;
        view.m22 = -1f; // camera at origin looking down +z (the engine flips z into view space)
        var planes = GeometryUtility.CalculateFrustumPlanes(proj * view);
        Assert.Equal(6, planes.Length);

        Assert.True(GeometryUtility.TestPlanesAABB(planes, new Bounds(new Vector3(0, 0, 10), Vector3.one)));
        Assert.False(GeometryUtility.TestPlanesAABB(planes, new Bounds(new Vector3(0, 0, -10), Vector3.one)));
        Assert.False(GeometryUtility.TestPlanesAABB(planes, new Bounds(new Vector3(100, 0, 10), Vector3.one)));
        Assert.False(GeometryUtility.TestPlanesAABB(planes, new Bounds(new Vector3(0, 0, 2000), Vector3.one)));
    }
}

public class LoggerTests
{
    sealed class Capture : ILogHandler
    {
        public readonly List<(LogType, string)> lines = new();
        public void LogFormat(LogType logType, CosmicShore.Engine.Object context, string format, params object[] args)
            => lines.Add((logType, string.Format(format, args)));
        public void LogException(Exception exception, CosmicShore.Engine.Object context) => lines.Add((LogType.Exception, exception.Message));
    }

    [Fact]
    public void FilterLogType_KeepsSeverityAndAbove_AndTagsPrefix()
    {
        var cap = new Capture();
        var logger = new CosmicShore.Engine.Logger(cap) { filterLogType = LogType.Warning };
        logger.Log(LogType.Log, "dropped");
        logger.Log(LogType.Warning, "kept");
        logger.LogError("Tag", "boom");
        logger.LogException(new InvalidOperationException("ex"));

        Assert.Equal(new[] { (LogType.Warning, "kept"), (LogType.Error, "Tag: boom"), (LogType.Exception, "ex") }, cap.lines);
    }
}

public class SoapAndResourcesTests
{
    [Fact]
    public void ScriptableVariable_ReadsAsItsValue()
    {
        var v = ScriptableObject.CreateInstance<CosmicShore.Engine.Soap.IntVariable>();
        v.Value = 3;
        int i = v;
        float f = Mathf.Clamp(v, 1, 2);
        Assert.Equal(3, i);
        Assert.Equal(2f, f);
    }

    [Fact]
    public void LoadAll_ReturnsRegisteredAssetsUnderAFolder()
    {
        var a = ScriptableObject.CreateInstance<JsonUtilityTests.Config>();
        var b = ScriptableObject.CreateInstance<JsonUtilityTests.Config>();
        Resources.Register("LoadAllTest/One", a);
        Resources.Register("LoadAllTest/Two", b);
        Resources.Register("LoadAllTestOther/Three", ScriptableObject.CreateInstance<JsonUtilityTests.Config>());

        var found = Resources.LoadAll<ScriptableObject>("LoadAllTest");
        Assert.Equal(2, found.Length);
        Assert.Contains(a, found);
        Assert.Contains(b, found);
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public class InputSystemGlobalsCollection { public const string Name = "InputSystem globals"; }

[Collection(InputSystemGlobalsCollection.Name)]
public class TmpInputFieldTests : IDisposable
{
    readonly GameLoop loop;
    readonly Keyboard keyboard;
    readonly TMP_InputField field;
    readonly TextMeshProUGUI label;

    public TmpInputFieldTests()
    {
        loop = new GameLoop(nameof(TmpInputFieldTests));
        keyboard = CosmicShore.Engine.InputSystem.InputSystem.AddDevice<Keyboard>();
        var go = new GameObject("Field", typeof(RectTransform));
        var labelGo = new GameObject("Text", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        label = labelGo.AddComponent<TextMeshProUGUI>();
        field = go.AddComponent<TMP_InputField>();
        field.transition = Selectable.Transition.None;
        field.textComponent = label;
        loop.Tick(1f / 60f);
    }

    public void Dispose()
    {
        CosmicShore.Engine.InputSystem.InputSystem.RemoveDevice(keyboard);
        loop.Dispose();
    }

    void Type(string s) { foreach (var c in s) keyboard.RaiseTextInput(c); }

    [Fact]
    public void TypedCharacters_EditTheText_AndRaiseValueChanged()
    {
        var changes = new List<string>();
        field.onValueChanged.AddListener(changes.Add);
        field.ActivateInputField();
        Type("abc");
        Assert.Equal("abc", field.text);
        Assert.Equal("abc", label.text);
        Assert.Equal(new[] { "a", "ab", "abc" }, changes);
    }

    [Fact]
    public void CharacterLimit_AndIntegerValidation_RejectInput()
    {
        field.contentType = TMP_InputField.ContentType.IntegerNumber;
        field.characterLimit = 4;
        field.ActivateInputField();
        Type("-1a2.3456");
        Assert.Equal("-123", field.text);
    }

    [Fact]
    public void Password_MasksTheLabel_AndBackspaceDeletes()
    {
        field.contentType = TMP_InputField.ContentType.Password;
        field.onFocusSelectAll = false;
        field.ActivateInputField();
        Type("secret");
        Assert.Equal("******", label.text);

        keyboard[Key.Backspace].SetRaw(true);
        loop.Tick(1f / 60f);
        keyboard[Key.Backspace].SetRaw(false);
        loop.Tick(1f / 60f);
        Assert.Equal("secre", field.text);
    }

    [Fact]
    public void Enter_SubmitsAndEndsEditOnASingleLineField()
    {
        string submitted = null, ended = null;
        field.onSubmit.AddListener(s => submitted = s);
        field.onEndEdit.AddListener(s => ended = s);
        field.ActivateInputField();
        Type("go");
        keyboard[Key.Enter].SetRaw(true);
        loop.Tick(1f / 60f);
        Assert.Equal("go", submitted);
        Assert.Equal("go", ended);
        Assert.False(field.isFocused);
    }
}

public class TmpDropdownPopupTests : IDisposable
{
    readonly GameLoop loop = new(nameof(TmpDropdownPopupTests));

    public void Dispose() => loop.Dispose();

    [Fact]
    public void Show_ClonesTheTemplateItemPerOption_AndPickingOneSetsTheValue()
    {
        var canvasGo = new GameObject("Canvas", typeof(RectTransform));
        canvasGo.AddComponent<Canvas>();
        var ddGo = new GameObject("Dropdown", typeof(RectTransform));
        ddGo.transform.SetParent(canvasGo.transform, false);

        var template = new GameObject("Template", typeof(RectTransform));
        template.transform.SetParent(ddGo.transform, false);
        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(template.transform, false);
        var item = new GameObject("Item", typeof(RectTransform));
        item.transform.SetParent(content.transform, false);
        item.AddComponent<Toggle>();
        var itemLabelGo = new GameObject("Item Label", typeof(RectTransform));
        itemLabelGo.transform.SetParent(item.transform, false);
        var itemLabel = itemLabelGo.AddComponent<TextMeshProUGUI>();
        template.SetActive(false);

        var dd = ddGo.AddComponent<TMP_Dropdown>();
        dd.transition = Selectable.Transition.None;
        dd.template = (RectTransform)template.transform;
        dd.itemText = itemLabel;
        dd.AddOptions(new List<string> { "Any", "Rhino", "Manta" });
        loop.Tick(1f / 60f);

        dd.Show();
        Assert.True(dd.IsExpanded);
        var list = ddGo.transform.Find("Dropdown List");
        Assert.NotNull(list);
        var items = list.GetComponentsInChildren<Toggle>();
        Assert.Equal(3, items.Length);
        Assert.Equal("Manta", items[2].GetComponentInChildren<TextMeshProUGUI>().text);
        Assert.True(items[0].isOn);

        int changed = -1;
        dd.onValueChanged.AddListener(v => changed = v);
        items[2].isOn = true;
        Assert.Equal(2, dd.value);
        Assert.Equal(2, changed);
        Assert.False(dd.IsExpanded);
    }
}
