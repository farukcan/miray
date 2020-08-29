﻿using RiveScript.Log;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;


namespace RiveScript.AST
{
    /// <summary>
    /// Tipic class for RiveScript
    /// </summary>
    public class Topic
    {
        static ILogger logger;

        Dictionary<string, Trigger> triggers = new Dictionary<string, Trigger>(); // Topics contain triggers
        bool _hasPrevious = false; // Has at least one %Previous
        Dictionary<string, ICollection<string>> previous = new Dictionary<string, ICollection<string>>();// Mapping of %Previous's to their triggers
        ICollection<string> _includes = new List<string>();// Included topics
        ICollection<string> _inherits = new List<string>();// Inherited topics
        string[] sorted = null; // Sorted trigger list

        //Currently selected topic
        public string name { get; private set; }

        /// <summary>
        /// Create a topic.
        /// </summary>
        /// <param name="name"></param>
        public Topic(string name)
        {
            this.name = name;
        }

        /// <summary>
        /// Set static logger object
	    /// be shared among all RiveScript instances and all Topics.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static void setLogger(ILogger logger)
        {
            Topic.logger = logger;
        }

        /// <summary>
        /// Fetch a Trigger object from the topic. If the trigger doesn't exist, it
	    /// is created on the fly.
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
        public Trigger trigger(string pattern)
        {
            if (!triggers.ContainsKey(pattern))
            {
                var trigger = new Trigger(pattern);
                trigger.setTopic(name);

                triggers.Add(pattern, trigger);
            }

            return triggers[pattern];
        }


        public void addTrigger(Trigger trigger)
        {
            var pattern = trigger.getPattern();
            trigger.setTopic(name);

            triggers.AddOrUpdate(pattern, trigger);
        }

        /// <summary>
        /// Test if a trigger exists by the pattern
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
        public bool triggerExists(string pattern)
        {
            return triggers.ContainsKey(pattern);
        }

        /// <summary>
        /// Fetch a sorted list of all triggers. Note that the results are only accurate if
        ///	you called sortTriggers() for this topic after loading new replies into it(the
        /// sortReplies() in RiveScript automagically calls sortTriggers() for all topics,
        /// so just make sure you call sortReplies() after loading new replies).
        /// </summary>
        /// <returns></returns>
        public string[] listTriggers()
        {
            return listTriggers(false);
        }

        /// <summary>
        /// Fetch a list of all triggers. If you provide a true value to this method, it will
        /// return the UNSORTED list(getting the keys of the trigger hash directly). If you
        /// want a SORTED list(which you probably do), use listTriggers() instead, or explicitly
        /// provide a false value to this method.
        /// </summary>
        /// <param name="raw"> Get a raw unsorted list instead of a sorted one.</param>
        /// <returns></returns>
        public string[] listTriggers(bool raw)
        {
            // If raw, get the unsorted triggers directly from the hash.
            if (raw)
            {
                var trigs = triggers.Keys;

                if (logger.IsDebugEnable)
                {
                    foreach (var t in trigs)
                    {
                        logger.Debug("RAW TRIGGER: " + t);
                    }
                }
                return trigs.ToArray();
            }

            // Do we have a sort buffer?
            if (sorted == null)
            {
                // Um no, that's bad.
                logger.Debug("[ERROR] You called listTriggers() for topic " + name + " before its replies have been sorted!");
                return new string[0];
            }

            return sorted;
        }

