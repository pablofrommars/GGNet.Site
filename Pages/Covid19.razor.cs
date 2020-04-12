using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;

using NodaTime;

using GGNet.Components;
using GGNet.Elements;
using GGNet.Palettes;

using GGNet.NaturalEarth;

using GGNet.Site.Data;

using static System.Math;

namespace GGNet.Site.Pages
{
    public partial class Covid19 : ComponentBase
    {
        private int sideMenuItem = 0;
        private int mainMenuItem = 0;
        private int mainMenuPlotItem = 0;

        private List<TS> data;
        private TS ts;

        private Data<TS, double, double> map;

        private readonly Dictionary<string, (Data<TS.Point, LocalDate, double> confirmed, Data<TS.Point, LocalDate, double> deaths)> sparks =
            new Dictionary<string, (Data<TS.Point, LocalDate, double> confirmed, Data<TS.Point, LocalDate, double> deaths)>();

        private Source<TS.Point> source = new Source<TS.Point>();
        private Data<TS.Point, LocalDate, double> data1;
        private Data<TS.Point, LocalDate, double> data2;
        private Plot<TS.Point, LocalDate, double> plot1;
        private Plot<TS.Point, LocalDate, double> plot2;

        private Data<TS, double, double> cfr;

        private Data<(TS ts, double age), double, double> age;

        protected override async Task OnInitializedAsync()
        {
            data = await Service.GetDataAsync().ConfigureAwait(false);

            {
                var theme = Theme.Default(dark: false);
                theme.Tooltip.Text.Size = new Size { Value = 16, Units = Units.px };

                foreach (var ts in data)
                {
                    var source = new Source<TS.Point>(ts.Points.TakeLast(15));

                    var confirmed = Plot.New(source, o => o.Date, o => o.ConfirmedCumulative)
                        .Geom_Bar(tooltip: o => $"{o.ConfirmedCumulative:#,###0}", fill: "var(--confirmed-color)", alpha: 0.8, animation: true)
                        .Theme(theme);

                    var deaths = Plot.New(source, o => o.Date, o => o.DeathsCumulative)
                        .Geom_Bar(tooltip: o => $"{o.DeathsCumulative:#,###0}", fill: "var(--deaths-color)", alpha: 0.8, animation: true)
                        .Theme(theme);

                    sparks[ts.Country.A2] = (confirmed, deaths);
                }
            }

            {
                var theme = Theme.Default(dark: false);

                theme.Panel.Grid.Major.X.Alpha = 0.25;
                theme.Panel.Grid.Minor.X.Alpha = 0.25;
                theme.Panel.Grid.Major.Y.Alpha = 0.25;
                theme.Panel.Grid.Minor.Y.Alpha = 0.25;

                theme.Tooltip.Margin = new Margin();
                theme.Tooltip.Background = "#FFFFFF";
                theme.Tooltip.Alpha = 1.0;
                theme.Tooltip.Text.Color = "#000000";

                map = Plot.New(data.Where(o => o.Name != "World"), o => o.Country.Capital.Point.Longitude, o => o.Country.Capital.Point.Latitude)
                    .Geom_Map(Scale110.Countries, o => o.Polygons, fill: "#6e84a3", alpha: 0.1, color: "#FFFFFF", width: 0.5)
                    .Geom_Point(onclick: (ts, e) => OnClick(ts), tooltip: Tooltip, color: "#000000", alpha: 0.5, animation: true)
                    .Scale_Longitude()
                    .Scale_Latitude()
                    .Scale_Color_Discrete(o => o.Country.Continent, Colors.Viridis, guide: false)
                    .Scale_Size_Continuous(o => o.ConfirmedCumulative, range: (1, 12), guide: false)
                    .Theme(theme);
            }

            ts = data.First();
            source.Add(ts.Points);

            var palette = Discrete<Stat, string>.Enum(new[] { "", "var(--deaths-color)" });

            data1 = Plot.New(source, o => o.Date, o => o.ConfirmedCumulative)
                .Geom_Bar(y: o => o.ConfirmedDelta, tooltip: Tooltip1, fill: "var(--confirmed-color)", alpha: 0.6)
                .Geom_Line(tooltip: Tooltip1, color: "var(--confirmed-color)", width: 3, alpha: 0.8)
                .Scale_Y_Continuous("#,##0")
                .Theme(dark: false, legend: Position.Bottom);

            data2 = Plot.New(source, o => o.Date, o => o.DeathsCumulative)
                .Geom_Bar(y: o => o.DeathsDelta, tooltip: Tooltip2, fill: "var(--deaths-color)", alpha: 0.6)
                .Geom_Line(tooltip: Tooltip2, color: "var(--deaths-color)", width: 3, alpha: 0.8)
                .Scale_Y_Continuous("#,##0")
                .Theme(dark: false, legend: Position.Bottom);

            {
                var theme = Theme.Default(dark: false);

                theme.Tooltip.Margin = new Margin();
                theme.Tooltip.Background = "#FFFFFF";
                theme.Tooltip.Alpha = 1.0;
                theme.Tooltip.Text.Color = "#000000";

                cfr = Plot.New(data.Where(o => o.DeathsCumulative >= 5 && !string.IsNullOrEmpty(o.Country.Continent)), o => o.ConfirmedCumulative, o => o.DeathsCumulative)
                    .Title("Case Fatality Rate")
                    .Geom_ABLine(new[] { 0.005, 0.01, 0.02, 0.05, 0.1 }, o => o, o => 0, o => $"{o:P2}".Replace(".00", ""), color: "rgba(0, 0, 0, 0.3)", lineType: LineType.Dotted)
                    .Geom_Point(tooltip: TooltipCFR, size: 3, alpha: 0.5, animation: true)
                    .Scale_X_Log10()
                    .XLab("Confirmed Cases - Log")
                    .Scale_Y_Log10()
                    .YLab("Confirmed Deaths - Log")
                    .Scale_Color_Discrete(o => o.Country.Continent, Colors.Viridis)
                    .Theme(theme);
            }

            {
                var ageData = await Service.GetAgeDataAsync().ConfigureAwait(false);

                var combined = data
                    .Where(o => o.Population > 0 && o.DeathsCumulative > 5 && !string.IsNullOrEmpty(o.Country.Continent))
                        .Join(ageData, o => o.Country.A3, o => o.a3, (o, p) => (ts: o, age: p.age60_))
                        .ToList();

                var t = 1.9855;
                var n = 86;
                var mse = 0.4248488275613588;
                var xavg = 0.170082554;
                var sx = 0.0831531957;
                var N = 100;

                double CI(double x) => t * Sqrt(n / (n - 2.0) * mse) * Sqrt(1.0 / n + (x - xavg) * (x - xavg) / ((n - 1.0) * sx * sx));

                age = Plot.New(combined, o => o.age / 100, o => o.ts.DeathsCumulative / o.ts.Population * 1000000.0)
                    .Title("Age impact on Mortality")
                    .SubTitle("R²=0.4")
                    .Geom_Ribbon(Enumerable.Range(0, N).Select(o => o * 0.4 / N), o => o, o => Pow(10.0, 6.37009 * o - 0.540860355 - CI(o)), o => Pow(10.0, 6.37009 * o - 0.540860355 + CI(o)), fill: "#6e84a3", alpha: 0.1, scale: (false, true))
                    .Geom_ABLine(new[] { (a: 6.37009, b: -0.540860355) }, o => o.a, o => o.b, o => $"y = {o.a:N2}x {o.b:N2}", transformation: (false, false), color: "rgba(0, 0, 0, 0.3)")
                    .Geom_Point(tooltip: o => o.ts.Name, size: 3, alpha: 0.5, animation: true)
                    .Scale_X_Continuous(format: "P0")
                    .Scale_Y_Log10()
                    .XLab("Proportion of Population above 60")
                    .YLab("Confirmed Deaths / Million - Log")
                    .Scale_Color_Discrete(o => o.ts.Country.Continent, Colors.Viridis)
                    .Theme(dark: false);
            }
        }

