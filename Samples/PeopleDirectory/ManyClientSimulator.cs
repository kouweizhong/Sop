﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Sop.Linq;

namespace Sop.Samples
{
    public class ManyClientSimulator : Sample
    {
        public class Person
        {
            public long PersonId;
            public string FirstName;
            public string LastName;
            public string PhoneNumber;
        }

        public void Run()
        {
            const int ThreadCount = 20;
            Console.WriteLine("Start of Many Client Simulator demo.");
            using (var Server = new ObjectServer(ServerFilename, true))
            {
                // Pre-populate store to simulate production store with existing items.
                AddItems(Server);

                List<Action> actions = new List<Action>();
                // create threads that will populate Virtual Cache and retrieve the items.
                for (int i = 0; i < ThreadCount; i++)
                {
                    // function to execute by the thread.
                    if (i < 4)
                    {
                        actions.Add(() =>
                        {
                            AddItems(Server);
                        });
                        continue;
                    }
                    actions.Add(() =>
                    {
                        ReadItems(Server);
                    });
                }
                for (int i = 0; i < ThreadCount; i++)
                {
                    // function to execute by the thread.
                    actions.Add(() =>
                    {
                        ReadItems(Server);
                    });
                }

                List<Task> tasks = new List<Task>();
                // launch or start the threads all at once.
                foreach (var a in actions)
                {
                    var t = TaskRun(a);
                    if (t == null)
                        continue;
                    tasks.Add(t);
                }
                // wait until all threads are finished.
                if (Threaded)
                    Task.WaitAll(tasks.ToArray());
                IStoreFactory sf = new StoreFactory();
                var PeopleStore = sf.Get<long, Person>(Server.SystemFile.Store, "People");
                Console.WriteLine("Processed, inserted & queried/enumerated multiple times, a total of {0} records.", PeopleStore.Count);
                Console.WriteLine("End of Many Client Simulator demo.");
            }
        }

        public bool Threaded = true;
        private Task TaskRun(Action action)
        {
            if (!Threaded)
            {
                action();
                return null;
            }
            return Task.Run(action);
        }

        const int ItemCount = 10000;
        private void AddItems(IObjectServer server)
        {
            IStoreFactory sf = new StoreFactory();
            var PeopleStore = sf.Get<long, Person>(server.SystemFile.Store, "People");
            for (int i = 0; i < ItemCount; i++)
            {
                var id = PeopleStore.GetNextSequence();
                PeopleStore.Add(new KeyValuePair<long, Person>(id, 
                    new Person {
                        PersonId = id,
                        FirstName = string.Format("Joe{0}", id),
                        LastName = string.Format("Petit{0}", id),
                        PhoneNumber = "555-999-4444"
                    }));
            }
        }
        private void ReadItems(IObjectServer server)
        {
            IStoreFactory sf = new StoreFactory();
            var PeopleStore = sf.Get<long, Person>(server.SystemFile.Store, "People");
            var r = new Random();
            var maxValue = (int)(PeopleStore.CurrentSequence / ItemCount);
            if (maxValue <= 0)
                maxValue = 1;
            maxValue *= 10;
            var i = r.Next(maxValue) * 1000;
            var keys = new long[1000];
            int c;
            for (c = 0; c < keys.Length; c++)
            {
                keys[c] = i + c + 1;
            }
            // just use Store and do Linq to Objects. Store & enumerators are thread safe.
            var qry = from a in PeopleStore.Query<long, Person>(keys)
                      select a;
            c = 0;
            foreach (var p in qry)
            {
                var personName = string.Format("{0} {1}", p.Value.FirstName, p.Value.LastName);
                if (p.Key % 100 == 0)
                    Console.WriteLine("Person found {0} from DB.", personName);
                if (keys[c] != p.Key)
                    throw new Exception(string.Format("Failed, didn't find person with key {0}, found {1} instead.",
                        keys[c], p.Key));
                c++;
            }
        }

        public const string ServerFilename = "SopBin\\OServer.dta";
    }
}
