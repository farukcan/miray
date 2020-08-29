using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RiveScript;
using RiveScript.Log;
using UnityEngine.UI;

public class RiveMonoBehaviour : MonoBehaviour {

    public static RiveMonoBehaviour instance;
    [HideInInspector]
    public string fnc_return = "";

    public Text reply;
    public Text konsol;
    public InputField question;

    public TextAsset riveData;

    public string userName = "localUser";

    [HideInInspector]
    public RiveScriptEngine engine;

    void Start()
    {

        instance = this;
        Config config = new Config();
        config.utf8 = true;
        config.logger = new ConsoleLogger();
        config.forceCase = true;
        config.debug = true;
        engine = new RiveScriptEngine(config);


        engine.stream(riveData.text);

        engine.sortReplies();


        
        Debug.Log(riveData.text);
    }

    // Update is called once per frame
    public void GetReply()
    {
        GetReply(question.text);
    }

    public void GetReply(string ask)
    {
        konsol.text = "+ " + ask + '\n' + konsol.text;
        string cmdReply = engine.reply(userName, ask);
        reply.text = command(cmdReply);
        konsol.text = "- " + reply.text + '\n' + konsol.text;
        question.text = "";
    }


    public string command(string cmdReply)
    {

        string reply="";


        string[] splitted = cmdReply.SplitRegex(@"\@(.+?)\@");

        for(int i=0; i < splitted.Length; i++)
        {
            if (i % 2 == 0)
            {
                reply += splitted[i];
            }
            else
            {
                string s = splitted[i];

                string[] ses = s.Split(':');

                fnc_return = "";

                if (ses.Length >= 2)
                {
                    BroadcastMessage(ses[0], ses[1]);
                    Debug.Log("Running CMD : " + ses[0] + " with ; " + ses[1]);
                }
                else
                {
                    BroadcastMessage(ses[0]);
                    Debug.Log("Running CMD : " + ses[0]);
                }

                reply += fnc_return;

            }
        }


        return reply;
    }
}
