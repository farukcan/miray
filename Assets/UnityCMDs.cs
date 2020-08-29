using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnityCMDs : MonoBehaviour
{
    public void close_app()
    {
        Application.Quit();
    }

    public void log(string log)
    {
        RiveMonoBehaviour.instance.fnc_return = log;
        Debug.Log(log);
    }
}
