using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Neutrino.Seyren;
using Neutrino.Seyren.Domain;
using VersionDb.Client;

namespace Neutrino.AlertController
{
    public static class AlertVersions
    {
        public static string TypeName => "alert";
        public static string V1 => "v1";
    }

    public static class SubscriptionVersions
    {
        public static string TypeName => "subscription";
        public static string V1 => "v1";
    }

    class Program
    {
        static IVersionDbClient<Alert> alertDataStore = new SimpleVersionDbClient<Alert>(new HttpClient {
            BaseAddress = new Uri("http://localhost:5000")
        }, AlertVersions.TypeName, AlertVersions.V1);

        static IVersionDbClient<Subscription> subscriptionDataStore = new SimpleVersionDbClient<Subscription>(new HttpClient {
            BaseAddress = new Uri("http://localhost:5000")
        }, SubscriptionVersions.TypeName, SubscriptionVersions.V1);

        static SeyrenClient seyrenClient = new SeyrenClient(new HttpClient {
            BaseAddress = new Uri("http://localhost:8080")
        });

        static void Main(string[] args)
        {

            foreach ( Change<Alert> change in alertDataStore.Watch("*") )
            {
                Synchronise();
            }
            
            foreach ( Change<Subscription> change in subscriptionDataStore.Watch("*") )
            {
                Synchronise();
            }

            // Timer
        }

        private static void Synchronise()
        {
            var currentChecks = seyrenClient.Checks.GetAllAsync().Result.Values;

            IEnumerable<Alert> alerts = alertDataStore.GetAll();
            IEnumerable<Subscription> subscriptions = subscriptionDataStore.GetAll();

            var targetChecks = alerts
                .Select(a => new Neutrino.Seyren.Domain.Check
                {
                    Name = $"{a.Name}", // TODO change scorecard to look at tags in description so we can tidy this up
                                                 // TODO: Append launchtower links?
                    // Description = a.description + $"\r\n\r\n#seyrendriver #{featureName} #{teamName}",
                    Target = a.Target,
                    Error = a.Error,
                    Warn = a.Warn,
                    Subscriptions = subscriptions
                    .Select(s => new Neutrino.Seyren.Domain.Subscription
                    {
                        Type = s.Type,
                        Target = s.Target,
                        EnabledOnMonday = true,
                        EnabledOnTuesday = true,
                        EnabledOnWednesday = true,
                        EnabledOnThursday = true,
                        EnabledOnFriday = true,
                        EnabledOnSaturday = true,
                        EnabledOnSunday = true,
                        Enabled = true,
                        FromTime = "0000",
                        ToTime = "2359"
                    }).ToList()
                });


            IEnumerable<(Check, Check)> joinStuff = targetChecks.FullJoinDistinct(
                currentChecks,
                c => c.Name.ToLowerInvariant(),
                c => c.Name.ToLowerInvariant(),
                (a, b) => (a, b)
            ).Distinct(new LambdaComparer<(Check, Check)>(
                (x, y) => $"{x.Item1?.Name}:{x.Item2?.Name}" == $"{y.Item1?.Name}:{y.Item2?.Name}",
                obj => $"{obj.Item1?.Name}:{obj.Item2?.Name}".GetHashCode()
            ));

            SynchroniseAlerts(joinStuff, seyrenClient);
        }