        /// <summary>
        /// (Re)create the internal sort cache for this topic's triggers.
        /// </summary>
        /// <param name="alltrigs"></param>
        public void sortTriggers(string[] alltrigs)
        {
            // Get our list of triggers.
            var sorted = new List<string>();

            // Do multiple sorts, one for each inheritence level.
            var heritage = new Dictionary<int, ICollection<string>>();
            heritage.Add(-1, new List<string>());
            var highest = -1;


            var reInherit = new Regex("\\{inherits=(\\d+)\\}");
            for (int i = 0; i < alltrigs.Length; i++)
            {
                int inherits = -1; // Default, when no {inherits} tag.

                // Does it have an inherit level?
                if (alltrigs[i].IndexOf("{inherits=") > -1)
                {
                    var mc = reInherit.Matches(alltrigs[i]);
                    foreach (Match m in mc)
                    {
                        inherits = int.Parse(m.Groups[1].Value);
                        if (inherits > highest)
                        {
                            highest = inherits;
                        }
                        break;
                    }
                }

                alltrigs[i] = Regex.Replace(alltrigs[i], "\\{inherits=\\d+\\}", "");

                // Initialize this inherit group?
                if (false == heritage.ContainsKey(inherits))
                {
                    heritage.Add(inherits, new List<string>());
                }

                // Add it.
                heritage[inherits].Add(alltrigs[i]);
            }

            // Move the no-{inherits} triggers to the bottom of the stack.
            heritage.Add(highest + 1, heritage[-1]);
            heritage.Remove(-1);


            // Go on and sort each heritage level. We want to loop from level 0 up,
            for (int h = 0; h <= highest + 1; h++)
            {
                if (false == heritage.ContainsKey(h))
                {
                    continue;
                }

                int inherits = h;
                logger.Debug("Sorting triggers by heritage level " + inherits);
                var triggers = heritage[inherits].ToArray();

                // Sort-priority maps.
                var prior = new Dictionary<int, ICollection<string>>();

                // Assign each trigger to its priority level.
                logger.Debug("BEGIN sortTriggers in topic " + name);

                var rePrior = new Regex("\\{weight=(\\d+?)\\}");
                for (int i = 0; i < triggers.Length; i++)
                {
                    int priority = 0;

                    // See if this trigger has a {weight}.
                    if (triggers[i].IndexOf("{weight") > -1)
                    {
                        //NOTE: I thnk this can do user only Match
                        var mc = rePrior.Matches(triggers[i]);
                        foreach (Match m in mc)
                        {
                            priority = int.Parse(m.Groups[1].Value);
                        }
                    }

                    // Initialize its priority group?
                    if (false == prior.ContainsKey(priority))
                    {
                        // Create it.
                        prior.Add(priority, new List<string>());
                    }

                    // Add it.
                    prior[priority].Add(triggers[i]);
                }

                /*
                    Keep in mind here that there is a difference between includes and
                    inherits -- topics that inherit other topics are able to OVERRIDE
                    triggers that appear in the inherited topic. This means that if the
                    top topic has a trigger of simply *, then NO triggers are capable of
                    matching in ANY inherited topic, because even though * has the lowest
                    sorting priority, it has an automatic priority over all inherited
                    topics.

                    The topicTriggers in TopicManager takes this into account. All topics
                    that inherit other topics will have their local triggers prefixed
                    with a fictional {inherits} tag, which will start at {inherits=0}
                    and increment if the topic tree has other inheriting topics. So
                    we can use this tag to make sure topics that inherit things will
                    have their triggers always be on the top of the stack, from
                    inherits=0 to inherits=n.
                */

                // Sort the priority lists numerically from highest to lowest.
                var prior_sorted = Util.SortKeysDesc(prior);
                for (int p = 0; p < prior_sorted.Length; p++)
                {
                    logger.Debug("Sorting triggers w/ priority " + prior_sorted[p]);
                    var p_list = prior[prior_sorted[p]];

                    /*
                        So, some of these triggers may include {inherits} tags, if
                        they came from a topic which inherits another topic. Lower
                        inherits values mean higher priority on the stack. Keep this
                        in mind when keeping track of how to sort these things.
                    */

                    // Initialize a sort bucket that will keep inheritance levels'
                    // triggers in separate places.
                    //com.rivescript.InheritanceManager bucket = new com.rivescript.InheritanceManager();
                    var bucket = new Inheritance();

                    // Loop through the triggers and sort them into their buckets.
                    bucket.Fill(p_list);

                    // Sort each inheritence level individually.
                    logger.Debug("Dumping sort bucket !");
                    var subsort = bucket.Dump();
                    foreach (var item in subsort)
                    {
                        logger.Debug("ADD TO SORT: " + item);
                        sorted.Add(item);
                    }
                }
            }

            // Turn the running sort buffer into a string array and store it.
            this.sorted = sorted.ToArray();
        }

