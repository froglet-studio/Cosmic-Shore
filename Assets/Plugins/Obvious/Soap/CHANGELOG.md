## [2.7.0] - 2024-03-02
- Added Description for all Soap scriptable objects 
- Added custom inspector for runtime scene objects variables
- Added missing documentation (PlayerVariable, PreviousValue and description)
- Fixed error with SoapSettings sometimes null
- Fixed Namespace wrapping for generated classes

## [2.6.1] - 2024-02-14
- Added Vector2Int as part of core
- Removed unnecessary [Serializable] attributes on abstract classes
- Fixed [SerializeField] for List<T>

## [2.6.0] - 2024-02-12
- Added TryAdd methods for List
- Added ScriptableList<T> inherits from IList<T>
- Added Option to override destination path when creating new type
- Added Optional namespace when creating new type
- Added nameof(T) for generated type to support refactoring
- Added Find references of Soap SO (in scenes and assets)
- Fixed Dynamic space for Vector2 and 3 in variable property drawer
- Changed Moved Soap into Plugins folder
- Changed Menu name to Obvious Game
- Changed ScriptableList Add() does not check for already added items anymore 

## [2.5.0] - 2024-01-12
- Added Scriptable list use a hashset for gain in performance 
- Added FAQ to Soap Window
- Added Option to override Save ID (GUID) of a scriptable variable 
- Added Delay for EventListeners
- Added Categories
- Added Integration with Playmaker
- Added ForEach support for scriptable Lists
- Changed Soap Wizard's layout (using more icons for better UX)

## [2.4.0] - 2023-12-20
- Added Unit Test for Type Creation
- Added Scene Documentation Button
- Added manual naming on creation
- Added settings in soap Window
- Added option to generate class from Create New Wizard
- Added Unit Tests for Type creation
- Moved Scene documentations in Documentation folder
- Changed min value for clamp from minValue to 0 (for floats and ints)

## [2.3.1] - 2023-11-09
- Fix Nested Folder for Soap Wizard
- Fix Pascal Case name of the variable in the name but not type
- Added Unit Test for Type Creation

## [2.3.0] - 2023-11-09
- Updated package to latest LTS version of Unity
- Updated Soap Icon
- Added documentation in code and tooltips
- Added Ratio property for IntVariable and FloatVariable
- Added Min and Max property for IntVariable and FloatVariable

## [2.2.1] - 2023-09-07
- Revert SerializeField for variable and lists
- Fixed compatibility with Fast Script Reload
- Fixed error when adding component
- Added compatibility with Odin Inspector

## [2.2.0] - 2023-08-07
- Fixed use SerializeReference instead of SerializeField for variable and lists
- Fixed not use Linq for IsEmpty property of lists

## [2.1.0] - 2023-06-18
- Added BindToInputField Component
- Fixed error when displaying non serializable classes
- Fixed overriding all custom property drawers
- Removed PlayModeResetter 
- Removed FastPlayMode Scene

## [2.0.0] - 2023-05-23
- Added ScriptableVariable Default value field
- Added Icons for Windows
- Added ScriptableVariables now reset when exiting PlayMode
- Fixed SoapSettings created multiple times
- Fixed cached editor
- Fixed null refs due to repaint logic
- Fixed various colors
- Fixed naming standards of EventListeners
- Removed PlayModeResetter logic
- Removed ScriptableVariable InitialValue field

## [1.5.3] - 2023-05-10
- Fixed Custom editor performances issues
- Fixed expanding ScriptableEvents from EventListeners
- Added Custom property drawer for each type
- Added Raise button for ScriptableEvent property drawer
- Added Count label for ScriptableList property drawer

## [1.5.2] - 2023-05-02
- Fixed Editor namespace compile error 
- Fixed Editor code preventing builds

## [1.5.1] - 2023-04-29
- Fixed create SO at selected folder instead of predefined path
- Fixed create new Types at selected folder instead of predefined path
- Fixed settings can only be modified from Soap Window
- Fixed Editor namespace
- Fixed naming of variable reference created from inspector button
- Added Soap Window
- Added MinMax property for IntVariable and FloatVariable

## [1.5.0] - 2023-04-08
- Fixed null ref debugging events
- Fixed InitialValue is public
- Fixed Modifying the value of a SV by code shows in Version Control
- Added create button from classes (SoapPropertyDrawer)
- Added Embedded inspector for all Soap SO (SoapPropertyDrawer)
- Added SoapSettings
- Added ScriptableVariable Display Mode (Default & Minimal)
- Added Search bar in SoapWizard
- Added debug for ScriptableEventNoParam

## [1.4.1] - 2023-02-24
- Added favorite option in Soap Wizard
- Fixed path saving in Soap Wizard
- Fixed Unit Tests under Editor Folder
- Fixed Events raise button disabled when not in play mode
 
## [1.4.0] - 2023-01-30
- Added Soap Wizard
- Added Events Debug Window visual
- Added file name for ScriptableList and Events
- Fixed base type of ScriptableVariableDrawer
- Fixed Rename enum CustomVariableType to follow C# standards

## [1.3.3] - 2023-01-19
- Added AddRange and RemoveRange methods for ScriptableList
- Removed parameter of the event OnItemCountChanged
- Fixed support for multiple instances of PlayModeResetter
- Fixed Debug Logs are now being displayed when subscribing to OnRaised in ScriptableEvents by code. 

## [1.3.2] - 2022-12-17
- Added Implicit operator for ScriptableVariables
- Added Version number on documentation
- Added ResetToInitialValue Button on ScriptableVariables
- Fixed Obstacle prefab structure
- Fixed icons are now being used
- Fixed minor bugs and project folders
- Fixed OnSceneLoaded is now protected

## [1.3.1] - 2022-11-20
- Added VariableReference base class
- Added IsEmpty property on ScriptableLists
- Added Undo on Bindings custom inspectors
- Added BindRendererColor
- Added BindGraphicColor
- Removed BindImageColor and ColorChanger
- Updated documentation
- Updated BindComparisonToUnityEvent

## [1.3.0] - 2022-11-09
- Added custom icons
- Added option to subscribe to ScriptableEvents by code	
- Fix error when modifying Bindings component at runtime

## [1.2.1] - 2022-10-29
- Added IsClamped bool to clamping for IntVariables and FloatVariables
- Added IsClamped bool for BindText/TextMeshPro components
- Added Discord and Asset store link buttons in Scenes
- Fix ScriptableList Drawing GameObjects
- Fix ScriptableVariable Guid serialization and generation


## [1.2.0] - 2022-10-20
- Added Min & Max clamping for IntVariables and FloatVariables
- Added Min & Max clamping for BindText/TextMeshPro components
- Added Uid as PlayerPrefs key for ScriptableVariables
- Added warning in PlayModeResetter
- Added Discord and Asset store link buttons in Scenes
- Updated custom inspector for ScriptableLists
- Fixed various custom inspectors

## [1.1.0] - 2022-10-10
- Fixed BindText and BindTextMeshPro when binding to a StringVariable
- Added default GameObjectScriptableEvent and EventListenerGameObject
- Added package.json and changelog
- Updated custom inspector for SO variables
- Fixed various bug 
- Updated documentation
- Uploaded with 2019.4

## [1.0.0] - 2022-09-27
- Initial Release