        public async Task OnClick(TS ts)
        {
            this.ts = ts;

            source.Clear();

            source.Add(ts.Points);

            StateHasChanged();

            await plot1.RefreshAsync().ConfigureAwait(false);
            await plot2.RefreshAsync().ConfigureAwait(false);
        }

        public string Tooltip(TS ts) =>
$@"
<div class=""border rounded"" style=""padding: 5px 10px;"">
    <div class=""text-center font-weight-bold border-bottom"" style=""color: var(--tooltip-color);"">{ts.Country.Name}</div>
    <div class=""d-flex justify-content-center"">
        <div><div class=""text-center mr-2"">Confirmed</div><div class=""text-center font-weight-bold mr-2"" style=""color: var(--confirmed-color)"">{ts.ConfirmedCumulative:#,##0} (+{ts.ConfirmedDelta:#,##0})</div></div>
        <div><div class=""text-center"">Deaths</div><div class=""text-center font-weight-bold"" style=""color: var(--deaths-color)"">{ts.DeathsCumulative:#,##0} (+{ts.DeathsDelta:#,##0})</div></div>
    </div>
</div>
";

        public string Tooltip1(TS.Point o) =>
$@"
<b>{o.Date}</b><br/>
{o.ConfirmedCumulative:#,##0} (+{o.ConfirmedDelta:#,##0})
";

        public string Tooltip2(TS.Point o) =>
$@"
<b>{o.Date}</b><br/>
{o.DeathsCumulative:#,##0} (+{o.DeathsDelta:#,##0})
";

        public string TooltipCFR(TS ts) =>
$@"
<div class=""border rounded"" style=""padding: 5px 10px;"">
    <div class=""text-center font-weight-bold border-bottom"" style=""color: var(--tooltip-color);"">{ts.Country.Name}</div>
    <div class=""d-flex justify-content-center"">
        <div><div class=""text-center mr-2"">Confirmed</div><div class=""text-center font-weight-bold mr-2"" style=""color: var(--confirmed-color)"">{ts.ConfirmedCumulative:#,##0}</div></div>
        <div><div class=""text-center mr-2"">Deaths</div><div class=""text-center font-weight-bold"" style=""color: var(--deaths-color)"">{ts.DeathsCumulative:#,##0}</div></div>
        <div><div class=""text-center"">CFR(%)</div><div class=""text-center font-weight-bold"">{ts.CFR * 100:N1}</div></div>
    </div>
</div>
";
    }
}
