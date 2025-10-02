using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class HideMouse : MonoBehaviour
{
    private void Start()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }
}