        /// <summary>
        /// Add a mapping between a trigger and a %Previous that follows it.
        /// </summary>
        /// <param name="pattern"></param>
        /// <param name="previous"></param>
        public void addPrevious(string pattern, string previous)
        {
            if (!this.previous.ContainsKey(previous))
            {
                this.previous.Add(previous, new List<string>());
            }

            this.previous[previous].Add(pattern);
        }

        /// <summary>
        /// Check if any trigger in the topic has a %Previous (only good after
	    /// sortPrevious, from RiveScript.sortReplies is called).
        /// </summary>
        public bool hasPrevious()
        {
            return _hasPrevious;
        }

        /// <summary>
        /// Get a list of all the %Previous keys.
        /// </summary>
        public string[] listPrevious()
        {
            var list = new List<string>();
            foreach (string key in previous.Keys)
            {
                list.Add(key);
            }
            return list.ToArray();
        }

        /// <summary>
        ///  List the triggers associated with a %Previous.
        /// </summary>
        /// <param name="previous">The %Previous pattern.</param>
        /// <returns></returns>
        public string[] listPreviousTriggers(string previous)
        {
            // TODO return sorted list
            if (this.previous.ContainsKey(previous))
            {
                return this.previous[previous].ToArray();
            }

            return new string[0];
        }

        /// <summary>
        /// Sort the %Previous buffer.
        /// </summary>
        public void sortPrevious()
        {
            // Keep track if ANYTHING has a %Previous.
            _hasPrevious = false;

            // Find all the triggers that have a %Previous. This hash maps a %Previous
            // label to the list of triggers that are associated with it.
            var prev2trig = new Dictionary<string, ICollection<string>>();

            // Loop through the triggers to find those with a %Previous.
            var triggers = listTriggers(true);

            for (int i = 0; i < triggers.Length; i++)
            {
                var pattern = triggers[i];
                if (pattern.IndexOf("{previous}") > -1)
                {
                    // This one has it.
                    _hasPrevious = true;

                    var parts = pattern.SplitRegex("\\{previous\\}", 2);
                    var previous = parts[1];

                    // Keep it under the %Previous.
                    if (false == prev2trig.ContainsKey(previous))
                    {
                        prev2trig.Add(previous, new List<String>());
                    }
                    prev2trig[previous].Add(parts[0]);
                }
            }

            // Loop over keys
            // TODO: Can do better to solve this, its is not best performance options
            // NOTE: ToArray is necessary to clone key list and avoid error on interation
            foreach (var key in prev2trig.Keys.ToArray())
            {
                prev2trig[key] = Inheritance.SortAtOnce(prev2trig[key]);
            }


            this.previous = prev2trig;
        }

        /// <summary>
        /// Query whether a %Previous is registered with this topic.
        /// </summary>
        /// <param name="previous">The pattern in the %Previous.</param>
        /// <returns></returns>
        public bool previousExists(string previous)
        {
            return this.previous.ContainsKey(previous);
        }

        /// <summary>
        ///  Retrieve a string array of the +Triggers that are associated with a %Previous.
        /// </summary>
        /// <param name="previous">The pattern in the %Previous.</param>
        /// <returns></returns>
        public string[] listPrevious(string previous)
        {
            if (this.previous.ContainsKey(previous))
            {
                return this.previous[previous].ToArray();
            }

            return new string[0];
        }

        /// <summary>
        /// Add a topic that this one includes.
        /// </summary>
        /// <param name="topic"> The included topic's name.</param>
        public void includes(string topic)
        {
            _includes.Add(topic);
        }

        /// <summary>
        ///  Add a topic that this one inherits.
        /// </summary>
        /// <param name="topic">The inherited topic's name.</param>
        public void inherits(string topic)
        {
            _inherits.Add(topic);
        }

        /// <summary>
        /// Retrieve a list of includes topics.
        /// </summary>
        /// <returns></returns>
        public string[] includes()
        {
            return _includes.ToArray();
        }

        /// <summary>
        /// Retrieve a list of inherited topics.
        /// </summary>
        /// <returns></returns>
        public string[] inherits()
        {
            return _inherits.ToArray();
        }
    }
}
