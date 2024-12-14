using UnityEngine;

namespace CosmicShore
{
    public class SavePng : MonoBehaviour
    {
        public bool DisableAnimations = true;
        public GameObject Container;
        public RenderTexture renderTextureSquare;
        public RenderTexture renderTextureSquareLarge;
        public RenderTexture renderTextureWide;
        public RenderTexture renderTextureWideLarge;
        public Camera _camera;
        public float PressedScaleFactor = 1.25f;
        public Texture2D SquareAspectBorderTexture;
        public Texture2D WideAspectBorderTexture;
        public Color SilhouetteColor = new Color(.95f, .95f, .95f);

        public enum Aspect
        {
            Square,
            Wide
        }

        private void Start()
        {
            _camera.backgroundColor = Color.clear;
        }

        private void Update()
        {
            var animators = Container.GetComponentsInChildren<Animator>();
            foreach (var animator in animators)
            {
                animator.enabled = false;
            }
        }

        public void SaveSquareTexture()
        {
            SaveTexture(Aspect.Square);
        }

        public void SaveWideTexture()
        {
            SaveTexture(Aspect.Wide);
        }

        public void SaveTexture(Aspect aspect)
        {
            var fileName = Container.transform.GetChild(0).name.Replace("(Clone)", "");
            RenderTexture renderTexture;
            RenderTexture renderTextureLarge;

            if (aspect == Aspect.Square)
            {
                renderTexture = renderTextureSquare;
                renderTextureLarge = renderTextureSquareLarge;
            }
            else
            {
                renderTexture = renderTextureWide;
                renderTextureLarge = renderTextureWideLarge;
            }

            byte[] bytes = ToTexture2D(renderTexture, renderTexture.width, renderTexture.height).EncodeToPNG();
            System.IO.File.WriteAllBytes(Application.persistentDataPath + "/" + fileName + ".png", bytes);

            bytes = ToTexture2D(renderTextureLarge, renderTextureLarge.width, renderTextureLarge.height).EncodeToPNG();
            System.IO.File.WriteAllBytes(Application.persistentDataPath + "/" + fileName + "_large.png", bytes);

            bytes = ToTexture2DSilhouette(renderTexture, renderTexture.width, renderTexture.height).EncodeToPNG();
            System.IO.File.WriteAllBytes(Application.persistentDataPath + "/" + fileName + "_silhouette.png", bytes);

            var pressedTexture = ToTexture2D(renderTexture, renderTexture.width, renderTexture.height);
            TextureScale.Bilinear(pressedTexture, (int)(renderTexture.width * PressedScaleFactor), (int)(renderTexture.height * PressedScaleFactor));
            bytes = pressedTexture.EncodeToPNG();
            System.IO.File.WriteAllBytes(Application.persistentDataPath + "/" + fileName + "_pressed.png", bytes);

            if (aspect == Aspect.Square)
            { 
                bytes = ToTexture2DBordered(renderTexture, SquareAspectBorderTexture, renderTexture.width, renderTexture.height).EncodeToPNG();
                System.IO.File.WriteAllBytes(Application.persistentDataPath + "/" + fileName + "_selected.png", bytes);
            }
            else
            {
                bytes = ToTexture2DBordered(renderTexture, WideAspectBorderTexture, renderTexture.width, renderTexture.height).EncodeToPNG();
                System.IO.File.WriteAllBytes(Application.persistentDataPath + "/" + fileName + "_selected.png", bytes);
            }
        }

        Texture2D ToTexture2D(RenderTexture rTex, int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            RenderTexture.active = rTex;
            tex.ReadPixels(new Rect(0, 0, rTex.width, rTex.height), 0, 0);
            tex.Apply();
            Destroy(tex);
            return tex;
        }

        Texture2D ToTexture2DSilhouette(RenderTexture rTex, int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            RenderTexture.active = rTex;
            tex.ReadPixels(new Rect(0, 0, rTex.width, rTex.height), 0, 0);
            Color32[] texColors = tex.GetPixels32();

            for (int i = 0; i < texColors.Length; i++)
            {
                if (texColors[i].a > .1)
                {
                    var alpha = texColors[i].a;
                    texColors[i] = new Color(SilhouetteColor.r, SilhouetteColor.g, SilhouetteColor.b, alpha);
                }
            }

            tex.SetPixels32(texColors);

            tex.Apply();
            Destroy(tex);
            return tex;
        }

        Texture2D ToTexture2DBordered(RenderTexture rTex, Texture2D borderTex, int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            RenderTexture.active = rTex;
            tex.ReadPixels(new Rect(0, 0, rTex.width, rTex.height), 0, 0);
            tex.Apply();

            Color32[] borderColors = borderTex.GetPixels32();
            Color32[] texColors = tex.GetPixels32();
            Color32[] newColors = new Color32[width * height]; ;

            for (int i = 0; i < borderColors.Length; i++)
            {
                if (borderColors[i].a > .1)
                {
                    newColors[i] = borderColors[i];
                }
                else
                {
                    newColors[i] = texColors[i];
                }
            }

            tex.SetPixels32(newColors);
            tex.Apply();

            Destroy(tex);
            return tex;
        }
    }
}