using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;

using NodaTime;

using GGNet.NaturalEarth;

namespace GGNet.Site.Pages
{
    public partial class Covid19 : ComponentBase
    {
        private async Task<List<TS>> GetData()
        {
            var csv = await Http.GetStringAsync("https://raw.githubusercontent.com/owid/covid-19-data/master/input/ecdc/releases/latest.csv");

            var data = csv
                .Split('\n')
                .Skip(1)
                .Select(line =>
                {
                    var fields = line.Split(',');

                    var a2 = fields[7];

                    var year = int.Parse(fields[3]);
                    var month = int.Parse(fields[2]);
                    var day = int.Parse(fields[1]);

                    var date = new LocalDate(year, month, day);

                    var cases = double.Parse(fields[4]);
                    var deaths = double.Parse(fields[5]);

                    return (a2, date, cases, deaths);
                })
                .GroupBy(o => o.a2)
                .Select(g =>
                {
                    var points = g
                        .OrderBy(o => o.date)
                        .Select(o => new TS.Point
                        {
                            Date = o.date,
                            ConfirmedDelta = o.cases,
                            DeathsDelta = o.deaths
                        })
                        .ToArray();

                    var confirmedCumulative = 0.0;
                    var deathsCumulative = 0.0;

                    for (var i = 0; i < points.Length; i++)
                    {
                        var point = points[i];

                        confirmedCumulative += point.ConfirmedDelta;
                        deathsCumulative += point.DeathsDelta;

                        point.ConfirmedCumulative = confirmedCumulative;
                        point.DeathsCumulative = deathsCumulative;
                    }

                    var last = points[^1];

                    var ts = new TS
                    {
                        Country = Scale110.Countries.SingleOrDefault(o => o.A2 == g.Key),
                        Points = points,
                        ConfirmedDelta = last.ConfirmedDelta,
                        ConfirmedCumulative = last.ConfirmedCumulative,
                        DeathsDelta = last.DeathsDelta,
                        DeathsCumulative = last.DeathsCumulative
                    };

                    return ts;
                })
                .Where(o => o.Country != null && o.Country.Capital != null)
                .ToList();

            return data;
        }

        private List<StatPoint> GetStatPoints(TS.Point[] points)
        {
            var stats = new List<StatPoint>();

            for (var i = 0; i < points.Length; i++)
            {
                var point = points[i];

                stats.Add(new StatPoint
                {
                    Date = point.Date,
                    Stat = Stat.Deaths,
                    Delta = point.DeathsDelta,
                    Cumulative = point.DeathsCumulative
                });

                stats.Add(new StatPoint
                {
                    Date = point.Date,
                    Stat = Stat.Confirmed,
                    Delta = point.ConfirmedDelta,
                    Cumulative = point.ConfirmedCumulative
                });
            }

            return stats;
        }
    }

    public class TS
    {
        public class Point
        {
            public LocalDate Date { get; set; }

            public double ConfirmedDelta { get; set; }

            public double ConfirmedCumulative { get; set; }

            public double DeathsDelta { get; set; }

            public double DeathsCumulative { get; set; }
        }

        public Country Country { get; set; }

        public string Name => Country.Name;

        public Point[] Points { get; set; }

        public double ConfirmedDelta { get; set; }

        public double ConfirmedCumulative { get; set; }

        public double DeathsDelta { get; set; }

        public double DeathsCumulative { get; set; }
    }

    public enum Stat
    {
        Confirmed,
        Deaths
    }

    public class StatPoint
    {
        public LocalDate Date { get; set; }

        public Stat Stat { get; set; }

        public double Delta { get; set; }

        public double Cumulative { get; set; }
    }
}
