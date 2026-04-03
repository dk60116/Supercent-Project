using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OreArea : MonoBehaviour
{


    public void Rotate()
    {
        for (int i = 0; i < transform.childCount - 1; ++i)
        {
            transform.GetChild(i).eulerAngles = new Vector3(0f, UnityEngine.Random.Range(0, 360), 0f);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            GameManager.Instance.Player.Controller.EnterMine(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            GameManager.Instance.Player.Controller.EnterMine(false);
        }
    }
}
