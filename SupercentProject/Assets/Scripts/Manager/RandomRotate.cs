using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RandomRotate : MonoBehaviour
{
    public void Rotate()
    {
        for (int i = 0; i < transform.childCount - 1; ++i)
        {
            transform.GetChild(i).eulerAngles = new Vector3(0f, UnityEngine.Random.Range(0, 360), 0f);
        }
    }
}
