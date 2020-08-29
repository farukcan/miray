using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RethinkDb.Driver.Ast;
using RethinkDb.Driver.Model;
using RethinkDb.Driver.Net;
using RethinkDb.Driver.Utils;
using RethinkDb.Driver;
using Newtonsoft.Json.Linq;

public class RThinkTest : MonoBehaviour
{
    public static RethinkDB R = RethinkDB.R;

    Connection c;

    void Start()
    {
        c = R.Connection()
         .Hostname("127.0.0.1")
         .Port(RethinkDBConstants.DefaultPort)
         .Timeout(60)
         .Connect();

        Debug.Log("Bağlantı sağlandı");

        StartCoroutine(getData());

        Debug.Log("Az önce trans başladı");

    }


    IEnumerator getData()
    {
        var transaction = R.Db("miray").Table("main").RunResultAsync<JArray>(c);

       yield return new WaitUntil( ()=> transaction.IsCompleted);

        Debug.Log(transaction.Result);

    }

}
