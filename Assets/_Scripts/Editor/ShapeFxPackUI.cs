using UnityEngine;
using UnityEditor;

public class ShapeFxPackUI : ShaderGUI
{

	MaterialEditor editor;
	MaterialProperty[] properties;
    bool TargetMode;
    bool MatCap;

    //get preperties function
    MaterialProperty FindProperty (string name) 
	{
		return FindProperty(name, properties);
	}
	//

	////
	static GUIContent staticLabel = new GUIContent();
	static GUIContent MakeLabel (MaterialProperty property, string tooltip = null) 
	{
		staticLabel.text = property.displayName;
		staticLabel.tooltip = tooltip;
		return staticLabel;
	}
	////

	public override void OnGUI (MaterialEditor editor, MaterialProperty[] properties) 
	{
		this.editor = editor;
		this.properties = properties;
		DoMain();

	}


	// GUI FUNCTION	
	void DoMain() 
	{
		//--- Logo
		Texture2D myGUITexture  = (Texture2D)Resources.Load("ShapesFX_PACK");
		GUILayout.Label(myGUITexture,EditorStyles.centeredGreyMiniLabel);

		//LABELS
		GUILayout.Label("/---------------/ SHAPES FX PACK /---------------/", EditorStyles.centeredGreyMiniLabel);
		GUILayout.Label("DIFFUSE ", EditorStyles.helpBox);

        // Matcap Mode
        //GUILayout.Label("MatCap MODE", EditorStyles.helpBox);
        MaterialProperty _MatCapSwitch = FindProperty("_MatCapSwitch");
        editor.ShaderProperty(_MatCapSwitch, MakeLabel(_MatCapSwitch));

        float MC = _MatCapSwitch.floatValue;
        if (MC == 0)
        {
         
            // get properties
            MaterialProperty _DiffuseMap = ShaderGUI.FindProperty("_DiffuseMap", properties);
            //Add to GUI
            editor.TexturePropertySingleLine(MakeLabel(_DiffuseMap, "FFace Map"), _DiffuseMap);
        }

        if (MC == 1)
        {

            // get properties
            MaterialProperty _FrontFace_Diffuse_map = ShaderGUI.FindProperty("_FrontFace_Diffuse_map", properties);

            //Add to GUI
            editor.TexturePropertySingleLine(MakeLabel(_FrontFace_Diffuse_map, "FFace Map"), _FrontFace_Diffuse_map, FindProperty("_FrontFace_Color"));
            //editor.TextureScaleOffsetProperty (_FrontFace_Diffuse_map);

            MaterialProperty _FrontFace_Intensity = FindProperty("_FrontFace_Intensity");
            editor.ShaderProperty(_FrontFace_Intensity, MakeLabel(_FrontFace_Intensity));

        }

        /*
		//--------------------

		//LABELS
		GUILayout.Label("BACK FACE", EditorStyles.helpBox);

		// get properties
		MaterialProperty _BackFace_Diffuse_map = ShaderGUI.FindProperty("_BackFace_Diffuse_map", properties);

		//Add to GUI
		editor.TexturePropertySingleLine(MakeLabel(_BackFace_Diffuse_map,"BFace Map"), _BackFace_Diffuse_map,FindProperty("_BackFace_Color"));
		//editor.TextureScaleOffsetProperty (_BackFace_Diffuse_map);

		MaterialProperty _BackFace_Intensity = FindProperty("_BackFace_Intensity");
		editor.ShaderProperty(_BackFace_Intensity, MakeLabel(_BackFace_Intensity));


		//--------------------
        */
		//LABELS
		GUILayout.Label("OUTLINE", EditorStyles.helpBox);

		// get properties
		MaterialProperty _OutlineTex = ShaderGUI.FindProperty("_OutlineTex", properties);

        //Add to GUI
        editor.TexturePropertySingleLine(MakeLabel(_OutlineTex,"Outline Map"), _OutlineTex,FindProperty("_Outline_Color"));
		//editor.TextureScaleOffsetProperty (_OutlineTex);

		MaterialProperty _Outline_Opacity = FindProperty("_Outline_Opacity");
		editor.ShaderProperty(_Outline_Opacity, MakeLabel(_Outline_Opacity));

        MaterialProperty _DefaultOutlineOpacity = FindProperty("_DefaultOutlineOpacity");
        editor.ShaderProperty(_DefaultOutlineOpacity, MakeLabel(_DefaultOutlineOpacity));


        //--------------------
        if (TargetMode == false)
        {

            //LABELS
            GUILayout.Label("DISPLACEMENT", EditorStyles.helpBox);

            // get properties
            MaterialProperty _DisplacementMask = ShaderGUI.FindProperty("_DisplacementMask", properties);

            //Add to GUI
            editor.TexturePropertySingleLine(MakeLabel(_DisplacementMask, "Mask Map"), _DisplacementMask);
            //editor.TextureScaleOffsetProperty (_DisplacementMask);

            MaterialProperty _TileX = FindProperty("_TileX");
            editor.ShaderProperty(_TileX, MakeLabel(_TileX));

            MaterialProperty _TileY = FindProperty("_TileY");
            editor.ShaderProperty(_TileY, MakeLabel(_TileY));

            GUILayout.Label("---------------/ PANNER /---------------", EditorStyles.centeredGreyMiniLabel);

            MaterialProperty _PannerX = FindProperty("_PannerX");
            editor.ShaderProperty(_PannerX, MakeLabel(_PannerX));

            MaterialProperty _PannerY = FindProperty("_PannerY");
            editor.ShaderProperty(_PannerY, MakeLabel(_PannerY));

            GUILayout.Label("---------------/ DIRECTION /---------------", EditorStyles.centeredGreyMiniLabel);

            MaterialProperty _DirectionChange = FindProperty("_DirectionChange");
            editor.ShaderProperty(_DirectionChange, MakeLabel(_DirectionChange));

        }


        //--------------------

        //LABELS
        GUILayout.Label("SETTINGS", EditorStyles.helpBox);

        MaterialProperty _DefaultShrink = FindProperty("_DefaultShrink");
        editor.ShaderProperty(_DefaultShrink, MakeLabel(_DefaultShrink));

        MaterialProperty _NormalPush = FindProperty("_NormalPush");
		editor.ShaderProperty(_NormalPush, MakeLabel(_NormalPush));

		MaterialProperty _Shrink_Faces_Amplitude = FindProperty("_Shrink_Faces_Amplitude");
		editor.ShaderProperty(_Shrink_Faces_Amplitude, MakeLabel(_Shrink_Faces_Amplitude));

		MaterialProperty _Animation_speed = FindProperty("_Animation_speed");
		editor.ShaderProperty(_Animation_speed, MakeLabel(_Animation_speed));
        /*
		MaterialProperty _deformation_type_Factor = FindProperty("_deformation_type_Factor");
		editor.ShaderProperty(_deformation_type_Factor, MakeLabel(_deformation_type_Factor));
        */
        MaterialProperty _ExtrudeUpFaces = FindProperty("_ExtrudeUpFaces");
        editor.ShaderProperty(_ExtrudeUpFaces, MakeLabel(_ExtrudeUpFaces));

        MaterialProperty _Stretching = FindProperty("_Stretching");
		editor.ShaderProperty(_Stretching, MakeLabel(_Stretching));

        // Target Mode
        GUILayout.Label("TARGET MODE", EditorStyles.helpBox);
        MaterialProperty _TargetMode = FindProperty("_TargetMode");
        editor.ShaderProperty(_TargetMode, MakeLabel(_TargetMode));

        //TargetMode = EditorGUILayout.Toggle("Target Mode", TargetMode);
        float lol = _TargetMode.floatValue;
        if (lol == 1)
        {
            GUILayout.Label(" --- Select The Target GameObject in the Script Component (SC_Effect Control) --- ", EditorStyles.helpBox);
            MaterialProperty _InfluenceRadius = FindProperty("_InfluenceRadius");
            editor.ShaderProperty(_InfluenceRadius, MakeLabel(_InfluenceRadius));
        }


        //LABELS
        GUILayout.Label("DEBUG", EditorStyles.helpBox);
        MaterialProperty _Debug_Mask = FindProperty("_Debug_Mask");
        editor.ShaderProperty(_Debug_Mask, MakeLabel(_Debug_Mask));
        GUILayout.Space(30);
        GUILayout.Label(" SHAPES FX PACK ", EditorStyles.centeredGreyMiniLabel);
        GUILayout.Label(" 2024 ", EditorStyles.centeredGreyMiniLabel);


    }
}