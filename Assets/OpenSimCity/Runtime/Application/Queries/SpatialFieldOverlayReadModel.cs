using System;
using System.Collections.Generic;
using OpenSimCity.Core.Mathematics;

namespace OpenSimCity.Application.Queries
{
    public enum SpatialFieldBand : byte
    {
        VeryLow,
        Low,
        Medium,
        High,
        VeryHigh
    }

    public enum SpatialFieldOverlayPattern : byte
    {
        Dots,
        Solid,
        Rings,
        Hatch,
        CrossHatch
    }

    [Serializable]
    public readonly struct SpatialFieldOverlayCell
    {
        public SpatialFieldOverlayCell(
            CellCoord cell,
            int value,
            SpatialFieldBand band,
            SpatialFieldOverlayPattern pattern,
            bool isAdverse,
            string label)
        {
            Cell = cell;
            Value = value;
            Band = band;
            Pattern = pattern;
            IsAdverse = isAdverse;
            Label = label ?? string.Empty;
        }
        public CellCoord Cell { get; }
        public int Value { get; }
        public SpatialFieldBand Band { get; }
        public SpatialFieldOverlayPattern Pattern { get; }
        public bool IsAdverse { get; }
        public string Label { get; }
    }

    [Serializable]
    public readonly struct SpatialFieldLegendEntry
    {
        public SpatialFieldLegendEntry(SpatialFieldBand band, int minimum, int maximum, string label)
        {
            Band = band;
            Minimum = minimum;
            Maximum = maximum;
            Label = label ?? string.Empty;
        }
        public SpatialFieldBand Band { get; }
        public int Minimum { get; }
        public int Maximum { get; }
        public string Label { get; }
    }

    [Serializable]
    public sealed class SpatialFieldOverlaySnapshot
    {
        public int SourceVersion;
        public SpatialFieldViewKind Kind;
        public string Title = string.Empty;
        public bool HigherIsAdverse;
        public IReadOnlyList<SpatialFieldOverlayCell> Cells =
            Array.AsReadOnly(Array.Empty<SpatialFieldOverlayCell>());
        public IReadOnlyList<SpatialFieldLegendEntry> Legend =
            Array.AsReadOnly(Array.Empty<SpatialFieldLegendEntry>());
    }

    public sealed class SpatialFieldOverlayReadModel
    {
        private readonly SpatialFieldReadModel _fields;

        public SpatialFieldOverlayReadModel(SpatialFieldReadModel fields)
        {
            _fields = fields ?? throw new ArgumentNullException(nameof(fields));
        }

        public SpatialFieldOverlaySnapshot Build(SpatialFieldViewKind kind)
        {
            _fields.RefreshIfNeeded();
            var source = _fields.Snapshot;
            var adverse = kind == SpatialFieldViewKind.Pollution ||
                          kind == SpatialFieldViewKind.Noise ||
                          kind == SpatialFieldViewKind.Crime;
            var cells = new SpatialFieldOverlayCell[source.Cells.Count];
            for (var index = 0; index < cells.Length; index++)
            {
                var value = source.Cells[index].Get(kind);
                var band = BandFor(value);
                cells[index] = new SpatialFieldOverlayCell(
                    source.Cells[index].Cell,
                    value,
                    band,
                    PatternFor(band),
                    adverse,
                    LabelFor(band));
            }
            return new SpatialFieldOverlaySnapshot
            {
                SourceVersion = source.Version,
                Kind = kind,
                Title = TitleFor(kind),
                HigherIsAdverse = adverse,
                Cells = Array.AsReadOnly(cells),
                Legend = Legend()
            };
        }

        public static SpatialFieldBand BandFor(int value)
        {
            if (value < 0 || value > 1000)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (value < 200) return SpatialFieldBand.VeryLow;
            if (value < 400) return SpatialFieldBand.Low;
            if (value < 600) return SpatialFieldBand.Medium;
            if (value < 800) return SpatialFieldBand.High;
            return SpatialFieldBand.VeryHigh;
        }

        private static SpatialFieldOverlayPattern PatternFor(SpatialFieldBand band)
        {
            switch (band)
            {
                case SpatialFieldBand.VeryLow: return SpatialFieldOverlayPattern.Dots;
                case SpatialFieldBand.Low: return SpatialFieldOverlayPattern.Solid;
                case SpatialFieldBand.Medium: return SpatialFieldOverlayPattern.Rings;
                case SpatialFieldBand.High: return SpatialFieldOverlayPattern.Hatch;
                default: return SpatialFieldOverlayPattern.CrossHatch;
            }
        }

        private static string LabelFor(SpatialFieldBand band)
        {
            switch (band)
            {
                case SpatialFieldBand.VeryLow: return "매우 낮음";
                case SpatialFieldBand.Low: return "낮음";
                case SpatialFieldBand.Medium: return "보통";
                case SpatialFieldBand.High: return "높음";
                default: return "매우 높음";
            }
        }

        private static string TitleFor(SpatialFieldViewKind kind)
        {
            switch (kind)
            {
                case SpatialFieldViewKind.LandValue: return "토지가치";
                case SpatialFieldViewKind.Pollution: return "오염";
                case SpatialFieldViewKind.Noise: return "소음";
                case SpatialFieldViewKind.Crime: return "범죄";
                case SpatialFieldViewKind.ParkAmenity: return "공원 효과";
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private static IReadOnlyList<SpatialFieldLegendEntry> Legend()
        {
            return Array.AsReadOnly(new[]
            {
                new SpatialFieldLegendEntry(SpatialFieldBand.VeryLow, 0, 199, "매우 낮음 · 점"),
                new SpatialFieldLegendEntry(SpatialFieldBand.Low, 200, 399, "낮음 · 실선"),
                new SpatialFieldLegendEntry(SpatialFieldBand.Medium, 400, 599, "보통 · 원형"),
                new SpatialFieldLegendEntry(SpatialFieldBand.High, 600, 799, "높음 · 사선"),
                new SpatialFieldLegendEntry(SpatialFieldBand.VeryHigh, 800, 1000, "매우 높음 · 교차선")
            });
        }
    }
}