        private static void SynchroniseAlerts(
            IEnumerable<(Check, Check)> joinStuff,
            SeyrenClient seyrenApiClient
            )
        {
            joinStuff
                .Where(p => p.Item1 == null)
                .Select(p => p.Item2)
                .ForEach(c =>
                {
                    Console.WriteLine("Delete: " + c.Name);
                    seyrenApiClient.Checks.Delete(c.Id);
                });

            joinStuff
                .Where(p => p.Item2 == null)
                .Select(p => p.Item1)
                .ForEach(c =>
                {
                    Console.WriteLine("Create: " + c.Name);
                    string checkId = seyrenApiClient.Checks.CreateAsync(c).Result.Id;
                });

            joinStuff
                .Where(p => p.Item1 != null && p.Item2 != null)
                .ForEach(p =>
                {
                    Console.WriteLine("Update: " + p.Item1.Name);
                    seyrenApiClient.Checks.UpdateAsync(p.Item2.Id, p.Item1).Wait();
                    // TODO: Update subscriptions

                    IEnumerable<(Neutrino.Seyren.Domain.Subscription, Neutrino.Seyren.Domain.Subscription)> joinedSubs = p.Item1.Subscriptions.FullJoinDistinct(
                        p.Item2.Subscriptions,
                        c => c.Target,
                        c => c.Target,
                        (a, b) => (a, b)
                    ).Distinct(new LambdaComparer<(Neutrino.Seyren.Domain.Subscription, Neutrino.Seyren.Domain.Subscription)>(
                        (x, y) => x.Item1.Target == y.Item1.Target && x.Item2.Target == y.Item2.Target,
                        obj => $"{obj.Item1.Target}:{obj.Item2.Target}".GetHashCode()
                    ));

                    SynchroniseSubscriptions(joinedSubs, p.Item2, seyrenApiClient);
                });
        }

        private static void SynchroniseSubscriptions(
            IEnumerable<(Neutrino.Seyren.Domain.Subscription, Neutrino.Seyren.Domain.Subscription)> joinedSubs, 
            Check check, 
            SeyrenClient seyrenApiClient)
        {
            joinedSubs
                .Where(s => s.Item1 == null)
                .Select(s => s.Item2)
                .ForEach(s =>
                {
                    Console.WriteLine($"Delete sub: {s.Type}: {s.Target}");
                    seyrenApiClient.Subscriptions.Delete(check.Id, s.Id).Wait();
                });

            joinedSubs
                .Where(s => s.Item2 == null)
                .Select(s => s.Item1)
                .ForEach(s =>
                {
                    Console.WriteLine($"Create sub: {s.Type}: {s.Target}");
                    seyrenApiClient.Subscriptions.Create(check.Id, s).Wait();
                });

            joinedSubs
                .Where(s => s.Item1 != null && s.Item2 != null)
                .ForEach(s =>
                {
                    Console.WriteLine($"Update Sub: {s.Item1.Type}: {s.Item1.Target}");
                    seyrenApiClient.Subscriptions.Update(check.Id, s.Item2.Id, s.Item1).Wait();
                });
        }
    }

    public class LambdaComparer<T> : IEqualityComparer<T>
    {
        private readonly Func<T, T, bool> equals;
        private readonly Func<T, int> getHashCode;

        public LambdaComparer(Func<T, T, bool> equals, Func<T, int> getHashCode)
        {
            this.equals = equals;
            this.getHashCode = getHashCode;
        }

        public bool Equals(T x, T y)
        {
            return equals(x, y);
        }

        public int GetHashCode(T obj)
        {
            return getHashCode(obj);
        }
    }


    public static class MyExtensions {

        public static IEnumerable<TResult> FullJoinDistinct<TLeft, TRight, TKey, TResult> (
            this IEnumerable<TLeft> leftItems, 
            IEnumerable<TRight> rightItems, 
            Func<TLeft, TKey> leftKeySelector, 
            Func<TRight, TKey> rightKeySelector,
            Func<TLeft, TRight, TResult> resultSelector
        ) {

            var leftJoin = 
                from left in leftItems
                join right in rightItems 
                on leftKeySelector(left) equals rightKeySelector(right) into temp
                from right in temp.DefaultIfEmpty()
                select resultSelector(left, right);

            var rightJoin = 
                from right in rightItems
                join left in leftItems 
                on rightKeySelector(right) equals leftKeySelector(left) into temp
                from left in temp.DefaultIfEmpty()
                select resultSelector(left, right);

            return leftJoin.Union(rightJoin);
        }

    }

    public static class EnumerableExtensions
    {
        public static void ForEach<T>(this IEnumerable<T> self, Action<T> action)
        {
            foreach ( T element in self )
            {
                action(element);
            }
        }
    }
}
