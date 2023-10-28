using UnityEngine;

namespace CosmicShore
{
    public class SavePng : MonoBehaviour
    {
        public bool DisableAnimations = true;
        public GameObject Container;
        public RenderTexture renderTextureSquare;
        public RenderTexture renderTextureWide;
        public Camera _camera;
        public float PressedScaleFactor = 1.25f;
        public Texture2D SquareAspectBorderTexture;
        public Texture2D SquareAspectQuestionMarkTexture;
        public Texture2D WideAspectBorderTexture;
        public Texture2D WideAspectQuestionMarkTexture;
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

            int width, height;
            if (aspect == Aspect.Square)
            {
                width = 256;
                height = 256;
                renderTexture = renderTextureSquare;
            }
            else
            {
                width = 420;
                height = 256;
                renderTexture = renderTextureWide;
            }

            byte[] bytes = toTexture2D(renderTexture, width, height).EncodeToPNG();
            System.IO.File.WriteAllBytes(Application.persistentDataPath + "/" + fileName + ".png", bytes);

            bytes = toTexture2DSilhouette(renderTexture, width, height).EncodeToPNG();
            System.IO.File.WriteAllBytes(Application.persistentDataPath + "/" + fileName + "_silhouette.png", bytes);

            var pressedTexture = toTexture2D(renderTexture, width, height);
            TextureScale.Bilinear(pressedTexture, (int)(width * PressedScaleFactor), (int)(height * PressedScaleFactor));
            bytes = pressedTexture.EncodeToPNG();
            System.IO.File.WriteAllBytes(Application.persistentDataPath + "/" + fileName + "_pressed.png", bytes);

            if (aspect == Aspect.Square)
            { 
                bytes = toTexture2DBordered(renderTexture, SquareAspectBorderTexture, width, height).EncodeToPNG();
                System.IO.File.WriteAllBytes(Application.persistentDataPath + "/" + fileName + "_selected.png", bytes);
            }
            else
            {
                bytes = toTexture2DBordered(renderTexture, WideAspectBorderTexture, width, height).EncodeToPNG();
                System.IO.File.WriteAllBytes(Application.persistentDataPath + "/" + fileName + "_selected.png", bytes);
            }
        }

        Texture2D toTexture2D(RenderTexture rTex, int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            RenderTexture.active = rTex;
            tex.ReadPixels(new Rect(0, 0, rTex.width, rTex.height), 0, 0);
            tex.Apply();
            Destroy(tex);
            return tex;
        }

        Texture2D toTexture2DSilhouette(RenderTexture rTex, int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            RenderTexture.active = rTex;
            tex.ReadPixels(new Rect(0, 0, rTex.width, rTex.height), 0, 0);
            Color[] texColors = tex.GetPixels();

            for (int i = 0; i < texColors.Length; i++)
            {
                if (texColors[i].a > .1)
                {
                    var alpha = texColors[i].a;
                    texColors[i] = new Color(SilhouetteColor.r, SilhouetteColor.g, SilhouetteColor.b, alpha);
                }
            }

            /*
            Color[] qMarkColors = SquareAspectQuestionMarkTexture.GetPixels();
            for (int i = 0; i < qMarkColors.Length; i++)
            {
                if (qMarkColors[i].a > .1)
                {
                    var alpha = texColors[i].a;
                    texColors[i] = qMarkColors[i];//Color.black;
                    //texColors[i].a = alpha;
                }
            }
            */

            tex.SetPixels(texColors);

            tex.Apply();
            Destroy(tex);
            return tex;
        }

        Texture2D toTexture2DBordered(RenderTexture rTex, Texture2D borderTex, int width, int height)
        {
            //Graphics.Blit(borderTex, rTex);
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            RenderTexture.active = rTex;
            tex.ReadPixels(new Rect(0, 0, rTex.width, rTex.height), 0, 0);
            tex.Apply();

            Color[] borderColors = borderTex.GetPixels();
            Color[] texColors = tex.GetPixels();
            Color[] newColors = new Color[width * height]; ;

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

            tex.SetPixels(newColors);
            tex.Apply();

            Destroy(tex);
            return tex;
        }
    }
}