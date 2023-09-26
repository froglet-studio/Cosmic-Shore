using System.Linq;
using UnityEngine;

public class TextObjectLoader : MonoBehaviour
{
    [SerializeField] TextAsset objectData;
    [SerializeField] GameObject WallPrefab;
    [SerializeField] GameObject PelletPrefab;
    [SerializeField] GameObject PowerPelletPrefab;
    [SerializeField] Vector3 PelletScale;
    [SerializeField] Vector3 WallScale;
    [SerializeField] Vector3 PowerPelletScale;

    // Start is called before the first frame update
    void Start()
    {
        string[] lines = objectData.text.Split("\n");
        Debug.Log(lines.Length);

        int x = 0;
        int z = 0;

        foreach (var line in lines)
        {
            foreach (char c in line.Reverse())
            {
                GameObject spawnable = null;
                Vector3 scale = Vector3.one;
                switch (c)
                {
                    case '\n':
                    case '\r':
                    case ' ':
                        break;
                    /*case '.':
                        spawnable = PelletPrefab;
                        scale = PelletScale;
                        break;
                    case '*':
                        spawnable = PowerPelletPrefab;
                        scale = PowerPelletScale;
                        break;
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        spawnable = WallPrefab;
                        scale = WallScale;
                        scale = new Vector3(WallScale.x, c-'0', WallScale.z);
                        break;*/
                    default:
                        spawnable = PowerPelletPrefab;
                        scale = PowerPelletScale;
                        break;
                }

                if (spawnable != null)
                {
                    var spawned = Instantiate(spawnable);
                    spawned.transform.localScale = scale;
                    spawned.transform.localPosition = new Vector3(x, scale.y/2, z);
                }

                x += 2;
            }

            x = 0;
            z += 4;
        }
        x = 0;
        z = 2;
        foreach (var line in lines)
        {
            foreach (char c in line.Reverse())
            {
                GameObject spawnable = null;
                Vector3 scale = Vector3.one;
                switch (c)
                {
                    case '\n':
                    case '\r':
                    case ' ':
                        break;
                    /*case '.':
                        spawnable = PelletPrefab;
                        scale = PelletScale;
                        break;
                    case '*':
                        spawnable = PowerPelletPrefab;
                        scale = PowerPelletScale;
                        break;
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        spawnable = WallPrefab;
                        scale = WallScale;
                        scale = new Vector3(WallScale.x, c-'0', WallScale.z);
                        break;*/
                    default:
                        spawnable = PowerPelletPrefab;
                        scale = PowerPelletScale;
                        break;
                }

                if (spawnable != null)
                {
                    var spawned = Instantiate(spawnable);
                    spawned.transform.localScale = scale;
                    spawned.transform.localPosition = new Vector3(x, scale.y / 2, z);
                }

                x += 2;
            }

            x = 0;
            z += 4;
        }
    }
}