using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Impact : MonoBehaviour
{

    public IEnumerator ImpactCoroutine(Vector3 velocity, Material material,string ID)
    {
        var velocityScale = 5f;
        float timeStamp = 0;

        if (ID == "Player") { material.SetFloat("_player", 1); }
        else
        {
            if (ID == "red") { material.SetFloat("_player", 0); material.SetFloat("_red", 1); }
            else { material.SetFloat("_player", 0); material.SetFloat("_red", 0); }
        }
        

        while (timeStamp <= 1)

        {
            yield return new WaitForSeconds(.001f);
            timeStamp += .001f;
            material.SetVector("_velocity", velocityScale*timeStamp*velocity);
            material.SetFloat("_opacity", (1-timeStamp));
            transform.position += (velocityScale * timeStamp * velocity);


        }
        Destroy(material);
        Destroy(transform.gameObject);

    }
}